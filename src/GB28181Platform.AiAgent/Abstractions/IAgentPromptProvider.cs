namespace GB28181Platform.AiAgent.Abstractions;

public interface IAgentPromptProvider
{
    string BuildSystemPrompt(string? deviceId);
}
