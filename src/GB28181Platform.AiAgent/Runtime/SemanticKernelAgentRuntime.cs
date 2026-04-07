using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GB28181Platform.AiAgent.Runtime;

public class SemanticKernelAgentRuntime : IAgentRuntime
{
    private readonly SemanticKernelOptions _options;
    private readonly IAgentPromptProvider _promptProvider;
    private readonly IAudioTranscriptionService _audioTranscriptionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentPluginRegistry _pluginRegistry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SemanticKernelAgentRuntime> _logger;
    private readonly Func<Kernel, ChatHistory, OpenAIPromptExecutionSettings, CancellationToken, Task<ChatMessageContent>>? _chatExecutor;

    public SemanticKernelAgentRuntime(
        SemanticKernelOptions options,
        IAgentPromptProvider promptProvider,
        IAudioTranscriptionService audioTranscriptionService,
        IServiceProvider serviceProvider,
        IAgentPluginRegistry pluginRegistry,
        ILoggerFactory loggerFactory,
        ILogger<SemanticKernelAgentRuntime> logger)
        : this(options, promptProvider, audioTranscriptionService, serviceProvider, pluginRegistry, loggerFactory, logger, null)
    {
    }

    public SemanticKernelAgentRuntime(
        SemanticKernelOptions options,
        IAgentPromptProvider promptProvider,
        IAudioTranscriptionService audioTranscriptionService,
        IServiceProvider serviceProvider,
        IAgentPluginRegistry pluginRegistry,
        ILoggerFactory loggerFactory,
        ILogger<SemanticKernelAgentRuntime> logger,
        Func<Kernel, ChatHistory, OpenAIPromptExecutionSettings, CancellationToken, Task<ChatMessageContent>>? chatExecutor)
    {
        _options = options;
        _promptProvider = promptProvider;
        _audioTranscriptionService = audioTranscriptionService;
        _serviceProvider = serviceProvider;
        _pluginRegistry = pluginRegistry;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _chatExecutor = chatExecutor;
    }

    public async Task<AgentChatResponse> ExecuteAsync(
        int userId,
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history,
        CancellationToken cancellationToken)
    {
        var endpoint = ResolveEndpoint(input, history);
        var kernel = BuildKernel(endpoint);
        var chatHistory = await BuildChatHistoryAsync(input, history, cancellationToken);
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
            Temperature = 0
        };

        var response = await ExecuteChatAsync(kernel, chatHistory, settings, cancellationToken);
        var toolCalls = FunctionCallContent.GetFunctionCalls(response)
            .Select(call => $"{call.PluginName}-{call.FunctionName}")
            .ToList();

        _logger.LogInformation(
            "Semantic Kernel runtime executed. UserId={UserId}, ConversationId={ConversationId}, ToolCalls={ToolCallCount}",
            userId,
            input.ConversationId,
            toolCalls.Count);

