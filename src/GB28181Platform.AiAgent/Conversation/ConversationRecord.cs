namespace GB28181Platform.AiAgent.Conversation;

public class ConversationRecord
{
    public string ConversationId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
