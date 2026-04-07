namespace GB28181Platform.AiAgent.Contracts;

public class AgentChatResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<AgentContentItemDto> ContentItems { get; set; } = [];
    public List<string> ToolCalls { get; set; } = [];
    public List<string> Citations { get; set; } = [];
    public AgentExecutionUsage Usage { get; set; } = new();
}
