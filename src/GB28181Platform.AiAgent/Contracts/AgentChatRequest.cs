namespace GB28181Platform.AiAgent.Contracts;

public class AgentChatRequest
{
    public string? ConversationId { get; set; }
    public string? DeviceId { get; set; }
    public string? ClientMessageId { get; set; }
    public List<AgentContentItemDto> ContentItems { get; set; } = [];
}
