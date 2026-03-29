using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GB28181Platform.Diagnostic.Browser;

public class CameraBrowserAgent
{
    private readonly HttpClient _qwenHttp;
    private readonly IConfiguration _config;
    private readonly ILogger<CameraBrowserAgent> _logger;

    public CameraBrowserAgent(IConfiguration config, ILogger<CameraBrowserAgent> logger)
    {
        _config = config;
        _logger = logger;
        var baseUrl = (config["QwenApi:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
        _qwenHttp = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    /// <summary>
    /// DOM 模式：登录摄像机网页，直接读取 DOM 中的国标配置
    /// </summary>
    public async Task<BrowserCheckResult> CheckCameraConfigByDomAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId)
    {
        try
        {
            var browserPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserPath);

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();

            var url = $"http://{ip}:{port}";
            await page.GotoAsync(url, new() { Timeout = 15000 });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // 尝试登录（如果有用户名密码）
            if (!string.IsNullOrEmpty(username))
            {
                _logger.LogInformation("尝试登录摄像机 {Ip}:{Port}", ip, port);
                await TryLoginAsync(page, username, password ?? "");
            }

            // 尝试导航到国标/GB28181 配置页面
            await TryNavigateToGb28181PageAsync(page);

            // 提取页面中所有 input/select 的值
            var configData = await ExtractPageConfigAsync(page);
            _logger.LogInformation("DOM 提取到 {Count} 个配置项", configData.Count);

            // 分析配置
            var analysis = AnalyzeConfig(configData, expectedSipServerIp, expectedServerId);

            return new BrowserCheckResult
            {
                Success = true,
                Analysis = analysis
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DOM 模式检查失败: {Ip}:{Port}", ip, port);
            return new BrowserCheckResult
            {
                Success = false,
                Analysis = $"DOM 模式检查失败: {ex.Message}"
            };
        }
    }

    private async Task TryLoginAsync(IPage page, string username, string password)
    {
        try
        {
            // 常见的登录表单选择器
            var usernameSelectors = new[] { "input[name='username']", "input[name='user']", "input[name='loginName']", "#username", "#user" };
            var passwordSelectors = new[] { "input[name='password']", "input[name='pass']", "input[name='loginPass']", "#password", "#pass" };
            var submitSelectors = new[] { "button[type='submit']", "input[type='submit']", ".login-btn", "#loginBtn", "button.btn-login" };

            foreach (var sel in usernameSelectors)
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el != null) { await el.FillAsync(username); break; }
            }
            foreach (var sel in passwordSelectors)
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el != null) { await el.FillAsync(password); break; }
            }
            foreach (var sel in submitSelectors)
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el != null) { await el.ClickAsync(); break; }
            }

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            _logger.LogInformation("登录操作完成");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("登录尝试失败: {Message}", ex.Message);
        }
    }

    private async Task TryNavigateToGb28181PageAsync(IPage page)
    {
        try
        {
            // 常见的国标配置页面关键词
            var keywords = new[] { "GB28181", "gb28181", "国标", "SIP", "平台接入", "上级平台", "国标接入" };
            foreach (var keyword in keywords)
            {
                var link = await page.QuerySelectorAsync($"a:has-text('{keyword}'), span:has-text('{keyword}'), div:has-text('{keyword}')");
                if (link != null)
                {
                    await link.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    _logger.LogInformation("导航到国标配置页面: {Keyword}", keyword);
                    return;
                }
            }
            _logger.LogWarning("未找到国标配置页面链接，将分析当前页面");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("导航到国标页面失败: {Message}", ex.Message);
        }
    }

    private async Task<Dictionary<string, string>> ExtractPageConfigAsync(IPage page)
    {
        var config = new Dictionary<string, string>();

        // 提取所有 input 元素的 label + value
        var inputs = await page.QuerySelectorAllAsync("input[type='text'], input[type='number'], input[type='password']");
        foreach (var input in inputs)
        {
            var name = await input.GetAttributeAsync("name") ?? await input.GetAttributeAsync("id") ?? "";
            var value = await input.InputValueAsync();
            var placeholder = await input.GetAttributeAsync("placeholder") ?? "";
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                config[name] = value;
            else if (!string.IsNullOrEmpty(placeholder) && !string.IsNullOrEmpty(value))
                config[placeholder] = value;
        }

        // 提取 select 元素
        var selects = await page.QuerySelectorAllAsync("select");
        foreach (var select in selects)
        {
            var name = await select.GetAttributeAsync("name") ?? await select.GetAttributeAsync("id") ?? "";
            var value = await select.EvaluateAsync<string>("el => el.options[el.selectedIndex]?.text || el.value");
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                config[name] = value;
        }

        // 提取页面文本中的关键配置（兜底）
        var bodyText = await page.InnerTextAsync("body");
        ExtractConfigFromText(config, bodyText);

        return config;
    }

    private void ExtractConfigFromText(Dictionary<string, string> config, string text)
    {
        // 匹配常见的 "标签: 值" 或 "标签：值" 模式
        var patterns = new Dictionary<string, string[]>
        {
            ["SIP服务器IP"] = new[] { "SIP服务器", "SIP Server", "服务器地址", "Server IP", "平台IP" },
            ["SIP服务器端口"] = new[] { "SIP端口", "SIP Port", "服务器端口", "Server Port", "平台端口" },
            ["设备编码"] = new[] { "设备编码", "Device ID", "设备ID", "SIP用户" },
            ["SIP域"] = new[] { "SIP域", "SIP Domain", "Realm", "域" },
        };

        foreach (var (key, labels) in patterns)
        {
            if (config.ContainsKey(key)) continue;
            foreach (var label in labels)
            {
                var idx = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                // 取标签后面的内容（跳过冒号和空格）
                var after = text[(idx + label.Length)..];
                after = after.TrimStart(':', '：', ' ', '\t', '\n', '\r');
                var value = new string(after.TakeWhile(c => !char.IsWhiteSpace(c) && c != '\n' && c != '\r').ToArray());
                if (!string.IsNullOrEmpty(value))
                {
                    config[key] = value;
                    break;
                }
            }
        }
    }

    private string AnalyzeConfig(Dictionary<string, string> config, string expectedSipIp, string expectedServerId)
    {
        var lines = new List<string>();
        lines.Add("=== DOM 模式配置检查 ===");
        lines.Add($"提取到 {config.Count} 个配置项:");

        foreach (var (k, v) in config)
            lines.Add($"  {k} = {v}");

        // 检查 SIP 服务器 IP
        var sipIpKeys = config.Keys.Where(k =>
            k.Contains("SIP", StringComparison.OrdinalIgnoreCase) &&
            (k.Contains("IP", StringComparison.OrdinalIgnoreCase) || k.Contains("服务器", StringComparison.OrdinalIgnoreCase) || k.Contains("Server", StringComparison.OrdinalIgnoreCase)));

        foreach (var key in sipIpKeys)
        {
            var val = config[key];
            if (val == expectedSipIp)
                lines.Add($"✓ {key}={val} 与期望值一致");
            else
                lines.Add($"✗ {key}={val} 与期望值 {expectedSipIp} 不一致");
        }

        // 检查设备编码
        var deviceIdKeys = config.Keys.Where(k =>
            k.Contains("编码", StringComparison.OrdinalIgnoreCase) || k.Contains("DeviceID", StringComparison.OrdinalIgnoreCase) || k.Contains("Device ID", StringComparison.OrdinalIgnoreCase));

        foreach (var key in deviceIdKeys)
        {
            var val = config[key];
            lines.Add($"  设备编码: {val}");
        }

        if (config.Count == 0)
            lines.Add("未能从页面提取到配置信息，可能需要登录或页面结构不支持自动提取");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 截图模式：截图 + 视觉模型分析（原有方式）
    /// </summary>
    public async Task<BrowserCheckResult> CheckCameraConfigByScreenshotAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId)
    {
        var screenshotDir = Path.Combine("screenshots", DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir, $"{ip}_{DateTime.Now:HHmmss}.png");

        try
        {
            var browserPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserPath);

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"http://{ip}:{port}", new() { Timeout = 15000 });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
            _logger.LogInformation("摄像机网页截图已保存: {Path}", screenshotPath);

            var analysis = await AnalyzeScreenshotAsync(screenshotPath, expectedSipServerIp, expectedServerId);
            return new BrowserCheckResult { Success = true, ScreenshotPath = screenshotPath, Analysis = analysis };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "截图模式检查失败: {Ip}:{Port}", ip, port);
            return new BrowserCheckResult { Success = false, ScreenshotPath = screenshotPath, Analysis = $"截图模式失败: {ex.Message}" };
        }
    }

    private async Task<string> AnalyzeScreenshotAsync(string screenshotPath, string expectedSipIp, string expectedServerId)
    {
        var imageBytes = await File.ReadAllBytesAsync(screenshotPath);
        var base64 = Convert.ToBase64String(imageBytes);

        var prompt = $@"这是一台摄像机的网页管理界面截图。请分析截图中是否能看到 GB28181/国标 相关配置。
如果能看到，请提取以下信息：SIP 服务器 IP、SIP 服务器端口、设备编码、SIP 域/realm
然后与期望配置对比：期望 SIP 服务器 IP: {expectedSipIp}，期望服务器编码: {expectedServerId}
如果有差异请指出，如果看不到国标配置页面请说明。";

        var requestBody = new
        {
            model = _config["QwenApi:VisionModel"] ?? _config["QwenApi:Model"] ?? "qwen-vl-plus",
            messages = new[] { new { role = "user", content = new object[] {
                new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64}" } },
                new { type = "text", text = prompt }
            } } },
            max_tokens = 1000
        };

        var apiKey = _config["QwenApi:ApiKey"] ?? "";
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(requestBody);

        var response = await _qwenHttp.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"视觉 API 调用失败 ({response.StatusCode}): {rawJson[..Math.Min(200, rawJson.Length)]}";

        var json = JsonSerializer.Deserialize<JsonElement>(rawJson);
        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
            return contentEl.ValueKind == JsonValueKind.String ? contentEl.GetString() ?? "分析结果为空" : contentEl.ToString();

        return $"视觉分析返回格式异常";
    }
}

public class BrowserCheckResult
{
    public bool Success { get; set; }
    public string ScreenshotPath { get; set; } = "";
    public string Analysis { get; set; } = "";
}
