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
        return GetHistoryInternalAsync(userId, conversationId);
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

    private async Task<IReadOnlyList<ConversationMessageRecord>> GetHistoryInternalAsync(int userId, string conversationId)
    {
        var rows = await _db.Queryable<AiConversation>()
            .Where(x => x.UserId == userId && x.SessionId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return rows
            .GroupBy(row => string.IsNullOrWhiteSpace(row.MessageId)
                ? $"{row.Role}:{row.CreatedAt:O}:{row.Id}"
                : row.MessageId!)
            .OrderBy(group => group.Min(x => x.CreatedAt))
            .Select(group =>
            {
                var first = group.First();
                return new ConversationMessageRecord
                {
                    MessageId = first.MessageId ?? $"{first.Role}-{first.Id}",
                    ConversationId = first.SessionId,
                    UserId = first.UserId,
                    Role = first.Role,
                    DeviceId = first.DeviceId,
                    CreatedAt = group.Min(x => x.CreatedAt),
                    Items = group.Select(row => new ConversationContentItemRecord
                    {
                        Kind = row.ContentKind ?? "text",
                        Text = row.ContentKind == "text" ? row.Content : null,
                        FileName = row.FileName,
                        MediaType = row.MediaType,
                        Base64Data = row.ContentKind is "image" or "audio" ? row.Content : null
                    }).ToList()
                };
            })
            .ToList();
    }
}
