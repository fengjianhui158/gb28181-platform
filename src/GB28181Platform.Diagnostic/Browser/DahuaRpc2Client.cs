using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.Diagnostic.Browser;

/// <summary>
/// 大华摄像机 RPC2 HTTP 客户端
/// 直接通过 HTTP 调用 RPC2 接口获取国标配置，不需要浏览器
/// 
/// 大华 RPC2 协议流程：
/// 1. POST /RPC2_Login 第一次 → 拿到 realm, random, session
/// 2. 用 MD5 摘要算法计算密码哈希
/// 3. POST /RPC2_Login 第二次 → 带哈希登录，拿到认证后的 session
/// 4. POST /RPC2 + session → configManager.getConfig({ name: "GBT28181" })
/// </summary>
public class DahuaRpc2Client
{
    private readonly ILogger<DahuaRpc2Client> _logger;
    private static readonly HttpClientHandler _handler = new()
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    };

    public DahuaRpc2Client(ILogger<DahuaRpc2Client> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取大华摄像机的国标(GBT28181)配置
    /// </summary>
    public async Task<DahuaGBConfig?> GetGBConfigAsync(string ip, int port, string username, string password)
    {
        using var http = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri($"http://{ip}:{port}"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            // 第一步：获取登录挑战（realm, random, session）
            var (session, realm, random) = await GetLoginChallengeAsync(http);
            if (string.IsNullOrEmpty(session))
            {
                _logger.LogWarning("大华 RPC2 登录挑战失败: {Ip}", ip);
                return null;
            }

            _logger.LogInformation("大华 RPC2 登录挑战成功: session={Session}, realm={Realm}, random={Random}", session, realm, random);

            // 第二步：尝试多种密码哈希算法登录
            var passwordHashes = ComputeDahuaPasswordHashes(username, password, realm, random);
            var loginOk = false;
            var hashIndex = 0;

            foreach (var hash in passwordHashes)
            {
                hashIndex++;
                _logger.LogInformation("尝试第 {Index}/{Total} 种密码哈希方式登录", hashIndex, passwordHashes.Count);
                loginOk = await LoginWithHashAsync(http, session, username, hash);
                if (loginOk) break;

                // 登录失败后可能需要重新获取挑战（session 可能失效）
                if (hashIndex < passwordHashes.Count)
                {
                    (session, realm, random) = await GetLoginChallengeAsync(http);
                    if (string.IsNullOrEmpty(session))
                    {
                        _logger.LogWarning("重新获取登录挑战失败");
                        break;
                    }
                    // 重新计算剩余的哈希（因为 random 变了）
                    passwordHashes = ComputeDahuaPasswordHashes(username, password, realm, random);
                }
            }

            if (!loginOk)
            {
                _logger.LogWarning("大华 RPC2 登录认证失败: {Ip}, 用户={User}", ip, username);
                return null;
            }

            _logger.LogInformation("大华 RPC2 登录成功: {Ip}", ip);

            // 第三步：获取 GBT28181 配置
            var configNames = new[] { "GBT28181", "GB28181", "T28181" };
            foreach (var name in configNames)
            {
                var configJson = await GetConfigAsync(http, session, name);
                if (configJson != null)
                {
                    _logger.LogInformation("获取到大华 {Name} 配置: {Ip}", name, ip);
                    var config = ParseGBConfig(configJson.Value);
                    if (config != null) return config;
                }
            }

            _logger.LogWarning("大华 RPC2 未找到国标配置: {Ip}", ip);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("大华 RPC2 调用异常: {Ip}, {Msg}", ip, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 第一步：发送空登录请求，获取挑战参数
    /// </summary>
    private async Task<(string session, string realm, string random)> GetLoginChallengeAsync(HttpClient http)
    {
        var body = new
        {
            method = "global.login",
            @params = new
            {
                userName = "",
                password = "",
                clientType = "Web3.0"
            },
            id = 1
        };

        var resp = await http.PostAsJsonAsync("/RPC2_Login", body);
        var json = await resp.Content.ReadAsStringAsync();
        _logger.LogInformation("RPC2_Login 挑战响应: {Json}", json[..Math.Min(500, json.Length)]);

        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        var session = "";
        var realm = "";
        var random = "";

        if (doc.TryGetProperty("session", out var s))
            session = s.GetString() ?? "";

        if (doc.TryGetProperty("params", out var p))
        {
            if (p.TryGetProperty("realm", out var r)) realm = r.GetString() ?? "";
            if (p.TryGetProperty("random", out var rnd)) random = rnd.GetString() ?? "";
        }
        // 有些固件版本用 error.params
        else if (doc.TryGetProperty("error", out var err) && err.TryGetProperty("params", out var ep))
        {
            if (ep.TryGetProperty("realm", out var r)) realm = r.GetString() ?? "";
            if (ep.TryGetProperty("random", out var rnd)) random = rnd.GetString() ?? "";
        }

        return (session, realm, random);
    }

    /// <summary>
    /// 大华密码哈希算法 - 支持多种版本自动尝试
    /// </summary>
    private static List<string> ComputeDahuaPasswordHashes(string username, string password, string realm, string random)
    {
        var hashes = new List<string>();

        // 方式1（旧版固件）：HA1 = MD5(username:realm:password)
        var ha1_v1 = Md5Hex($"{username}:{realm}:{password}");
        hashes.Add(Md5Hex($"{username}:{random}:{ha1_v1}").ToUpperInvariant());

        // 方式2（新版固件）：HA1 = MD5(username:realm:MD5(password))
        var md5Pass = Md5Hex(password);
        var ha1_v2 = Md5Hex($"{username}:{realm}:{md5Pass}");
        hashes.Add(Md5Hex($"{username}:{random}:{ha1_v2}").ToUpperInvariant());

        // 方式3：HA1 = MD5(username:realm:password) 但最终哈希小写
        hashes.Add(Md5Hex($"{username}:{random}:{ha1_v1}"));

        // 方式4：HA1 = MD5(username:realm:MD5(password)) 最终哈希小写
        hashes.Add(Md5Hex($"{username}:{random}:{ha1_v2}"));

        return hashes.Distinct().ToList();
    }

    private static string Md5Hex(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 第二步：带哈希密码登录
    /// </summary>
    private async Task<bool> LoginWithHashAsync(HttpClient http, string session, string username, string passwordHash)
    {
        var body = new
        {
            method = "global.login",
            @params = new
            {
                userName = username,
                password = passwordHash,
                clientType = "Web3.0",
                authorityType = "Default"
            },
            id = 2,
            session
        };

        var resp = await http.PostAsJsonAsync("/RPC2_Login", body);
        var json = await resp.Content.ReadAsStringAsync();
        _logger.LogInformation("RPC2_Login 认证响应: {Json}", json[..Math.Min(500, json.Length)]);

        // 登录成功时 result=true
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        if (doc.TryGetProperty("result", out var result) && result.GetBoolean())
            return true;

        // 有些固件返回 error code
        if (doc.TryGetProperty("error", out var err))
            _logger.LogWarning("登录错误详情: {Err}", err.ToString());

        return false;
    }

    /// <summary>
    /// 第三步：获取指定名称的配置
    /// </summary>
    private async Task<JsonElement?> GetConfigAsync(HttpClient http, string session, string configName)
    {
        var body = new
        {
            method = "configManager.getConfig",
            @params = new { name = configName },
            id = 3,
            session
        };

        var resp = await http.PostAsJsonAsync("/RPC2", body);
        var json = await resp.Content.ReadAsStringAsync();
        _logger.LogDebug("RPC2 getConfig({Name}) 响应: {Json}", configName, json[..Math.Min(500, json.Length)]);

        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        // 成功时有 result=true 和 params
        if (doc.TryGetProperty("result", out var result) && result.GetBoolean() &&
            doc.TryGetProperty("params", out var configParams))
        {
            _logger.LogInformation("RPC2 getConfig({Name}) 成功，配置数据长度: {Len}", configName, json.Length);
            return configParams;
        }

        return null;
    }

    /// <summary>
    /// 解析 GBT28181 配置 JSON 为结构化对象
    /// 大华返回格式示例：
    /// { "table": { "Enable": true, "SIPServerID": "340200...", "SIPServerIP": "192.168.1.100", ... } }
    /// 或者直接平铺字段
    /// </summary>
    private DahuaGBConfig? ParseGBConfig(JsonElement configParams)
    {
        try
        {
            var config = new DahuaGBConfig();

            // 大华可能把配置放在 table 或 GBT28181 子对象里
            var root = configParams;
            if (configParams.TryGetProperty("table", out var table))
                root = table;
            else if (configParams.TryGetProperty("GBT28181", out var gb))
                root = gb;

            // 遍历所有属性，按名称匹配
            foreach (var prop in root.EnumerateObject())
            {
                var name = prop.Name.ToLowerInvariant();
                var value = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.ToString();

                switch (name)
                {
                    case "enable":
                        config.Enable = prop.Value.ValueKind == JsonValueKind.True ||
                                        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                        value == "1";
                        break;
                    case "sipserverip" or "sip_server_ip" or "serverip" or "server_ip":
                        config.SipServerIp = value;
                        break;
                    case "sipserverid" or "sip_server_id" or "serverid" or "server_id":
                        config.SipServerId = value;
                        break;
                    case "sipserverport" or "sip_server_port" or "serverport" or "server_port":
                        config.SipServerPort = value;
                        break;
                    case "deviceid" or "device_id" or "sipdeviceid" or "sip_device_id" or "localid":
                        config.DeviceId = value;
                        break;
                    case "localsipport" or "local_sip_port" or "sipport" or "sip_port" or "localport":
                        config.LocalSipPort = value;
                        break;
                    case "sipdomain" or "sip_domain" or "realm" or "domain":
                        config.SipDomain = value;
                        break;
                    case "password" or "sippassword" or "sip_password":
                        config.Password = "***"; // 不记录密码
                        break;
                    default:
                        // 记录未识别的字段，方便调试
                        config.ExtraFields[prop.Name] = value;
                        break;
                }
            }

            // 如果关键字段都是空的，说明解析失败
            if (string.IsNullOrEmpty(config.SipServerIp) && string.IsNullOrEmpty(config.SipServerId) && !config.Enable)
            {
                _logger.LogDebug("GBT28181 配置解析结果为空，原始数据: {Json}", root.ToString());
                return null;
            }

            _logger.LogInformation("大华国标配置: Enable={Enable}, ServerIP={Ip}, ServerID={Id}, DeviceID={Did}, Port={Port}",
                config.Enable, config.SipServerIp, config.SipServerId, config.DeviceId, config.SipServerPort);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("解析 GBT28181 配置失败: {Msg}", ex.Message);
            return null;
        }
    }
}

/// <summary>
/// 大华摄像机国标(GBT28181)配置
/// </summary>
public class DahuaGBConfig
{
    public bool Enable { get; set; }
    public string SipServerIp { get; set; } = "";
    public string SipServerId { get; set; } = "";
    public string SipServerPort { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string LocalSipPort { get; set; } = "";
    public string SipDomain { get; set; } = "";
    public string Password { get; set; } = "";
    public Dictionary<string, string> ExtraFields { get; set; } = new();

    /// <summary>
    /// 与期望配置对比，返回中文分析结果
    /// </summary>
    public string CompareWith(string expectedSipServerIp, string expectedServerId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== 大华 RPC2 直连获取的国标配置 ===");
        sb.AppendLine();

        var allMatch = true;

        sb.AppendLine($"  国标接入启用: {(Enable ? "是" : "否")}");
        if (!Enable)
        {
            sb.AppendLine("  ⚠ 国标接入未启用！");
            allMatch = false;
        }

        sb.AppendLine($"  SIP服务器IP: {(string.IsNullOrEmpty(SipServerIp) ? "(空)" : SipServerIp)}");
        if (!string.IsNullOrEmpty(expectedSipServerIp) && expectedSipServerIp != "0.0.0.0")
        {
            var ipMatch = SipServerIp == expectedSipServerIp;
            sb.AppendLine($"    期望值: {expectedSipServerIp} → {(ipMatch ? "✓ 匹配" : "✗ 不匹配")}");
            if (!ipMatch) allMatch = false;
        }

        sb.AppendLine($"  服务器编码: {(string.IsNullOrEmpty(SipServerId) ? "(空)" : SipServerId)}");
        if (!string.IsNullOrEmpty(expectedServerId))
        {
            var idMatch = SipServerId == expectedServerId;
            sb.AppendLine($"    期望值: {expectedServerId} → {(idMatch ? "✓ 匹配" : "✗ 不匹配")}");
            if (!idMatch) allMatch = false;
        }

        sb.AppendLine($"  SIP服务器端口: {(string.IsNullOrEmpty(SipServerPort) ? "(空)" : SipServerPort)}");
        sb.AppendLine($"  设备编码: {(string.IsNullOrEmpty(DeviceId) ? "(空)" : DeviceId)}");
        sb.AppendLine($"  本地SIP端口: {(string.IsNullOrEmpty(LocalSipPort) ? "(空)" : LocalSipPort)}");
        sb.AppendLine($"  SIP域: {(string.IsNullOrEmpty(SipDomain) ? "(空)" : SipDomain)}");

        if (ExtraFields.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  其他字段:");
            foreach (var (k, v) in ExtraFields)
                sb.AppendLine($"    {k} = {v}");
        }

        sb.AppendLine();
        sb.AppendLine(allMatch ? "结论：配置匹配 ✓" : "结论：配置不匹配 ✗，需要修改摄像机配置");

        return sb.ToString();
    }
}
