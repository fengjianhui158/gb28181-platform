using System.Net.Http.Json;
using System.Text.Json;
using GB28181Platform.AiAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GB28181Platform.Diagnostic.Browser;

public class CameraBrowserAgent
{
    private readonly IQwenClient _qwen;
    private readonly IConfiguration _config;
    private readonly ILogger<CameraBrowserAgent> _logger;

    public CameraBrowserAgent(IQwenClient qwen, IConfiguration config, ILogger<CameraBrowserAgent> logger)
    {
        _qwen = qwen;
        _config = config;
        _logger = logger;
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
        IPlaywright? pw = null;
        IBrowser? browser = null;
        try
        {
            (pw, browser, var page) = await LaunchBrowserAsync(ip, port);

            // 第一步：AI 辅助登录
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

            // 第二步：导航到国标配置页
            // 优先用厂商配置路径，没有则 AI 自动分析
            await Task.Delay(3000);

            var mfr = (manufacturer ?? "").ToLower();
            var navPath = _config[$"Diagnostic:NavPaths:{mfr}"];

            // 如果精确匹配不到，模糊匹配
            if (string.IsNullOrEmpty(navPath) && !string.IsNullOrEmpty(manufacturer))
            {
                var paths = _config.GetSection("Diagnostic:NavPaths").GetChildren();
                foreach (var p in paths)
                {
                    if (mfr.Contains(p.Key.ToLower()) || p.Key.ToLower().Contains(mfr))
                    {
                        navPath = p.Value;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(navPath))
            {
                _logger.LogInformation("使用配置路径导航: {Path}", navPath);
                await NavigateByPathAsync(page, navPath);
                // 大华等摄像机通过 RPC/AJAX 动态加载配置数据，需要等待
                await Task.Delay(5000);
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

            // 第三步：AI 提取配置并对比（遍历所有 frame，优先取含 SIP/国标关键词的）
            var configHtml = "";
            foreach (var frame in page.Frames)
            {
                try
                {
                    var html = await frame.EvaluateAsync<string>(@"() => {
                        return document.body ? document.body.innerHTML.substring(0, 8000) : '';
                    }");
                    if (string.IsNullOrEmpty(html)) continue;

                    // 优先选包含国标/SIP 关键词的 frame
                    var hasSipKeywords = html.Contains("SIP") || html.Contains("sip") ||
                        html.Contains("28181") || html.Contains("国标") || html.Contains("平台接入") ||
                        html.Contains("服务器编号") || html.Contains("Server");

                    _logger.LogDebug("Frame {Url}: 长度={Len}, 含SIP关键词={Has}", frame.Url, html.Length, hasSipKeywords);

                    if (hasSipKeywords)
                    {
                        configHtml = html;
                        _logger.LogInformation("从 frame {Url} 获取到国标配置 HTML，长度: {Len}", frame.Url, html.Length);
                        break; // 找到了就不再找
                    }
                    else if (string.IsNullOrEmpty(configHtml))
                    {
                        configHtml = html; // 兜底用第一个非空 frame
                    }
                }
                catch { /* iframe 可能不可访问 */ }
            }

            if (!configHtml.Contains("SIP") && !configHtml.Contains("28181"))
                _logger.LogWarning("未在任何 frame 中找到国标/SIP 配置内容，可能导航未到达配置页或内容在动态加载的 iframe 中");

            var comparePrompt = $@"以下是摄像机配置页面的 HTML：
{configHtml?[..Math.Min(6000, configHtml?.Length ?? 0)]}

我们平台数据库中该设备的期望配置：
- SIP 服务器 IP: {expectedSipServerIp}
- 服务器编码(Server ID): {expectedServerId}

请：
1. 从 HTML 中提取摄像机当前的国标/SIP 相关配置值
2. 逐项与期望配置对比
3. 给出结论

用中文回答，先说结论，再给详细分析。";

            var configResp = await _qwen.ChatAsync(new List<ChatMessage>
            {
                new() { Role = "user", Content = comparePrompt }
            });

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
            // AI 可能返回 ```json ... ``` 包裹的内容
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
        var steps = navPath.Split("->").Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s));
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
        _logger.LogInformation("获取 {Path} 页面内容成功", navPath);
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

        // 先在主页面找
        foreach (var sel in selectors)
        {
            var el = await page.QuerySelectorAsync(sel);
            if (el != null) return el;
        }

        // 再在每个 iframe 里找
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
                catch { /* iframe 可能不可访问 */ }
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

    private string GetManufacturerKey(string ip) => ""; // 后续可根据 Device.Manufacturer 传入

    /// <summary>
    /// DOM 模式：硬编码选择器（兜底）
    /// </summary>
    public async Task<BrowserCheckResult> CheckCameraConfigByDomAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId)
    {
        IPlaywright? pw = null;
        IBrowser? browser = null;
        try
        {
            (pw, browser, var page) = await LaunchBrowserAsync(ip, port);

            if (!string.IsNullOrEmpty(username))
                await SmartLoginAsync(page, username, password ?? "");

            var configData = await ExtractPageConfigAsync(page);
            var analysis = AnalyzeConfig(configData, expectedSipServerIp, expectedServerId);
            return new BrowserCheckResult { Success = true, Analysis = analysis };
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

    private async Task<Dictionary<string, string>> ExtractPageConfigAsync(IPage page)
    {
        var config = new Dictionary<string, string>();
        var inputs = await page.QuerySelectorAllAsync("input[type='text'], input[type='number']");
        foreach (var input in inputs)
        {
            var name = await input.GetAttributeAsync("name") ?? await input.GetAttributeAsync("id") ?? "";
            var value = await input.InputValueAsync();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                config[name] = value;
        }
        return config;
    }

    private string AnalyzeConfig(Dictionary<string, string> config, string expectedSipIp, string expectedServerId)
    {
        var lines = new List<string> { $"=== DOM 模式 === 提取到 {config.Count} 个配置项:" };
        foreach (var (k, v) in config) lines.Add($"  {k} = {v}");
        if (config.Count == 0) lines.Add("未能提取到配置信息");
        return string.Join("\n", lines);
    }

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
