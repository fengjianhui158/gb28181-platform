namespace GB28181Platform.AiAgent.Conversation;

public class ConversationMessageRecord
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string ConversationId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ConversationContentItemRecord> Items { get; set; } = [];
}
