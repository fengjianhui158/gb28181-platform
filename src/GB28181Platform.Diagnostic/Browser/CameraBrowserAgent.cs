using System.Net.Http.Json;
using System.Text.Json;
using GB28181Platform.AiAgent;
using GB28181Platform.Diagnostic.Browser.VisibleField;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GB28181Platform.Diagnostic.Browser;

public class CameraBrowserAgent
{
    private readonly IQwenClient _qwen;
    private readonly IConfiguration _config;
    private readonly ILogger<CameraBrowserAgent> _logger;
    private readonly DahuaRpc2Client _dahuaRpc2;
    private readonly VisibleFieldConfigExtractor _visibleFieldExtractor;
    private readonly ManufacturerNavigationOptions _manufacturerNavigationOptions;

    public CameraBrowserAgent(IQwenClient qwen, IConfiguration config,
        ILogger<CameraBrowserAgent> logger, DahuaRpc2Client dahuaRpc2,
        VisibleFieldConfigExtractor visibleFieldExtractor,
        ManufacturerNavigationOptions manufacturerNavigationOptions)
    {
        _qwen = qwen;
        _config = config;
        _logger = logger;
        _dahuaRpc2 = dahuaRpc2;
        _visibleFieldExtractor = visibleFieldExtractor;
        _manufacturerNavigationOptions = manufacturerNavigationOptions;
    }

    private async Task<(IPlaywright, IBrowser, IPage)> LaunchBrowserAsync(string ip, int port)
    {
        var browserPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserPath);

