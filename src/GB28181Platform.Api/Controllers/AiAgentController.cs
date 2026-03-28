using GB28181Platform.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace GB28181Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiAgentController : ControllerBase
{
    private readonly ILogger<AiAgentController> _logger;

    public AiAgentController(ILogger<AiAgentController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// AI 问答接口
    /// </summary>
    [HttpPost("chat")]
    public async Task<ApiResponse<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        // TODO: Phase 4 实现 — 集成 Qwen Function Calling
        _logger.LogInformation("AI 问答: SessionId={SessionId}, Message={Message}",
            request.SessionId, request.Message);

        return ApiResponse<ChatResponse>.Ok(new ChatResponse
        {
            SessionId = request.SessionId ?? Guid.NewGuid().ToString("N"),
            Reply = "AI Agent 功能将在 Phase 4 实现。"
        });
    }
}

public class ChatRequest
{
    public string? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
}
