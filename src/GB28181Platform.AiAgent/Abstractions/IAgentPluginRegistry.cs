namespace GB28181Platform.AiAgent.Abstractions;

public interface IAgentPluginRegistry
{
    IReadOnlyList<AgentPluginRegistration> GetRegistrations();
}

public sealed record AgentPluginRegistration(Type ServiceType, string? PluginName = null);
