using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Contracts;

namespace GB28181Platform.AiAgent;

public class AiAgentService : IAiAgentService
{
    private readonly AiChatApplicationService _applicationService;

    public AiAgentService(AiChatApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    public Task<AgentChatResponse> ChatAsync(int userId, AgentChatRequest request, CancellationToken cancellationToken = default)
        => _applicationService.ExecuteAsync(userId, request, cancellationToken);

    public async Task<string> ChatAsync(string sessionId, string userMessage, string? deviceId = null)
    {
        var response = await _applicationService.ExecuteAsync(0, new AgentChatRequest
        {
            ConversationId = sessionId,
            DeviceId = deviceId,
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = userMessage }]
        }, CancellationToken.None);

        return response.ContentItems.FirstOrDefault(item => item.Kind == "text")?.Text ?? string.Empty;
    }
}
