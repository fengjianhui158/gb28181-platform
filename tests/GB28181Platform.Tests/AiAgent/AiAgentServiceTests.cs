using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AiAgentServiceTests
{
    [Fact]
    public async Task ChatAsync_DelegatesStructuredRequest_ToApplicationService()
    {
        var runtime = Substitute.For<IAgentRuntime>();
        runtime.ExecuteAsync(Arg.Any<int>(), Arg.Any<NormalizedAgentInput>(), Arg.Any<IReadOnlyList<ConversationMessageRecord>>(), Arg.Any<CancellationToken>())
            .Returns(new AgentChatResponse
            {
                ConversationId = "conv-001",
                MessageId = "msg-001",
                ContentItems = [new AgentContentItemDto { Kind = "text", Text = "all systems healthy" }]
            });

        var store = Substitute.For<IConversationStore>();
        store.GetHistoryAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var applicationService = new AiChatApplicationService(
            runtime,
            store,
            Substitute.For<IAgentPromptProvider>(),
            NullLogger<AiChatApplicationService>.Instance);

        var service = new AiAgentService(applicationService);

        var result = await service.ChatAsync(7, new AgentChatRequest
        {
            ConversationId = "conv-001",
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = "system status?" }]
        });

        Assert.Equal("conv-001", result.ConversationId);
        await runtime.Received(1).ExecuteAsync(7, Arg.Any<NormalizedAgentInput>(), Arg.Any<IReadOnlyList<ConversationMessageRecord>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacyChatAsync_ConvertsTextOnlyRequest()
    {
        var runtime = Substitute.For<IAgentRuntime>();
        runtime.ExecuteAsync(0, Arg.Any<NormalizedAgentInput>(), Arg.Any<IReadOnlyList<ConversationMessageRecord>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var input = callInfo.ArgAt<NormalizedAgentInput>(1);
                return new AgentChatResponse
                {
                    ConversationId = input.ConversationId,
                    MessageId = "msg-001",
                    ContentItems = [new AgentContentItemDto { Kind = "text", Text = "camera cam001 offline" }]
                };
            });

        var store = Substitute.For<IConversationStore>();
        store.GetHistoryAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var applicationService = new AiChatApplicationService(
            runtime,
            store,
            Substitute.For<IAgentPromptProvider>(),
            NullLogger<AiChatApplicationService>.Instance);

        var service = new AiAgentService(applicationService);

        var result = await service.ChatAsync("session1", "why is cam001 offline?", "cam001");

        Assert.Equal("camera cam001 offline", result);
        await runtime.Received(1).ExecuteAsync(0,
            Arg.Is<NormalizedAgentInput>(input =>
                input.ConversationId == "session1" &&
                input.DeviceId == "cam001" &&
                input.Items.Count == 1 &&
                input.Items[0].Kind == "text" &&
                input.Items[0].Text == "why is cam001 offline?"),
            Arg.Any<IReadOnlyList<ConversationMessageRecord>>(),
            Arg.Any<CancellationToken>());
    }
}
