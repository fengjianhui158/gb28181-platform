namespace GB28181Platform.AiAgent;

public interface IQwenClient
{
    Task<ChatResponse> ChatAsync(List<ChatMessage> messages, List<FunctionDefinition>? functions = null);
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string? Content { get; set; }
    public string? Name { get; set; }
    public FunctionCall? FunctionCall { get; set; }
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
}
