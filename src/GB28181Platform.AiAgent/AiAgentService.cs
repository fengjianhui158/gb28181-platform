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
}
