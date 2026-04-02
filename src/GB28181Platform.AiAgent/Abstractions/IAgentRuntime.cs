using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;

namespace GB28181Platform.AiAgent.Abstractions;

public interface IAgentRuntime
{
    Task<AgentChatResponse> ExecuteAsync(
        int userId,
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history,
        CancellationToken cancellationToken);
}