        var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"http://{ip}:{port}", new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return (pw, browser, page);
    }

    /// <summary>
    /// AI-DOM 模式：用 AI 分析页面 DOM，智能登录、导航、提取配置
    /// </summary>
    public async Task<BrowserCheckResult> CheckCameraConfigByAiDomAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId, string? manufacturer = null)
    {
        var mfr = (manufacturer ?? "").ToLower();
        var navigationPaths = VisibleFieldNavigationResolver.Resolve(_manufacturerNavigationOptions, manufacturer);

        // ========== 浏览器登录 + RPC2 获取配置 ==========
        IPlaywright? pw = null;
        IBrowser? browser = null;
        try
        {
            (pw, browser, var page) = await LaunchBrowserAsync(ip, port);

            // 注册 RPC2 响应拦截（收集所有 RPC2/CGI 响应）
            var rpcPayloads = new List<string>();
            var rpcConfigJson = "";
            var rpcIntercepted = false;

            page.Response += async (_, response) =>
            {
                try
                {
                    var url = response.Url;
                    if (!url.Contains("RPC2", StringComparison.OrdinalIgnoreCase) &&
                        !url.Contains("cgi-bin", StringComparison.OrdinalIgnoreCase))
                        return;

                    var body = await response.TextAsync();
                    if (string.IsNullOrWhiteSpace(body)) return;

                    _logger.LogInformation("拦截到 RPC/CGI 响应: URL={Url}, 长度={Len}", url, body.Length);

                    // 收集所有含相关关键词的响应
                    if (HasRelevantKeywords(body))
                    {
                        lock (rpcPayloads)
                        {
                            if (!rpcPayloads.Any(existing => existing == body))
                                rpcPayloads.Add(body);
                        }
                    }

                    // 特别标记含 SIP/28181 的响应
                    if (body.Contains("SIP") || body.Contains("sip") || body.Contains("28181") ||
                        body.Contains("GBT28181") || body.Contains("ServerID") || body.Contains("ServerIp"))
                    {
                        rpcConfigJson = body;
                        rpcIntercepted = true;
                        _logger.LogInformation("拦截到 RPC2 国标配置响应: URL={Url}, 长度={Len}", url, body.Length);
                    }
                }
                catch { /* 某些响应可能无法读取 body */ }
            };

            // ========== 第一步：AI 辅助登录 ==========
            if (!string.IsNullOrEmpty(username))
            {
                var loginHtml = await page.EvaluateAsync<string>(@"() => {
                    const forms = document.querySelectorAll('form, .login, [class*=login], [id*=login]');
                    if (forms.length > 0) return forms[0].outerHTML;
                    const inputs = document.querySelectorAll('input, button, a[class*=btn], [type=submit]');
                    return Array.from(inputs).map(el => el.outerHTML).join('\n');
                }");

                _logger.LogInformation("登录页 HTML 片段长度: {Len}", loginHtml?.Length ?? 0);

                if (!string.IsNullOrEmpty(loginHtml))
                {
                    var loginPrompt = $@"以下是一台摄像机登录页面的 HTML 片段：
{loginHtml[..Math.Min(3000, loginHtml.Length)]}

请分析这个登录页面，找到用户名输入框、密码输入框和登录按钮。
只返回 JSON，不要其他内容：
{{""usernameSelector"": ""CSS选择器"", ""passwordSelector"": ""CSS选择器"", ""submitSelector"": ""CSS选择器""}}";

                    var aiResp = await _qwen.ChatAsync(new List<ChatMessage>
                    {
                        new() { Role = "user", Content = loginPrompt }
                    });

                    var selectors = ParseJsonFromAiResponse(aiResp.Content ?? "");
                    if (selectors.HasValue)
                    {
                        await AiAssistedLoginAsync(page, selectors.Value, username, password ?? "");
                    }
                    else
                    {
                        _logger.LogWarning("AI 未能解析登录选择器，尝试智能匹配");
                        await SmartLoginAsync(page, username, password ?? "");
                    }
                }
            }

            var visibleFieldResult = await TryExtractVisibleFieldConfigAsync(page, navigationPaths);
            if (visibleFieldResult.IsSuccess)
            {
                _logger.LogInformation("可见字段模式成功命中国标配置页，命中字段数: {Count}", visibleFieldResult.MatchedFields);
                return new BrowserCheckResult
                {
                    Success = true,
                    Analysis = AnalyzeVisibleConfig(visibleFieldResult.Config, expectedSipServerIp, expectedServerId)
                };
            }

            _logger.LogInformation("可见字段模式未命中有效配置页: {Reason}", visibleFieldResult.FailureReason);

            // ========== 第二步：浏览器登录成功后，用 session 直接调 RPC2 获取国标配置 ==========
            await Task.Delay(3000);

            // 大华摄像机：浏览器登录后，直接在浏览器上下文中调 RPC2 获取 GBT28181 配置
            if (mfr.Contains("dahua") || mfr.Contains("大华") || mfr.Contains("dh"))
            {
                _logger.LogInformation("浏览器登录成功，尝试在浏览器上下文中调 RPC2 获取国标配置");
                var rpc2Result = await FetchGBConfigViaPageAsync(page);
                if (!string.IsNullOrEmpty(rpc2Result))
                {
                    // 拿到了，直接用文本模型分析
                    var rpcPrompt = $@"以下是从大华摄像机 RPC2 接口获取到的国标(GBT28181)配置 JSON：
{rpc2Result[..Math.Min(6000, rpc2Result.Length)]}

我们平台数据库中该设备的期望配置：
- SIP 服务器 IP: {expectedSipServerIp}
- 服务器编码(Server ID): {expectedServerId}

请从 JSON 中提取国标/SIP 相关配置值，逐项与期望配置对比，给出结论。用中文回答。";

                    var rpcResp = await _qwen.ChatAsync(new List<ChatMessage>
                    {
                        new() { Role = "user", Content = rpcPrompt }
                    });

                    return new BrowserCheckResult
                    {
                        Success = true,
                        Analysis = rpcResp.Content ?? "AI 未返回分析结果"
                    };
                }
                _logger.LogInformation("浏览器上下文 RPC2 调用未获取到国标配置，继续导航方式");
            }

            // ========== 第三步：导航到国标配置页 ==========

            var primaryNavigationPath = navigationPaths.FirstOrDefault();
            if (primaryNavigationPath is { Count: > 0 })
            {
                _logger.LogInformation("使用配置路径导航: {Path}", string.Join(" -> ", primaryNavigationPath));
                await NavigateByPathAsync(page, primaryNavigationPath);
                await Task.Delay(3000);

                // 关键：清空之前收集的 RPC2 响应，只保留点击"平台接入"后的
                var platformAccessPayloads = new List<string>();

                // 注册一个新的精确拦截器：收集接下来所有 RPC2 响应（不过滤关键词）
                page.Response += async (_, response) =>
                {
                    try
                    {
                        var url = response.Url;
                        if (!url.Contains("RPC2", StringComparison.OrdinalIgnoreCase)) return;
                        if (url.Contains("RPC2_Login") || url.Contains("RPC2_Notify")) return;

                        var body = await response.TextAsync();
                        if (string.IsNullOrWhiteSpace(body) || body.Length < 50) return;

                        lock (platformAccessPayloads)
                        {
                            platformAccessPayloads.Add(body);
                        }
                        _logger.LogInformation("平台接入 RPC2 响应: 长度={Len}, 前100字符={Preview}",
                            body.Length, body[..Math.Min(100, body.Length)]);
                    }
                    catch { }
                };

                // 重新点击"平台接入"触发配置数据加载
                var lastStep = primaryNavigationPath.LastOrDefault();
                if (!string.IsNullOrEmpty(lastStep))
                {
                    var el = await FindClickableByTextAsync(page, lastStep);
                    if (el != null)
                    {
                        await el.ClickAsync();
                        _logger.LogInformation("重新点击 '{Step}' 以精确捕获配置数据", lastStep);
                    }
                }
                await Task.Delay(5000);

                _logger.LogInformation("平台接入点击后捕获到 {Count} 条 RPC2 响应", platformAccessPayloads.Count);

                // 如果捕获到了响应，直接全部交给 AI 分析
                if (platformAccessPayloads.Count > 0)
                {
                    var allPayloads = string.Join("\n\n---RPC2响应分隔---\n\n",
                        platformAccessPayloads.Select(p => p[..Math.Min(3000, p.Length)]));

                    var rpcPrompt = $@"以下是点击摄像机【平台接入】配置页后，从 RPC2 接口捕获到的所有响应数据：

{allPayloads[..Math.Min(8000, allPayloads.Length)]}

我们平台数据库中该设备的期望配置：
- SIP 服务器 IP: {expectedSipServerIp}
- 服务器编码(Server ID): {expectedServerId}

请：
1. 从上述 RPC2 响应中找到与国标(GB28181)/SIP/平台接入相关的配置数据
2. 提取 SIP 服务器 IP、服务器编码、设备编码、端口、域等配置值
3. 与期望配置逐项对比
4. 如果响应中确实没有国标配置数据，请明确说明

用中文回答，先说结论，再给详细分析。";

                    var rpcResp = await _qwen.ChatAsync(new List<ChatMessage>
                    {
                        new() { Role = "user", Content = rpcPrompt }
                    });

                    return new BrowserCheckResult
                    {
                        Success = true,
                        Analysis = rpcResp.Content ?? "AI 未返回分析结果"
                    };
                }
            }
            else
            {
                _logger.LogInformation("无配置路径，使用 AI 自动导航");
                var menuHtml = await GetPageLinksAsync(page);
                if (!string.IsNullOrEmpty(menuHtml))
                {
                    var navPrompt = $@"以下是摄像机管理页面中所有可点击的菜单项：
{menuHtml[..Math.Min(4000, menuHtml.Length)]}

请找到与 GB28181、国标、SIP、平台接入相关的菜单项。只返回 JSON：
[{{""selector"": ""CSS选择器"", ""description"": ""说明""}}]";

                    var navResp = await _qwen.ChatAsync(new List<ChatMessage>
                    {
                        new() { Role = "user", Content = navPrompt }
                    });
                    await AiAssistedNavigateAsync(page, navResp.Content ?? "");
                }
            }

            // ========== 第三步：提取配置 ==========
            // 策略0（最优）：主动调用大华 RPC2 接口获取 GBT28181 配置
            var dahuaRpcConfig = "";
            if (primaryNavigationPath is { Count: > 0 })
            {
                dahuaRpcConfig = await TryFetchDahuaGBConfigViaRpc2Async(page, ip, port);

                if (string.IsNullOrEmpty(dahuaRpcConfig))
                {
                    // 兜底：重新点击最后一个导航步骤触发页面加载
                    var lastStep = primaryNavigationPath.LastOrDefault();
                    if (!string.IsNullOrEmpty(lastStep))
                    {
                        var el = await FindClickableByTextAsync(page, lastStep);
                        if (el != null)
                        {
                            await el.ClickAsync();
                            _logger.LogInformation("重新点击 '{Step}' 以触发 RPC2 数据加载", lastStep);
                        }
                    }
                    await Task.Delay(5000);
                }
            }

            // 策略1：遍历所有 frame，提取 input 的 value
            var configHtml = "";
            var inputValues = new Dictionary<string, string>();

            _logger.LogInformation("开始遍历 {Count} 个 frame 查找配置内容", page.Frames.Count);
            foreach (var frame in page.Frames)
            {
                try
                {
                    var frameUrl = frame.Url;
                    _logger.LogInformation("检查 Frame: URL={Url}, Name={Name}", frameUrl, frame.Name);

                    // 提取所有 input 的 name/id 和 value
                    var frameInputs = await frame.EvaluateAsync<JsonElement>(@"() => {
                        const result = {};
                        const inputs = document.querySelectorAll('input, select, textarea');
                        inputs.forEach(el => {
                            const key = el.name || el.id || el.getAttribute('data-field') || '';
                            const val = el.value || el.textContent || '';
                            if (key && val) result[key] = val;
                        });
                        // 也提取 span/td 中的配置值（某些厂商用只读文本显示）
                        const labels = document.querySelectorAll('td, span, label, div');
                        labels.forEach(el => {
                            const text = (el.textContent || '').trim();
                            if (text.includes(':') && text.length < 200) {
                                const parts = text.split(':');
                                if (parts.length === 2) result['label_' + parts[0].trim()] = parts[1].trim();
                            }
                        });
                        return result;
                    }");

                    var inputCount = 0;
                    foreach (var prop in frameInputs.EnumerateObject())
                    {
                        var val = prop.Value.GetString() ?? "";
                        if (!string.IsNullOrEmpty(val))
                        {
                            inputValues[prop.Name] = val;
                            inputCount++;
                        }
                    }
                    _logger.LogInformation("Frame {Url}: 提取到 {Count} 个 input 值", frameUrl, inputCount);

                    // 策略2：innerHTML 关键词匹配（兜底）
                    var html = await frame.EvaluateAsync<string>(@"() => {
                        return document.body ? document.body.innerHTML.substring(0, 8000) : '';
                    }");
                    if (string.IsNullOrEmpty(html)) continue;

                    var hasSipKeywords = html.Contains("SIP") || html.Contains("sip") ||
                        html.Contains("28181") || html.Contains("国标") || html.Contains("平台接入") ||
                        html.Contains("服务器编号") || html.Contains("Server");

                    _logger.LogDebug("Frame {Url}: HTML长度={Len}, 含SIP关键词={Has}", frameUrl, html.Length, hasSipKeywords);

                    if (hasSipKeywords)
                    {
                        configHtml = html;
                        _logger.LogInformation("从 frame {Url} 获取到含国标关键词的 HTML，长度: {Len}", frameUrl, html.Length);
                        break;
                    }
                    else if (string.IsNullOrEmpty(configHtml))
                    {
                        configHtml = html;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Frame 访问失败: {Msg}", ex.Message);
                }
            }

            // ========== 汇总配置信息（优先级：主动RPC2 > 拦截RPC2 > input > HTML）==========
            var configSummary = "";
            if (!string.IsNullOrEmpty(dahuaRpcConfig))
            {
                configSummary = $"[大华 RPC2 主动获取的 GBT28181 配置]\n{dahuaRpcConfig[..Math.Min(6000, dahuaRpcConfig.Length)]}";
                _logger.LogInformation("使用主动 RPC2 调用获取的国标配置数据");
            }
            else if (rpcIntercepted && !string.IsNullOrEmpty(rpcConfigJson))
            {
                configSummary = $"[RPC2 拦截数据]\n{rpcConfigJson[..Math.Min(6000, rpcConfigJson.Length)]}";
                _logger.LogInformation("使用 RPC2 拦截数据作为主要配置来源");
            }
            else if (rpcPayloads.Count > 0)
            {
                // 把所有拦截到的 RPC2 响应都送给 AI 分析
                var rpcSummary = string.Join("\n\n---\n\n",
                    rpcPayloads.Take(5).Select(p => p[..Math.Min(2000, p.Length)]));
                configSummary = $"[RPC2 拦截的所有相关响应 - 共 {rpcPayloads.Count} 条]\n{rpcSummary}";
                _logger.LogInformation("使用 {Count} 条 RPC2 拦截响应作为配置来源", rpcPayloads.Count);
            }
            else if (inputValues.Count > 0)
            {
                var inputSummary = string.Join("\n", inputValues.Select(kv => $"  {kv.Key} = {kv.Value}"));
                configSummary = $"[Input 表单值 - 共 {inputValues.Count} 项]\n{inputSummary}";
                if (!string.IsNullOrEmpty(configHtml))
                    configSummary += $"\n\n[页面 HTML 片段]\n{configHtml[..Math.Min(3000, configHtml.Length)]}";
                _logger.LogInformation("使用 input 表单值作为主要配置来源，共 {Count} 项", inputValues.Count);
            }
            else
            {
                configSummary = configHtml ?? "";
                _logger.LogWarning("未能通过 RPC2 或 input 提取配置，使用 HTML 兜底");
            }

            if (string.IsNullOrEmpty(configSummary) || (!configSummary.Contains("SIP") && !configSummary.Contains("28181") && inputValues.Count == 0))
                _logger.LogWarning("未在任何 frame 中找到国标/SIP 配置内容，可能导航未到达配置页或内容在动态加载的 iframe 中");

            // ========== 第四步：AI 对比分析 ==========
            // 判断是否拿到了有效的配置数据
            var hasValidConfig = !string.IsNullOrEmpty(configSummary) &&
                (configSummary.Contains("SIP") || configSummary.Contains("28181") ||
                 configSummary.Contains("GBT28181") || configSummary.Contains("ServerID") ||
                 configSummary.Contains("Enable") || inputValues.Count > 3);

            ChatResponse configResp;

            if (!hasValidConfig)
            {
                // DOM/RPC2 都没拿到有效数据，fallback 到截图 + 视觉模型分析
                _logger.LogInformation("DOM/RPC2 未获取到有效国标配置，切换到截图分析模式");

                try
                {
                    // 截图当前页面（包含所有 frame 的完整渲染结果）
                    var screenshotBytes = await page.ScreenshotAsync(new() { FullPage = true });
                    var screenshotBase64 = Convert.ToBase64String(screenshotBytes);
                    _logger.LogInformation("截图成功，大小: {Size} bytes", screenshotBytes.Length);

                    var visionPrompt = $@"这是一台摄像机的国标(GB28181)平台接入配置页面的截图。
请仔细查看截图中的所有配置项，提取以下信息：
1. SIP 服务器 IP 地址
2. SIP 服务器端口
3. 服务器编码/Server ID
4. 设备编码/Device ID
5. SIP 域/Realm
6. 是否启用国标接入
7. 其他你能看到的国标/SIP 相关配置

然后与以下期望配置对比：
- 期望 SIP 服务器 IP: {expectedSipServerIp}
- 期望服务器编码(Server ID): {expectedServerId}

用中文回答，先说结论（配置是否匹配），再逐项列出从截图中读取到的值和对比结果。";

                    configResp = await _qwen.ChatWithImageAsync(visionPrompt, screenshotBase64);
                    _logger.LogInformation("视觉模型分析完成");
                }
                catch (Exception visionEx)
                {
                    _logger.LogWarning("视觉模型分析失败: {Msg}，回退到文本模式", visionEx.Message);
                    // 视觉模型也失败了，用文本模型兜底
                    var fallbackPrompt = $@"以下是摄像机配置页面提取到的信息（可能不完整）：
{configSummary[..Math.Min(6000, configSummary.Length)]}

我们平台数据库中该设备的期望配置：
- SIP 服务器 IP: {expectedSipServerIp}
- 服务器编码(Server ID): {expectedServerId}

请分析上述信息，尝试提取国标/SIP 配置并与期望值对比。
如果信息不足无法判断，请明确说明。用中文回答。";

                    configResp = await _qwen.ChatAsync(new List<ChatMessage>
                    {
                        new() { Role = "user", Content = fallbackPrompt }
                    });
                }
            }
            else
            {
                // DOM/RPC2 拿到了有效数据，直接用文本模型分析
                var comparePrompt = $@"以下是摄像机配置页面提取到的信息：
{configSummary[..Math.Min(6000, configSummary.Length)]}

我们平台数据库中该设备的期望配置：
- SIP 服务器 IP: {expectedSipServerIp}
- 服务器编码(Server ID): {expectedServerId}

请：
1. 从上述信息中提取摄像机当前的国标/SIP 相关配置值
2. 逐项与期望配置对比
3. 给出结论

用中文回答，先说结论，再给详细分析。";

                configResp = await _qwen.ChatAsync(new List<ChatMessage>
                {
                    new() { Role = "user", Content = comparePrompt }
                });
            }

            return new BrowserCheckResult
            {
                Success = true,
                Analysis = configResp.Content ?? "AI 未返回分析结果"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI-DOM 模式检查失败: {Ip}:{Port}", ip, port);
            return new BrowserCheckResult
            {
                Success = false,
                Analysis = $"AI-DOM 模式检查失败: {ex.Message}"
            };
        }
        finally
        {
            if (browser != null) await browser.DisposeAsync();
            pw?.Dispose();
        }
    }

    /// <summary>
    /// 浏览器登录成功后，直接在页面上下文中用 fetch 调 RPC2 获取国标配置
    /// 优点：自动复用浏览器的 session/cookie，不需要自己算密码哈希
    /// </summary>
    private async Task<string> FetchGBConfigViaPageAsync(IPage page)
    {
        var configNames = new[] { "GBT28181", "GB28181", "T28181", "PlatformAccess" };

        foreach (var name in configNames)
        {
            try
            {
                _logger.LogInformation("浏览器上下文 RPC2: 尝试获取 {Name} 配置", name);

                var result = await page.EvaluateAsync<string>($@"async () => {{
                    try {{
                        const resp = await fetch('/RPC2', {{
                            method: 'POST',
                            headers: {{ 'Content-Type': 'application/json' }},
                            body: JSON.stringify({{
                                method: 'configManager.getConfig',
                                params: {{ name: '{name}' }},
                                id: 10
                            }})
                        }});
                        const text = await resp.text();
                        return text;
                    }} catch(e) {{
                        return 'ERROR:' + e.message;
                    }}
                }}");

                if (string.IsNullOrEmpty(result) || result.StartsWith("ERROR:"))
                {
                    _logger.LogInformation("RPC2 getConfig({Name}) 失败: {Result}", name, result);
                    continue;
                }

                // 检查是否返回了有效数据（result:true 表示成功）
                if (result.Contains("\"result\":true") || result.Contains("\"result\": true"))
                {
                    _logger.LogInformation("浏览器上下文 RPC2 获取到 {Name} 配置，长度: {Len}", name, result.Length);
                    return result;
                }

                // 大华有些固件返回 result:false 但 params 里有数据，也算成功
                if (result.Contains("\"params\"") && result.Length > 200)
                {
                    _logger.LogInformation("浏览器上下文 RPC2 {Name} 返回了 params 数据（result 非 true），长度: {Len}", name, result.Length);
                    return result;
                }

                _logger.LogInformation("RPC2 getConfig({Name}) 返回（未匹配成功条件）: {Result}", name, result[..Math.Min(300, result.Length)]);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("RPC2 getConfig({Name}) 异常: {Msg}", name, ex.Message);
            }
        }

        return "";
    }

    /// <summary>
    /// 主动调用大华 RPC2 接口获取 GBT28181 国标配置
    /// 大华摄像机的配置数据不在 DOM 中，而是通过 RPC2 JSON 接口动态加载
    /// </summary>
    private async Task<string> TryFetchDahuaGBConfigViaRpc2Async(IPage page, string ip, int port)
    {
        try
        {
            // 通过 page.evaluate 在浏览器上下文中发 RPC2 请求（自动带 cookie/session）
            var configNames = new[] { "GBT28181", "GB28181", "T28181", "PlatformAccess", "SIPServer" };

            foreach (var configName in configNames)
            {
                _logger.LogInformation("尝试通过 RPC2 获取配置: {Name}", configName);

                var result = await page.EvaluateAsync<string>($@"async () => {{
                    try {{
                        const resp = await fetch('/RPC2', {{
                            method: 'POST',
                            headers: {{ 'Content-Type': 'application/json' }},
                            body: JSON.stringify({{
                                method: 'configManager.getConfig',
                                params: {{ name: '{configName}' }},
                                id: 1
                            }})
                        }});
                        return await resp.text();
                    }} catch(e) {{
                        return 'ERROR:' + e.message;
                    }}
                }}");

                if (string.IsNullOrEmpty(result) || result.StartsWith("ERROR:"))
                {
                    _logger.LogDebug("RPC2 getConfig({Name}) 失败: {Result}", configName, result);
                    continue;
                }

                _logger.LogInformation("RPC2 getConfig({Name}) 响应长度: {Len}", configName, result.Length);

                // 检查是否是错误响应
                if (result.Contains("\"error\"") && !result.Contains("SIP") && !result.Contains("Enable"))
                {
                    _logger.LogDebug("RPC2 getConfig({Name}) 返回错误响应，跳过", configName);
                    continue;
                }

                _logger.LogInformation("通过 RPC2 主动获取到 {Name} 配置数据，长度: {Len}", configName, result.Length);
                return result;
            }

            // 兜底：尝试获取 Network 配置看是否包含国标信息
            var defaultResult = await page.EvaluateAsync<string>(@"async () => {
                try {
                    const resp = await fetch('/RPC2', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            method: 'configManager.getConfig',
                            params: { name: 'Network' },
                            id: 2
                        })
                    });
                    return await resp.text();
                } catch(e) {
                    return 'ERROR:' + e.message;
                }
            }");

            if (!string.IsNullOrEmpty(defaultResult) && !defaultResult.StartsWith("ERROR:") &&
                (defaultResult.Contains("SIP") || defaultResult.Contains("28181")))
            {
                _logger.LogInformation("从 Network 配置中发现国标相关数据，长度: {Len}", defaultResult.Length);
                return defaultResult;
            }

            _logger.LogWarning("所有 RPC2 配置名称均未获取到国标配置数据");
            return "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("主动 RPC2 调用失败: {Msg}", ex.Message);
            return "";
        }
    }

    private async Task AiAssistedLoginAsync(IPage page, JsonElement selectors, string username, string password)
    {
        try
        {
            if (selectors.TryGetProperty("usernameSelector", out var uSel))
            {
                var el = await page.QuerySelectorAsync(uSel.GetString() ?? "");
                if (el != null) { await el.FillAsync(username); _logger.LogInformation("AI 定位用户名框成功"); }
            }
            if (selectors.TryGetProperty("passwordSelector", out var pSel))
            {
                var el = await page.QuerySelectorAsync(pSel.GetString() ?? "");
                if (el != null) { await el.FillAsync(password); _logger.LogInformation("AI 定位密码框成功"); }
            }
            if (selectors.TryGetProperty("submitSelector", out var sSel))
            {
                var el = await page.QuerySelectorAsync(sSel.GetString() ?? "");
                if (el != null) { await el.ClickAsync(); _logger.LogInformation("AI 定位登录按钮成功"); }
            }
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);
            _logger.LogInformation("登录 {Url} 设备成功", page.Url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("AI 辅助登录失败: {Msg}", ex.Message);
        }
    }

    private async Task AiAssistedNavigateAsync(IPage page, string aiResponse)
    {
        try
        {
            var json = ParseJsonArrayFromAiResponse(aiResponse);
            if (json == null || json.Value.GetArrayLength() == 0)
            {
                _logger.LogWarning("AI 未找到国标配置页面链接");
                return;
            }
            foreach (var item in json.Value.EnumerateArray())
            {
                if (item.TryGetProperty("selector", out var sel))
                {
                    var el = await page.QuerySelectorAsync(sel.GetString() ?? "");
                    if (el != null)
                    {
                        await el.ClickAsync();
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        await Task.Delay(1000);
                        var desc = item.TryGetProperty("description", out var d) ? d.GetString() : "未知";
                        _logger.LogInformation("AI 导航点击: {Desc}", desc);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("AI 辅助导航失败: {Msg}", ex.Message);
        }
    }

    private JsonElement? ParseJsonFromAiResponse(string text)
    {
        try
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var jsonStr = text[start..(end + 1)];
                return JsonSerializer.Deserialize<JsonElement>(jsonStr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("解析 AI JSON 响应失败: {Msg}", ex.Message);
        }
        return null;
    }

    private JsonElement? ParseJsonArrayFromAiResponse(string text)
    {
        try
        {
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                var jsonStr = text[start..(end + 1)];
                return JsonSerializer.Deserialize<JsonElement>(jsonStr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("解析 AI JSON 数组响应失败: {Msg}", ex.Message);
        }
        return null;
    }

    private static bool HasRelevantKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var keywords = new[]
        {
            "sip", "28181", "gb28181", "gbt28181", "国标", "平台接入", "平台",
            "server", "serverid", "server ip", "device id", "服务器", "服务器编号",
            "设备编码", "设备id", "realm", "domain", "端口", "认证"
        };
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 智能匹配登录（AI 失败时的兜底）
    /// </summary>
    private async Task SmartLoginAsync(IPage page, string username, string password)
    {
        var allInputs = await page.QuerySelectorAllAsync("input:visible");
        IElementHandle? userInput = null, passInput = null;

        foreach (var input in allInputs)
        {
            var type = (await input.GetAttributeAsync("type") ?? "text").ToLower();
            if (type == "password") passInput = input;
            else if (type == "text" || type == "") userInput ??= input;
        }

        if (userInput != null) await userInput.FillAsync(username);
        if (passInput != null) await passInput.FillAsync(password);

        var btn = await page.QuerySelectorAsync("button[type='submit'], input[type='submit'], button:has-text('登录'), button:has-text('Login')");
        if (btn != null) await btn.ClickAsync();
        else if (passInput != null) await passInput.PressAsync("Enter");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000);
    }

    /// <summary>
    /// 按配置路径导航（如 "设置->网络设置->平台接入"）
    /// </summary>
    private async Task NavigateByPathAsync(IPage page, string navPath)
    {
        var steps = navPath.Split("->").Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        await NavigateByPathAsync(page, steps);
    }

    private async Task NavigateByPathAsync(IPage page, IReadOnlyList<string> steps)
    {
        foreach (var step in steps)
        {
            _logger.LogInformation("路径导航: 查找 '{Step}'", step);
            var el = await FindClickableByTextAsync(page, step);
            if (el != null)
            {
                await el.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await Task.Delay(1500);
                _logger.LogInformation("路径导航: 已点击 '{Step}'", step);
            }
            else
            {
                _logger.LogWarning("路径导航: 未找到 '{Step}'", step);
            }
        }
        _logger.LogInformation("获取 {Path} 页面内容成功", string.Join(" -> ", steps));
    }

    /// <summary>
    /// 在主页面和所有 iframe 中查找包含指定文字的可点击元素
    /// </summary>
    private async Task<IElementHandle?> FindClickableByTextAsync(IPage page, string text)
    {
        var selectors = new[]
        {
            $"a:has-text('{text}')", $"span:has-text('{text}')", $"td:has-text('{text}')",
            $"div:has-text('{text}')", $"li:has-text('{text}')", $"button:has-text('{text}')"
        };

        foreach (var sel in selectors)
        {
            var el = await page.QuerySelectorAsync(sel);
            if (el != null) return el;
        }

        foreach (var frame in page.Frames)
        {
            if (frame == page.MainFrame) continue;
            foreach (var sel in selectors)
            {
                try
                {
                    var el = await frame.QuerySelectorAsync(sel);
                    if (el != null) return el;
                }
                catch { }
            }
        }
        return null;
    }

    private async Task<string> GetPageLinksAsync(IPage page)
    {
        return await page.EvaluateAsync<string>(@"() => {
            const links = document.querySelectorAll('a, span[onclick], div[onclick], li[onclick], td[onclick]');
            const items = [];
            links.forEach(el => {
                const text = (el.textContent || '').trim();
                if (text && text.length < 50) {
                    const id = el.id ? '#' + el.id : '';
                    const cls = el.className ? '.' + el.className.split(' ')[0] : '';
                    items.push(el.tagName.toLowerCase() + id + cls + ' => ' + text);
                }
            });
            return items.join('\n').substring(0, 5000);
        }") ?? "";
    }

    /// <summary>
    /// DOM 模式：按页面可见字段提取国标配置
    /// </summary>
    public async Task<BrowserCheckResult> CheckCameraConfigByDomAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId, string? manufacturer = null)
    {
        IPlaywright? pw = null;
        IBrowser? browser = null;
        try
        {
            (pw, browser, var page) = await LaunchBrowserAsync(ip, port);
            var navigationPaths = VisibleFieldNavigationResolver.Resolve(_manufacturerNavigationOptions, manufacturer);

            if (!string.IsNullOrEmpty(username))
                await SmartLoginAsync(page, username, password ?? "");

            var extraction = await TryExtractVisibleFieldConfigAsync(page, navigationPaths);
            if (!extraction.IsSuccess)
            {
                return new BrowserCheckResult
                {
                    Success = false,
                    Analysis = $"DOM 模式未命中有效国标配置页: {extraction.FailureReason}"
                };
            }

            return new BrowserCheckResult
            {
                Success = true,
                Analysis = AnalyzeVisibleConfig(extraction.Config, expectedSipServerIp, expectedServerId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DOM 模式检查失败: {Ip}:{Port}", ip, port);
            return new BrowserCheckResult { Success = false, Analysis = $"DOM 模式检查失败: {ex.Message}" };
        }
        finally
        {
            if (browser != null) await browser.DisposeAsync();
            pw?.Dispose();
        }
    }

    private async Task<VisibleFieldExtractionResult> TryExtractVisibleFieldConfigAsync(
        IPage page,
        IReadOnlyList<IReadOnlyList<string>> navigationPaths)
    {
        var bestResult = await EvaluateVisibleFieldPagesAsync(page);
        if (bestResult.IsSuccess)
        {
            return bestResult;
        }

        foreach (var navigationPath in navigationPaths)
        {
            await NavigateByPathAsync(page, navigationPath);
            await Task.Delay(1500);

            var navigationResult = await EvaluateVisibleFieldPagesAsync(page);
            if (navigationResult.IsSuccess)
            {
                return navigationResult;
            }

            if (navigationResult.MatchedFields > bestResult.MatchedFields)
            {
                bestResult = navigationResult;
            }
        }

        if (bestResult.MatchedFields == 0 && string.IsNullOrWhiteSpace(bestResult.FailureReason))
        {
            bestResult.FailureReason = "未找到可见字段配置页";
        }

        return bestResult;
    }

    private async Task<VisibleFieldExtractionResult> EvaluateVisibleFieldPagesAsync(IPage page)
    {
        VisibleFieldExtractionResult? bestResult = null;

        void EvaluateHtml(string html, string source)
        {
            var result = VisibleFieldPageDetector.Evaluate(_visibleFieldExtractor, html);
            if (result.IsSuccess)
            {
                result.FailureReason = "";
                _logger.LogInformation("可见字段命中有效配置页: {Source}, 字段数={Count}", source, result.MatchedFields);
                bestResult = result;
                return;
            }

            if (bestResult == null || result.MatchedFields > bestResult.MatchedFields)
            {
                bestResult = result;
                _logger.LogDebug("可见字段候选页: {Source}, 字段数={Count}", source, result.MatchedFields);
            }
        }

        EvaluateHtml(await page.ContentAsync(), "main");
        if (bestResult?.IsSuccess == true)
        {
            return bestResult;
        }

        foreach (var frame in page.Frames)
        {
            if (frame == page.MainFrame)
            {
                continue;
            }

            try
            {
                EvaluateHtml(await frame.ContentAsync(), frame.Url);
                if (bestResult?.IsSuccess == true)
                {
                    return bestResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("读取 Frame HTML 失败: {Url}, {Msg}", frame.Url, ex.Message);
            }
        }

        return bestResult ?? new VisibleFieldExtractionResult
        {
            IsSuccess = false,
            FailureReason = "未找到可见字段配置页"
        };
    }

    private string AnalyzeVisibleConfig(VisibleGbConfig config, string expectedSipIp, string expectedServerId)
    {
        static string MatchText(bool matched) => matched ? "匹配" : "不匹配";

        var sipIpMatched = !string.IsNullOrWhiteSpace(config.SipServerIp) &&
            string.Equals(config.SipServerIp, expectedSipIp, StringComparison.OrdinalIgnoreCase);
        var serverIdMatched = !string.IsNullOrWhiteSpace(config.SipServerId) &&
            string.Equals(config.SipServerId, expectedServerId, StringComparison.OrdinalIgnoreCase);

        var lines = new List<string>
        {
            sipIpMatched && serverIdMatched ? "结论: 国标关键配置匹配" : "结论: 国标关键配置存在差异",
            $"SIP服务器IP: 当前={DisplayValue(config.SipServerIp)}，期望={DisplayValue(expectedSipIp)}，结果={MatchText(sipIpMatched)}",
            $"SIP服务器编号: 当前={DisplayValue(config.SipServerId)}，期望={DisplayValue(expectedServerId)}，结果={MatchText(serverIdMatched)}"
        };

        if (config.Enable.HasValue)
        {
            lines.Add($"接入使能: {(config.Enable.Value ? "启用" : "未启用")}");
        }

        AddLineIfPresent(lines, "SIP域", config.SipDomain);
        AddLineIfPresent(lines, "SIP服务器端口", config.SipServerPort);
        AddLineIfPresent(lines, "本地SIP端口", config.LocalSipPort);
        AddLineIfPresent(lines, "注册有效期", config.RegisterExpiry);
        AddLineIfPresent(lines, "心跳周期", config.Heartbeat);
        AddLineIfPresent(lines, "最大心跳超时次数", config.HeartbeatTimeoutCount);
        AddLineIfPresent(lines, "设备编号", config.DeviceId);
        AddLineIfPresent(lines, "通道编号", config.ChannelId);

        return string.Join("\n", lines);
    }

    private static void AddLineIfPresent(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    private static string DisplayValue(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(空)" : value;

    /// <summary>
    /// 截图模式（保留）
    /// </summary>
    public async Task<BrowserCheckResult> CheckCameraConfigByScreenshotAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId)
    {
        var screenshotPath = Path.Combine("screenshots", DateTime.Now.ToString("yyyyMMdd"), $"{ip}_{DateTime.Now:HHmmss}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        IPlaywright? pw = null;
        IBrowser? browser = null;
        try
        {
            (pw, browser, var page) = await LaunchBrowserAsync(ip, port);
            await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
            return new BrowserCheckResult { Success = true, ScreenshotPath = screenshotPath, Analysis = "截图已保存，需要视觉模型分析" };
        }
        catch (Exception ex)
        {
            return new BrowserCheckResult { Success = false, ScreenshotPath = screenshotPath, Analysis = $"截图失败: {ex.Message}" };
        }
        finally
        {
            if (browser != null) await browser.DisposeAsync();
            pw?.Dispose();
        }
    }
}

public class BrowserCheckResult
{
    public bool Success { get; set; }
    public string ScreenshotPath { get; set; } = "";
    public string Analysis { get; set; } = "";
}
