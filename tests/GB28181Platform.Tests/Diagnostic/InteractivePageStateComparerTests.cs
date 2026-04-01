using GB28181Platform.Diagnostic.Browser.Interactive;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class InteractivePageStateComparerTests
{
    [Fact]
    public void HasMeaningfulChange_ReturnsTrue_WhenVisibleTextChanges()
    {
        var before = new InteractivePageStateSnapshot(
            "/index",
            "预览 回放 设置",
            ["预览", "回放", "设置"]);
        var after = new InteractivePageStateSnapshot(
            "/index",
            "预览 回放 设置 网络设置",
            ["预览", "回放", "设置", "网络设置"]);

        Assert.True(InteractivePageStateComparer.HasMeaningfulChange(before, after));
    }

    [Fact]
    public void HasMeaningfulChange_ReturnsFalse_WhenStateIsEquivalent()
    {
        var before = new InteractivePageStateSnapshot(
            "/index",
            "预览 回放 设置",
            ["预览", "回放", "设置"]);
        var after = new InteractivePageStateSnapshot(
            "/index",
            "预览 回放 设置",
            ["预览", "回放", "设置"]);

        Assert.False(InteractivePageStateComparer.HasMeaningfulChange(before, after));
    }
}
