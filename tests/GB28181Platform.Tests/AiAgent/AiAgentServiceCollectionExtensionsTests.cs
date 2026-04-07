using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AiAgentServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAiAgentCoreAndPlugin_RegistersRuntimeAndPluginMetadata()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SemanticKernel:TextModel:BaseUrl"] = "https://api.example.com",
                ["SemanticKernel:TextModel:ApiKey"] = "key",
                ["SemanticKernel:TextModel:Model"] = "chat-model"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddAiAgentCore(configuration);
        services.AddAiAgentPlugin<TestCapabilityPlugin>("custom_tools");

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IAgentPluginRegistry>();

        Assert.Contains(registry.GetRegistrations(), registration =>
            registration.ServiceType == typeof(TestCapabilityPlugin) &&
            registration.PluginName == "custom_tools");
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAgentRuntime));
    }

    private sealed class TestCapabilityPlugin
    {
        [KernelFunction("echo")]
        public string Echo(string input) => input;
    }
}
