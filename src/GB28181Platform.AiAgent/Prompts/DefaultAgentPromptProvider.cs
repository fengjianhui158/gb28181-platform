using GB28181Platform.AiAgent.Abstractions;

namespace GB28181Platform.AiAgent.Prompts;

public class DefaultAgentPromptProvider : IAgentPromptProvider
{
    public string BuildSystemPrompt(string? deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId)
            ? SystemPrompts.MultimodalAssistant
            : $"{SystemPrompts.MultimodalAssistant}\n当前会话设备上下文: {deviceId}";
    }
}
