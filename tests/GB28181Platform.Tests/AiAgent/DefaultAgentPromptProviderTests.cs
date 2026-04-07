using GB28181Platform.AiAgent.Prompts;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class DefaultAgentPromptProviderTests
{
    [Fact]
    public void BuildSystemPrompt_WithoutDeviceId_ReturnsBasePrompt()
    {
        var sut = new DefaultAgentPromptProvider();

        var prompt = sut.BuildSystemPrompt(null);

        Assert.Contains("X-Link", prompt);
        Assert.DoesNotContain("当前会话设备上下文", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithDeviceId_AppendsDeviceContext()
    {
        var sut = new DefaultAgentPromptProvider();

        var prompt = sut.BuildSystemPrompt("34020000001320000001");

        Assert.Contains("当前会话设备上下文", prompt);
        Assert.Contains("34020000001320000001", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsDiagnosticConclusionHardConstraint()
    {
        var sut = new DefaultAgentPromptProvider();

        var prompt = sut.BuildSystemPrompt("34020000001320000001");

        Assert.Contains("任务级诊断结论", prompt);
        Assert.Contains("优先按诊断结论回答", prompt);
        Assert.Contains("禁止自由猜测网络原因", prompt);
    }
}
