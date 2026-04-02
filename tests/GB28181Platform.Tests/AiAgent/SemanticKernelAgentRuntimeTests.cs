using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Plugins;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using GB28181Platform.AiAgent.Prompts;
using GB28181Platform.AiAgent.Runtime;
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
            new DeviceCapabilityPlugin(Substitute.For<ISqlSugarClient>()),
            new DiagnosticCapabilityPlugin(Substitute.For<ISqlSugarClient>()),
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
}
