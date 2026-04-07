using GB28181Platform.AiAgent.Abstractions;

namespace GB28181Platform.AiAgent.Runtime;

public class AgentPluginRegistry : IAgentPluginRegistry
{
    private readonly IReadOnlyList<AgentPluginRegistration> _registrations;

    public AgentPluginRegistry(IEnumerable<AgentPluginRegistration> registrations)
    {
        _registrations = registrations.ToArray();
    }

    public IReadOnlyList<AgentPluginRegistration> GetRegistrations() => _registrations;
}
