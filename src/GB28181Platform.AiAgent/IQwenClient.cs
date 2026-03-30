namespace GB28181Platform.AiAgent;

public interface IQwenClient
{
    Task<ChatResponse> ChatAsync(List<ChatMessage> messages, List<FunctionDefinition>? functions = null);

    /// <summary>
    /// 带图片的聊天（用于视觉模型分析截图等场景）
    /// imageBase64 为 PNG/JPG 图片的 base64 编码
    /// </summary>
    Task<ChatResponse> ChatWithImageAsync(string prompt, string imageBase64);
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string? Content { get; set; }
    public string? Name { get; set; }
    public FunctionCall? FunctionCall { get; set; }
    public string? ToolCallId { get; set; }
}

public class FunctionCall
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
}

public class FunctionDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object Parameters { get; set; } = new { };
}

public class ChatResponse
{
    public string? Content { get; set; }
    public FunctionCall? FunctionCall { get; set; }
    public string FinishReason { get; set; } = "";
    public string? ToolCallId { get; set; }
}
