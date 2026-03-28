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
        _qwenHttp = new HttpClient
        {
            BaseAddress = new Uri(config["QwenApi:BaseUrl"] ?? "http://localhost:8000")
        };
    }

    public async Task<BrowserCheckResult> CheckCameraConfigAsync(
        string ip, int port, string? username, string? password,
        string expectedSipServerIp, string expectedServerId)
    {
        var screenshotDir = Path.Combine("screenshots", DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir, $"{ip}_{DateTime.Now:HHmmss}.png");

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();

            var url = $"http://{ip}:{port}";
            await page.GotoAsync(url, new() { Timeout = 15000 });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
            _logger.LogInformation("摄像机网页截图已保存: {Path}", screenshotPath);

            var analysis = await AnalyzeScreenshotAsync(screenshotPath, expectedSipServerIp, expectedServerId);

            return new BrowserCheckResult
            {
                Success = true,
                ScreenshotPath = screenshotPath,
                Analysis = analysis
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "浏览器检查失败: {Ip}:{Port}", ip, port);
            return new BrowserCheckResult
            {
                Success = false,
                ScreenshotPath = screenshotPath,
                Analysis = $"浏览器访问失败: {ex.Message}"
            };
        }
    }

    private async Task<string> AnalyzeScreenshotAsync(string screenshotPath, string expectedSipIp, string expectedServerId)
    {
        var imageBytes = await File.ReadAllBytesAsync(screenshotPath);
        var base64 = Convert.ToBase64String(imageBytes);

        var prompt = $@"这是一台摄像机的网页管理界面截图。请分析截图中是否能看到 GB28181/国标 相关配置。
如果能看到，请提取以下信息：
- SIP 服务器 IP
- SIP 服务器端口
- 设备编码
- SIP 域/realm

然后与期望配置对比：
- 期望 SIP 服务器 IP: {expectedSipIp}
- 期望服务器编码: {expectedServerId}

如果有差异请指出，如果看不到国标配置页面请说明。";

        var requestBody = new
        {
            model = _config["QwenApi:Model"] ?? "qwen-vl-plus",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64}" } },
                        new { type = "text", text = prompt }
                    }
                }
            },
            max_tokens = 1000
        };

        var apiKey = _config["QwenApi:ApiKey"] ?? "";
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(requestBody);

        var response = await _qwenHttp.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var content = json.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? "分析结果为空";
    }
}

public class BrowserCheckResult
{
    public bool Success { get; set; }
    public string ScreenshotPath { get; set; } = "";
    public string Analysis { get; set; } = "";
}
