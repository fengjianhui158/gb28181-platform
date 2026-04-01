namespace GB28181Platform.Diagnostic.Browser;

public sealed record ClickableTextCandidate(
    string Selector,
    int Index,
    string Text,
    bool IsVisible,
    int Priority);

public static class ClickableTextCandidateSelector
{
    public static ClickableTextCandidate? SelectBest(
        IEnumerable<ClickableTextCandidate> candidates,
        string expectedText)
    {
        var materialized = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
            .ToList();

        if (materialized.Count == 0)
        {
            return null;
        }

        var normalizedExpected = Normalize(expectedText);

        return materialized
            .OrderBy(candidate => candidate.IsVisible ? 0 : 1)
            .ThenBy(candidate => GetMatchRank(candidate.Text, normalizedExpected))
            .ThenBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Text.Length)
            .FirstOrDefault();
    }

    private static int GetMatchRank(string candidateText, string expectedText)
    {
        var normalizedCandidate = Normalize(candidateText);

        if (string.Equals(normalizedCandidate, expectedText, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalizedCandidate.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Where(ch => !char.IsWhiteSpace(ch)));
    }
}
