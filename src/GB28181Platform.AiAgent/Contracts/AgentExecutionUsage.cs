namespace GB28181Platform.AiAgent.Contracts;

public class AgentExecutionUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
