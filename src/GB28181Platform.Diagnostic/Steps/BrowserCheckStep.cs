using GB28181Platform.Diagnostic.Browser;
using GB28181Platform.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace GB28181Platform.Diagnostic.Steps;

public class BrowserCheckStep : IDiagnosticStep
{
    private readonly CameraBrowserAgent _agent;
    private readonly IConfiguration _config;

    public BrowserCheckStep(CameraBrowserAgent agent, IConfiguration config)
    {
        _agent = agent;
        _config = config;
    }

    public string StepName => "浏览器配置检查";
    public DiagnosticStepType StepType => DiagnosticStepType.BrowserCheck;

    public async Task<StepResult> ExecuteAsync(DiagnosticContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var expectedSipIp = _config["SipServer:ListenIp"] ?? "0.0.0.0";
        var expectedServerId = _config["SipServer:ServerId"] ?? "";
        var mode = (_config["Diagnostic:BrowserCheckMode"] ?? "ai-dom").ToLower();

        BrowserCheckResult result = mode switch
        {
            "screenshot" => await _agent.CheckCameraConfigByScreenshotAsync(
                context.IpAddress, context.WebPort, context.WebUsername, context.WebPassword,
                expectedSipIp, expectedServerId),
            "dom" => await _agent.CheckCameraConfigByDomAsync(
                context.IpAddress, context.WebPort, context.WebUsername, context.WebPassword,
                expectedSipIp, expectedServerId, context.Manufacturer),
            _ => await _agent.CheckCameraConfigByAiDomAsync(
                context.IpAddress, context.WebPort, context.WebUsername, context.WebPassword,
                expectedSipIp, expectedServerId, context.Manufacturer),
        };

        sw.Stop();
        return new StepResult
        {
            Success = result.Success,
            Detail = result.Analysis,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ScreenshotPath = result.ScreenshotPath,
            ContinueNext = false
        };
    }
}
