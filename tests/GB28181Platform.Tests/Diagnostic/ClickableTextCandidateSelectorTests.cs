using GB28181Platform.Diagnostic.Browser;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class ClickableTextCandidateSelectorTests
{
    [Fact]
    public void SelectBest_PrefersVisibleExactMatchOverHiddenCandidate()
    {
        ClickableTextCandidate[] candidates =
        [
            new ClickableTextCandidate("div:has-text(\"设置\")", 0, "设置", false, 5),
            new ClickableTextCandidate("td:has-text(\"设置\")", 0, "设置", true, 3),
            new ClickableTextCandidate("span:has-text(\"设置\")", 0, "设置管理", true, 4)
        ];

        var best = ClickableTextCandidateSelector.SelectBest(candidates, "设置");

        Assert.NotNull(best);
        Assert.Equal("td:has-text(\"设置\")", best!.Selector);
    }

    [Fact]
    public void SelectBest_FallsBackToVisibleContainsMatchWhenExactMatchIsUnavailable()
    {
        ClickableTextCandidate[] candidates =
        [
            new ClickableTextCandidate("div:has-text(\"平台接入\")", 0, "网络设置平台接入", true, 5),
            new ClickableTextCandidate("li:has-text(\"平台接入\")", 0, "平台接入", false, 2)
        ];

        var best = ClickableTextCandidateSelector.SelectBest(candidates, "平台接入");

        Assert.NotNull(best);
        Assert.Equal("div:has-text(\"平台接入\")", best!.Selector);
    }
}
