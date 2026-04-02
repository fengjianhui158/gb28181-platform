using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.AiAgent.Runtime;

public class SemanticKernelAgentRuntime : IAgentRuntime
{
    private readonly ILogger<SemanticKernelAgentRuntime> _logger;

    public SemanticKernelAgentRuntime(ILogger<SemanticKernelAgentRuntime> logger)
    {
        _logger = logger;
    }

    public Task<AgentChatResponse> ExecuteAsync(
        int userId,
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Semantic Kernel runtime placeholder executed for user {UserId}", userId);

        return Task.FromResult(new AgentChatResponse
        {
            ConversationId = input.ConversationId,
            MessageId = Guid.NewGuid().ToString("N"),
            Model = "semantic-kernel-placeholder",
            ContentItems =
            [
                new AgentContentItemDto { Kind = "text", Text = "placeholder" }
            ]
        });
    }
}
