using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.AiAgent.Capabilities.Application;

public class AiChatApplicationService
{
    private readonly IAgentRuntime _runtime;
    private readonly IConversationStore _conversationStore;
    private readonly IAgentPromptProvider _promptProvider;
    private readonly ILogger<AiChatApplicationService> _logger;

    public AiChatApplicationService(
        IAgentRuntime runtime,
        IConversationStore conversationStore,
        IAgentPromptProvider promptProvider,
        ILogger<AiChatApplicationService> logger)
    {
        _runtime = runtime;
        _conversationStore = conversationStore;
        _promptProvider = promptProvider;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ExecuteAsync(int userId, AgentChatRequest request, CancellationToken cancellationToken)
    {
        var normalized = AgentInputNormalizer.Normalize(request);
        var history = await _conversationStore.GetHistoryAsync(userId, normalized.ConversationId, cancellationToken);
        _logger.LogInformation("AI chat executing. UserId={UserId}, ConversationId={ConversationId}, DeviceId={DeviceId}",
            userId, normalized.ConversationId, normalized.DeviceId);

        _ = _promptProvider.BuildSystemPrompt(normalized.DeviceId);

        var userMessage = new ConversationMessageRecord
        {
            ConversationId = normalized.ConversationId,
            UserId = userId,
            DeviceId = normalized.DeviceId,
            Role = "user",
            Items = normalized.Items.Select(x => new ConversationContentItemRecord
            {
                Kind = x.Kind,
                Text = x.Text,
                FileName = x.FileName,
                MediaType = x.MediaType,
                Base64Data = x.Base64Data
            }).ToList()
        };

        await _conversationStore.AppendMessageAsync(userMessage, cancellationToken);

        var response = await _runtime.ExecuteAsync(userId, normalized, history, cancellationToken);

        await _conversationStore.AppendMessageAsync(new ConversationMessageRecord
        {
            MessageId = response.MessageId,
            ConversationId = response.ConversationId,
            UserId = userId,
            DeviceId = normalized.DeviceId,
            Role = "assistant",
            Items = response.ContentItems.Select(x => new ConversationContentItemRecord
            {
                Kind = x.Kind,
                Text = x.Text,
                FileName = x.FileName,
                MediaType = x.MediaType,
                Base64Data = x.Base64Data
            }).ToList()
        }, cancellationToken);

        return response;
    }
}
