namespace GB28181Platform.AiAgent.Contracts;

public class AgentContentItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public string? Base64Data { get; set; }
}
