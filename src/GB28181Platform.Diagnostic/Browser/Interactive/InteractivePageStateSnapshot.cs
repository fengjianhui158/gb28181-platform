namespace GB28181Platform.Diagnostic.Browser.Interactive;

public sealed record InteractivePageStateSnapshot(
    string UrlPath,
    string VisibleText,
    IReadOnlyList<string> VisibleTokens);
