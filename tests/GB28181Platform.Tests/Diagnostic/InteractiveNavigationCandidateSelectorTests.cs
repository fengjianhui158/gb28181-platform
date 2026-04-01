using GB28181Platform.Diagnostic.Browser.Interactive;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class InteractiveNavigationCandidateSelectorTests
{
    [Fact]
    public void SelectBest_PrefersVisibleExactMenuCandidate()
    {
        InteractiveNavigationCandidate[] candidates =
        [
            new("div", 0, "\u8bbe\u7f6e", true, false, 6, Top: 320, Area: 24000, NavigationContextScore: 0, HasPointerCursor: false, HasNoiseKeywords: false, LineCount: 1),
            new("button", 0, "\u8bbe\u7f6e", true, true, 1, Top: 48, Area: 2800, NavigationContextScore: 3, HasPointerCursor: true, HasNoiseKeywords: false, LineCount: 1),
            new("span", 0, "\u8bbe\u7f6e\u7ba1\u7406", true, false, 4, Top: 80, Area: 3200, NavigationContextScore: 0, HasPointerCursor: false, HasNoiseKeywords: false, LineCount: 1)
        ];

        var best = InteractiveNavigationCandidate.SelectBest(candidates, "\u8bbe\u7f6e");

        Assert.NotNull(best);
        Assert.Equal("button", best!.TagName);
        Assert.True(best.IsSemanticMenu);
    }

    [Fact]
    public void SelectBest_PenalizesNoisyContainerText()
    {
        InteractiveNavigationCandidate[] candidates =
        [
            new("div", 0, "\u7528\u6237\u540d\r\n\u5bc6\u7801\r\n\u8bbe\u7f6e\r\n\u767b\u5f55 \u53d6\u6d88", true, false, 15, Top: 60, Area: 42000, NavigationContextScore: 0, HasPointerCursor: false, HasNoiseKeywords: true, LineCount: 4),
            new("td", 0, "\u8bbe\u7f6e", true, true, 3, Top: 58, Area: 5400, NavigationContextScore: 2, HasPointerCursor: true, HasNoiseKeywords: false, LineCount: 1)
        ];

        var best = InteractiveNavigationCandidate.SelectBest(candidates, "\u8bbe\u7f6e");

        Assert.NotNull(best);
        Assert.Equal("td", best!.TagName);
        Assert.False(best.HasNoiseKeywords);
    }

    [Fact]
    public void SelectBest_PrefersCandidateInsideNavigationGroup()
    {
        InteractiveNavigationCandidate[] candidates =
        [
            new("div", 0, "\u8bbe\u7f6e", true, true, 0, Top: 72, Area: 2200, NavigationContextScore: 0, HasPointerCursor: true, HasNoiseKeywords: false, LineCount: 1),
            new(":text-is(\"\\u8bbe\\u7f6e\")", 0, "\u8bbe\u7f6e", true, true, 0, Top: 68, Area: 1800, NavigationContextScore: 5, HasPointerCursor: true, HasNoiseKeywords: false, LineCount: 1)
        ];

        var best = InteractiveNavigationCandidate.SelectBest(candidates, "\u8bbe\u7f6e");

        Assert.NotNull(best);
        Assert.Equal(5, best!.NavigationContextScore);
        Assert.True(best.HasPointerCursor);
    }
}
