using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AiChatApplicationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_AppendsUserAndAssistantMessages()
    {
        var runtime = Substitute.For<IAgentRuntime>();
        runtime.ExecuteAsync(Arg.Any<int>(), Arg.Any<NormalizedAgentInput>(), Arg.Any<IReadOnlyList<ConversationMessageRecord>>(), Arg.Any<CancellationToken>())
            .Returns(new AgentChatResponse
            {
                ConversationId = "conv-001",
                MessageId = "msg-001",
                Model = "semantic-kernel-placeholder",
                ContentItems = [new AgentContentItemDto { Kind = "text", Text = "device offline" }]
            });

        var store = Substitute.For<IConversationStore>();
        store.GetHistoryAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = new AiChatApplicationService(
            runtime,
            store,
            Substitute.For<IAgentPromptProvider>(),
            NullLogger<AiChatApplicationService>.Instance);

        var response = await sut.ExecuteAsync(7, new AgentChatRequest
        {
            ConversationId = "conv-001",
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = "check device status" }]
        }, CancellationToken.None);

        Assert.Equal("conv-001", response.ConversationId);
        await store.Received(2).AppendMessageAsync(Arg.Any<ConversationMessageRecord>(), Arg.Any<CancellationToken>());
    }
}
