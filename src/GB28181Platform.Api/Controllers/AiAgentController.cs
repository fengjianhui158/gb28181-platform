using System.Security.Claims;
using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Contracts;
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

    [HttpPost("chat")]
    public async Task<ApiResponse<AgentChatResponse>> Chat([FromBody] AgentChatRequest request, CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        _logger.LogInformation("AI chat request received. UserId={UserId}, ConversationId={ConversationId}, ItemCount={ItemCount}",
            userId, request.ConversationId, request.ContentItems.Count);

        var response = await _aiAgent.ChatAsync(userId, request, cancellationToken);
        return ApiResponse<AgentChatResponse>.Ok(response);
    }

    private int TryGetUserId()
    {
        if (User is null)
        {
            return 0;
        }

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var userId) ? userId : 0;
    }
}
