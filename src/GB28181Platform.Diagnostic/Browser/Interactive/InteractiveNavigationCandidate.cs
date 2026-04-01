namespace GB28181Platform.Diagnostic.Browser.Interactive;

public sealed record InteractiveNavigationCandidate(
    string TagName,
    int Index,
    string Text,
    bool IsVisible,
    bool IsSemanticMenu,
    int Priority,
    double Top,
    double Area,
    int NavigationContextScore,
    bool HasPointerCursor,
    bool HasNoiseKeywords,
    int LineCount)
{
    public static InteractiveNavigationCandidate? SelectBest(
        IEnumerable<InteractiveNavigationCandidate> candidates,
        string expectedText)
    {
        var normalizedExpected = Normalize(expectedText);

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
            // 关键：只接受文本精确匹配(rank 0)或包含匹配(rank 1)
            // rank 2 = 完全不匹配，例如 has-text 匹配到隐藏子元素的容器
            .Where(candidate => MatchRank(candidate.Text, normalizedExpected) <= 1)
            .OrderByDescending(candidate => candidate.IsVisible)
            .ThenBy(candidate => MatchRank(candidate.Text, normalizedExpected))
            .ThenByDescending(candidate => candidate.NavigationContextScore)
            .ThenByDescending(candidate => candidate.HasPointerCursor)
            .ThenBy(candidate => candidate.HasNoiseKeywords ? 1 : 0)
            .ThenBy(candidate => candidate.LineCount)
            .ThenBy(candidate => TopBand(candidate.Top))
            .ThenByDescending(candidate => candidate.IsSemanticMenu)
            .ThenBy(candidate => TagPenalty(candidate.TagName))
            .ThenBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Text.Length)
            .ThenBy(candidate => candidate.Area)
            .FirstOrDefault();
    }

    private static int MatchRank(string text, string expectedText)
    {
        var normalizedText = Normalize(text);
        if (string.Equals(normalizedText, expectedText, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalizedText.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string Normalize(string value) =>
        string.Concat((value ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch)));

    private static int TopBand(double top)
    {
        if (top <= 0)
        {
            return 2;
        }

        if (top <= 140)
        {
            return 0;
        }

        if (top <= 260)
        {
            return 1;
        }

        return 2;
    }

    private static int TagPenalty(string tagName)
    {
        var normalized = (tagName ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "button" or "a" or "li" or "input" => 0,
            "td" or "span" => 1,
            "div" => 2,
            _ when normalized.StartsWith(":", StringComparison.Ordinal) => 0,
            _ => 1
        };
    }
}
