using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.Domain.Entities;
using SqlSugar;

namespace GB28181Platform.AiAgent.Capabilities.Persistence;

public class SqlSugarConversationStore : IConversationStore
{
    private readonly ISqlSugarClient _db;

    public SqlSugarConversationStore(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ConversationMessageRecord>> GetHistoryAsync(int userId, string conversationId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConversationMessageRecord>>([]);
    }

    public async Task AppendMessageAsync(ConversationMessageRecord message, CancellationToken cancellationToken)
    {
        var rows = message.Items.Select(item => new AiConversation
        {
            UserId = message.UserId,
            SessionId = message.ConversationId,
            MessageId = message.MessageId,
            DeviceId = message.DeviceId,
            Role = message.Role,
            ContentKind = item.Kind,
            FileName = item.FileName,
            MediaType = item.MediaType,
            Content = item.Text ?? item.Base64Data ?? string.Empty,
            CreatedAt = message.CreatedAt
        }).ToList();

        await _db.Insertable(rows).ExecuteCommandAsync();
    }
}
