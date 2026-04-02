using GB28181Platform.AiAgent.Conversation;

namespace GB28181Platform.AiAgent.Abstractions;

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationMessageRecord>> GetHistoryAsync(int userId, string conversationId, CancellationToken cancellationToken);
    Task AppendMessageAsync(ConversationMessageRecord message, CancellationToken cancellationToken);
}
