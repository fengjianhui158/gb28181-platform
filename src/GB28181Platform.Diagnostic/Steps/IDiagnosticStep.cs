using GB28181Platform.Domain.Enums;

namespace GB28181Platform.Diagnostic.Steps;

public interface IDiagnosticStep
{
    string StepName { get; }
    DiagnosticStepType StepType { get; }
    Task<StepResult> ExecuteAsync(DiagnosticContext context);
}

public class DiagnosticContext
{
    public string DeviceId { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int WebPort { get; set; } = 80;
    public string? WebUsername { get; set; }
    public string? WebPassword { get; set; }
    public string? Manufacturer { get; set; }
    public int TaskId { get; set; }
    public bool ShouldContinue { get; set; } = true;
}

public class StepResult
{
    public bool Success { get; set; }
    public string Detail { get; set; } = "";
    public int DurationMs { get; set; }
    public string? ScreenshotPath { get; set; }
    public bool ContinueNext { get; set; } = true;
}
