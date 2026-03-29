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
        string expectedSipServerIp, string expectedServerId)
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

            // 第二步：AI 辅助导航到国标配置页
            // 大华等摄像机登录后通过 JS/iframe 动态加载，需要多等一会
            await Task.Delay(3000);

            // 获取完整页面内容（包括 iframe 内容和所有可点击元素）
            var menuHtml = await page.EvaluateAsync<string>(@"() => {
                // 先尝试获取所有 a 标签和可点击元素的文本
                const links = document.querySelectorAll('a, span[onclick], div[onclick], li[onclick], td[onclick], [class*=menu], [class*=tree], [class*=nav]');
                const items = [];
                links.forEach(el => {
                    const text = (el.textContent || '').trim();
                    if (text && text.length < 50) {
                        const id = el.id ? '#' + el.id : '';
                        const cls = el.className ? '.' + el.className.split(' ')[0] : '';
                        const tag = el.tagName.toLowerCase();
                        items.push(tag + id + cls + ' => ' + text);
                    }
                });
                // 也获取 iframe 列表
                const iframes = document.querySelectorAll('iframe');
                iframes.forEach((f, i) => items.push('iframe[' + i + '] src=' + f.src));
                return items.join('\n').substring(0, 5000);
            }");

            _logger.LogInformation("页面菜单/链接信息长度: {Len}", menuHtml?.Length ?? 0);

            if (!string.IsNullOrEmpty(menuHtml))
            {
                var navPrompt = $@"以下是摄像机管理页面中所有可点击的菜单项和链接（格式：元素选择器 => 文本内容）：
{menuHtml[..Math.Min(4000, menuHtml.Length)]}

请找到与 GB28181、国标、SIP、平台接入、网络配置 相关的菜单项。
大华摄像机通常在 网络设置 > 平台接入 或 网络 > GB28181 下。
如果需要多级导航（先点一级菜单再点二级），返回数组。只返回 JSON：
[{{""selector"": ""CSS选择器"", ""description"": ""说明""}}]
如果找不到，返回空数组 []";

                var navResp = await _qwen.ChatAsync(new List<ChatMessage>
                {
                    new() { Role = "user", Content = navPrompt }
                });

                await AiAssistedNavigateAsync(page, navResp.Content ?? "");
            }

            // 第三步：AI 提取配置并对比
            var configHtml = await page.EvaluateAsync<string>(@"() => {
                const form = document.querySelector('form, table, .config, [class*=config], [class*=setting], [class*=sip], [class*=gb28181]');
                if (form) return form.outerHTML.substring(0, 8000);
                return document.body.innerHTML.substring(0, 8000);
            }");

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
            _logger.LogInformation("AI 辅助登录完成，当前 URL: {Url}", page.Url);
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
