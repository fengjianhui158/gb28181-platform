using GB28181Platform.AiAgent.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GB28181Platform.AiAgent.Runtime;

public class SemanticKernelPromptExecutor : IAgentPromptExecutor
{
    private readonly SemanticKernelOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public SemanticKernelPromptExecutor(SemanticKernelOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> ExecuteTextPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var kernel = SemanticKernelClientFactory.CreateKernel(_options.Text, _loggerFactory, "prompt-text");
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentAsync(
            history,
            new OpenAIPromptExecutionSettings(),
            kernel,
            cancellationToken);

        return response.Content ?? string.Empty;
    }

    public async Task<string> ExecuteVisionPromptAsync(string prompt, string imageBase64, string mediaType = "image/png", CancellationToken cancellationToken = default)
    {
        var endpoint = string.IsNullOrWhiteSpace(_options.Vision.BaseUrl) ? _options.Text : _options.Vision;
        var kernel = SemanticKernelClientFactory.CreateKernel(endpoint, _loggerFactory, "prompt-vision");
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var items = new ChatMessageContentItemCollection
        {
            new ImageContent(Convert.FromBase64String(imageBase64), mediaType),
            new TextContent(prompt)
        };

        var history = new ChatHistory();
        history.AddUserMessage(items);

        var response = await chatService.GetChatMessageContentAsync(
            history,
            new OpenAIPromptExecutionSettings(),
            kernel,
            cancellationToken);

        return response.Content ?? string.Empty;
    }
}
