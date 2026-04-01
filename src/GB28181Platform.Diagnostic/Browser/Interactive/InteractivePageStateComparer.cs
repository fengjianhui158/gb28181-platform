namespace GB28181Platform.Diagnostic.Browser.Interactive;

public static class InteractivePageStateComparer
{
    public static bool HasMeaningfulChange(
        InteractivePageStateSnapshot before,
        InteractivePageStateSnapshot after)
    {
        if (!string.Equals(before.UrlPath, after.UrlPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(before.VisibleText, after.VisibleText, StringComparison.Ordinal))
        {
            return true;
        }

        return before.VisibleTokens.Count != after.VisibleTokens.Count;
    }
}
