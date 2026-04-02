namespace GB28181Platform.AiAgent.Multimodal;

public class NormalizedAgentInput
{
    public string ConversationId { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public List<NormalizedAgentInputItem> Items { get; set; } = [];
}

public class NormalizedAgentInputItem
{
    public string Kind { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public string? Base64Data { get; set; }
}