        return new AgentChatResponse
        {
            ConversationId = input.ConversationId,
            MessageId = Guid.NewGuid().ToString("N"),
            Model = response.ModelId ?? endpoint.Model,
            ContentItems =
            [
                new AgentContentItemDto { Kind = "text", Text = response.Content ?? string.Empty }
            ],
            ToolCalls = toolCalls,
            Usage = new AgentExecutionUsage()
        };
    }

    private Kernel BuildKernel(SemanticKernelEndpointOptions endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl) || string.IsNullOrWhiteSpace(endpoint.Model))
        {
            throw new InvalidOperationException("Semantic Kernel endpoint configuration is required.");
        }

        var kernel = SemanticKernelClientFactory.CreateKernel(endpoint, _loggerFactory, "agent-runtime");

        foreach (var registration in _pluginRegistry.GetRegistrations())
        {
            var plugin = _serviceProvider.GetRequiredService(registration.ServiceType);
            if (string.IsNullOrWhiteSpace(registration.PluginName))
            {
                kernel.ImportPluginFromObject(plugin);
                continue;
            }

            kernel.ImportPluginFromObject(plugin, registration.PluginName);
        }

        return kernel;
    }

    private SemanticKernelEndpointOptions ResolveEndpoint(
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history)
    {
        var requiresVision = input.Items.Any(item => item.Kind == "image") ||
            history.Any(message => message.Items.Any(item => item.Kind == "image"));

        if (requiresVision && IsConfigured(_options.Vision))
        {
            return _options.Vision;
        }

        if (IsConfigured(_options.Text))
        {
            return _options.Text;
        }

        if (requiresVision)
        {
            throw new InvalidOperationException("SemanticKernel:VisionModel or SemanticKernel:TextModel configuration is required for image inputs.");
        }

        throw new InvalidOperationException("SemanticKernel:TextModel configuration is required.");
    }

    private static bool IsConfigured(SemanticKernelEndpointOptions endpoint)
        => !string.IsNullOrWhiteSpace(endpoint.BaseUrl) && !string.IsNullOrWhiteSpace(endpoint.Model);

    private async Task<ChatHistory> BuildChatHistoryAsync(
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history,
        CancellationToken cancellationToken)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(_promptProvider.BuildSystemPrompt(input.DeviceId));

        foreach (var message in history)
        {
            await AddConversationMessageAsync(chatHistory, message.Role, message.Items, cancellationToken);
        }

        await AddNormalizedInputAsync(chatHistory, input, cancellationToken);
        return chatHistory;
    }

    private async Task AddNormalizedInputAsync(ChatHistory chatHistory, NormalizedAgentInput input, CancellationToken cancellationToken)
    {
        var items = await BuildContentItemsAsync(input.Items.Select(item => new ConversationContentItemRecord
        {
            Kind = item.Kind,
            Text = item.Text,
            FileName = item.FileName,
            MediaType = item.MediaType,
            Base64Data = item.Base64Data
        }).ToList(), cancellationToken);

        chatHistory.AddUserMessage(items);
    }

    private async Task AddConversationMessageAsync(
        ChatHistory chatHistory,
        string role,
        IReadOnlyList<ConversationContentItemRecord> items,
        CancellationToken cancellationToken)
    {
        var contentItems = await BuildContentItemsAsync(items, cancellationToken);

        if (role == "assistant")
        {
            chatHistory.AddMessage(AuthorRole.Assistant, contentItems);
            return;
        }

        chatHistory.AddMessage(AuthorRole.User, contentItems);
    }

    private async Task<ChatMessageContentItemCollection> BuildContentItemsAsync(
        IReadOnlyList<ConversationContentItemRecord> items,
        CancellationToken cancellationToken)
    {
        var contentItems = new ChatMessageContentItemCollection();

        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case "image" when !string.IsNullOrWhiteSpace(item.Base64Data):
                    contentItems.Add(new ImageContent(
                        Convert.FromBase64String(item.Base64Data),
                        item.MediaType ?? "image/png"));
                    break;

                case "audio" when !string.IsNullOrWhiteSpace(item.Base64Data):
                    var transcription = await _audioTranscriptionService.TranscribeAsync(
                        item.MediaType ?? "audio/wav",
                        item.Base64Data,
                        cancellationToken);
                    contentItems.Add(new TextContent($"[audio transcription]\n{transcription}"));
                    break;

                default:
                    contentItems.Add(new TextContent(item.Text ?? string.Empty));
                    break;
            }
        }

        return contentItems;
    }

    private Task<ChatMessageContent> ExecuteChatAsync(
        Kernel kernel,
        ChatHistory chatHistory,
        OpenAIPromptExecutionSettings settings,
        CancellationToken cancellationToken)
    {
        if (_chatExecutor is not null)
        {
            return _chatExecutor(kernel, chatHistory, settings, cancellationToken);
        }

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        return chatService.GetChatMessageContentAsync(chatHistory, settings, kernel, cancellationToken);
    }
}
