using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Plugins;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using GB28181Platform.AiAgent.Prompts;
using GB28181Platform.AiAgent.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NSubstitute;
using SqlSugar;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class SemanticKernelAgentRuntimeTests
{
    [Fact]
    public async Task ExecuteAsync_TranscribesAudioAndPreservesImageItems()
    {
        ChatHistory? capturedHistory = null;
        var audioTranscription = Substitute.For<IAudioTranscriptionService>();
        audioTranscription.TranscribeAsync("audio/wav", "UklGRg==", Arg.Any<CancellationToken>())
            .Returns("audio transcript");
        using var services = BuildServiceProvider((nameof(DeviceCapabilityPlugin), typeof(DeviceCapabilityPlugin)), (nameof(DiagnosticCapabilityPlugin), typeof(DiagnosticCapabilityPlugin)));
        using var scope = services.CreateScope();

        var runtime = new SemanticKernelAgentRuntime(
            new SemanticKernelOptions
            {
                Text = new SemanticKernelEndpointOptions
                {
                    BaseUrl = "https://api.example.com",
                    ApiKey = "test-key",
                    Model = "test-model"
                }
            },
            new DefaultAgentPromptProvider(),
            audioTranscription,
            scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<IAgentPluginRegistry>(),
            NullLoggerFactory.Instance,
            NullLogger<SemanticKernelAgentRuntime>.Instance,
            (kernel, chatHistory, settings, cancellationToken) =>
            {
                capturedHistory = chatHistory;
                return Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, "runtime ok", "test-model"));
            });

        var input = new NormalizedAgentInput
        {
            ConversationId = "conv-001",
            Items =
            [
                new NormalizedAgentInputItem
                {
                    Kind = "image",
                    MediaType = "image/png",
                    Base64Data = "ZmFrZQ=="
                },
                new NormalizedAgentInputItem
                {
                    Kind = "audio",
                    MediaType = "audio/wav",
                    Base64Data = "UklGRg=="
                }
            ]
        };

        var response = await runtime.ExecuteAsync(1, input, [], CancellationToken.None);

        Assert.Equal("runtime ok", response.ContentItems[0].Text);
        Assert.NotNull(capturedHistory);
        Assert.Equal(2, capturedHistory!.Count);
        Assert.Contains(capturedHistory[1].Items, item => item is ImageContent);
        Assert.Contains(capturedHistory[1].Items, item => item is TextContent text && text.Text!.Contains("audio transcript", StringComparison.Ordinal));
        await audioTranscription.Received(1).TranscribeAsync("audio/wav", "UklGRg==", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithImageInput_AllowsVisionOnlyConfiguration()
    {
        using var services = BuildServiceProvider((nameof(DeviceCapabilityPlugin), typeof(DeviceCapabilityPlugin)), (nameof(DiagnosticCapabilityPlugin), typeof(DiagnosticCapabilityPlugin)));
        using var scope = services.CreateScope();

        var runtime = new SemanticKernelAgentRuntime(
            new SemanticKernelOptions
            {
                Vision = new SemanticKernelEndpointOptions
                {
                    BaseUrl = "https://vision.example.com",
                    ApiKey = "vision-key",
                    Model = "vision-model"
                }
            },
            new DefaultAgentPromptProvider(),
            Substitute.For<IAudioTranscriptionService>(),
            scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<IAgentPluginRegistry>(),
            NullLoggerFactory.Instance,
            NullLogger<SemanticKernelAgentRuntime>.Instance,
            (kernel, chatHistory, settings, cancellationToken) =>
                Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, "vision ok", "vision-model")));

        var input = new NormalizedAgentInput
        {
            ConversationId = "conv-vision",
            Items =
            [
                new NormalizedAgentInputItem
                {
                    Kind = "image",
                    MediaType = "image/png",
                    Base64Data = "ZmFrZQ=="
                }
            ]
        };

        var response = await runtime.ExecuteAsync(1, input, [], CancellationToken.None);

        Assert.Equal("vision-model", response.Model);
        Assert.Equal("vision ok", response.ContentItems[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_ImportsPluginsFromRegistry()
    {
        string[] importedPlugins = [];
        using var services = BuildServiceProvider(("custom_tools", typeof(TestCapabilityPlugin)));
        using var scope = services.CreateScope();

        var runtime = new SemanticKernelAgentRuntime(
            new SemanticKernelOptions
            {
                Text = new SemanticKernelEndpointOptions
                {
                    BaseUrl = "https://api.example.com",
                    ApiKey = "test-key",
                    Model = "test-model"
                }
            },
            new DefaultAgentPromptProvider(),
            Substitute.For<IAudioTranscriptionService>(),
            scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<IAgentPluginRegistry>(),
            NullLoggerFactory.Instance,
            NullLogger<SemanticKernelAgentRuntime>.Instance,
            (kernel, chatHistory, settings, cancellationToken) =>
            {
                importedPlugins = kernel.Plugins.Select(plugin => plugin.Name).ToArray();
                return Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, "ok", "test-model"));
            });

        await runtime.ExecuteAsync(1, new NormalizedAgentInput
        {
            ConversationId = "conv-plugin",
            Items = [new NormalizedAgentInputItem { Kind = "text", Text = "hello" }]
        }, [], CancellationToken.None);

        Assert.Contains("custom_tools", importedPlugins);
    }

    private static ServiceProvider BuildServiceProvider(params (string PluginName, Type PluginType)[] registrations)
    {
        var services = new ServiceCollection();
        var db = Substitute.For<ISqlSugarClient>();

        services.AddScoped(_ => db);
        services.AddScoped<DeviceCapabilityPlugin>();
        services.AddScoped<DiagnosticCapabilityPlugin>();
        services.AddScoped<TestCapabilityPlugin>();

        foreach (var (pluginName, pluginType) in registrations)
        {
            services.AddSingleton(new AgentPluginRegistration(pluginType, pluginName));
        }

        services.AddSingleton<IAgentPluginRegistry, AgentPluginRegistry>();
        return services.BuildServiceProvider();
    }

    private sealed class TestCapabilityPlugin
    {
        [KernelFunction("ping")]
        public string Ping() => "pong";
    }
}
