using GB28181Platform.AiAgent.Contracts;

namespace GB28181Platform.AiAgent;

public interface IAiAgentService
{
    Task<AgentChatResponse> ChatAsync(int userId, AgentChatRequest request, CancellationToken cancellationToken = default);
}
