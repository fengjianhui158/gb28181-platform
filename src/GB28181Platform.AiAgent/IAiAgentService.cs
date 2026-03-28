namespace GB28181Platform.AiAgent;

public interface IAiAgentService
{
    Task<string> ChatAsync(string sessionId, string userMessage, string? deviceId = null);
}
