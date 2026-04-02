using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.Api.Controllers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AiAgentControllerTests
{
    [Fact]
    public async Task Chat_ReturnsStructuredAgentResponse()
    {
        var aiAgent = Substitute.For<IAiAgentService>();
        aiAgent.ChatAsync(Arg.Any<int>(), Arg.Any<AgentChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AgentChatResponse
            {
                ConversationId = "conv-001",
                MessageId = "msg-001",
                Model = "deepseek-chat",
                ContentItems = [new AgentContentItemDto { Kind = "text", Text = "device online" }]
            });

        var controller = new AiAgentController(aiAgent, Substitute.For<ILogger<AiAgentController>>());

        var result = await controller.Chat(new AgentChatRequest
        {
            ConversationId = "conv-001",
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = "device status?" }]
        }, CancellationToken.None);

        Assert.Equal("conv-001", result.Data?.ConversationId);
        Assert.Equal("device online", result.Data?.ContentItems[0].Text);
    }
}
