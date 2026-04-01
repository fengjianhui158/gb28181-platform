namespace GB28181Platform.Diagnostic.Browser.Interactive;

public sealed record InteractiveNavigationStepResult(
    string Step,
    bool Success,
    string Selector,
    string MatchedText,
    string Source,
    string FailureReason = "");
