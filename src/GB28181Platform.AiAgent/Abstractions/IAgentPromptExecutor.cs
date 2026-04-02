namespace GB28181Platform.AiAgent.Abstractions;

public interface IAgentPromptExecutor
{
    Task<string> ExecuteTextPromptAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> ExecuteVisionPromptAsync(string prompt, string imageBase64, string mediaType = "image/png", CancellationToken cancellationToken = default);
}
