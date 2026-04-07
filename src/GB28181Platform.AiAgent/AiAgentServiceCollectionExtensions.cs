using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Prompts;
using GB28181Platform.AiAgent.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GB28181Platform.AiAgent;

public static class AiAgentServiceCollectionExtensions
{
    public static IServiceCollection AddAiAgentCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<SemanticKernelOptions>(_ => SemanticKernelModelRouter.FromConfiguration(configuration));
        services.TryAddSingleton<IAgentPluginRegistry, AgentPluginRegistry>();
        services.TryAddScoped<IAgentPromptProvider, DefaultAgentPromptProvider>();
        services.TryAddScoped<IAgentPromptExecutor, SemanticKernelPromptExecutor>();
        services.TryAddScoped<IAgentRuntime, SemanticKernelAgentRuntime>();
        services.TryAddScoped<AiChatApplicationService>();
        services.TryAddScoped<IAiAgentService, AiAgentService>();
        return services;
    }

    public static IServiceCollection AddAiAgentPlugin<TPlugin>(this IServiceCollection services, string? pluginName = null)
        where TPlugin : class
    {
        services.AddScoped<TPlugin>();
        services.AddSingleton(new AgentPluginRegistration(typeof(TPlugin), pluginName));
        return services;
    }
}
