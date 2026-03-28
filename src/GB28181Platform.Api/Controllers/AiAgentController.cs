using GB28181Platform.AiAgent;
using GB28181Platform.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace GB28181Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiAgentController : ControllerBase
{
    private readonly IAiAgentService _aiAgent;
    private readonly ILogger<AiAgentController> _logger;

    public AiAgentController(IAiAgentService aiAgent, ILogger<AiAgentController> logger)
    {
        _aiAgent = aiAgent;
        _logger = logger;
    }

    /// <summary>
    /// AI 问答接口
    /// </summary>
    [HttpPost("chat")]
    public async Task<ApiResponse<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N");
        _logger.LogInformation("AI 问答: SessionId={SessionId}, Message={Message}",
            sessionId, request.Message);

        var reply = await _aiAgent.ChatAsync(sessionId, request.Message, request.DeviceId);

        return ApiResponse<ChatResponse>.Ok(new ChatResponse
        {
            SessionId = sessionId,
            Reply = reply
        });
    }
}

public class ChatRequest
{
    public string? SessionId { get; set; }
    public string? DeviceId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
}
