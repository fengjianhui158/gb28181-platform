using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
        var userId = ResolveUserId(request);
        _logger.LogInformation("AI chat request received. UserId={UserId}, ConversationId={ConversationId}, ItemCount={ItemCount}",
            userId, request.ConversationId, request.ContentItems.Count);

        var response = await _aiAgent.ChatAsync(userId, request, cancellationToken);
        return ApiResponse<AgentChatResponse>.Ok(response);
    }

    private int ResolveUserId(AgentChatRequest request)
    {
        if (User is not null)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(claim, out var userId))
            {
                return userId;
            }
        }

        var clientKey = request.ClientId;
        if (string.IsNullOrWhiteSpace(clientKey))
        {
            var httpContext = HttpContext;
            var remoteIp = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "unknown";
            clientKey = $"{remoteIp}|{userAgent}";
        }

        return ComputeStablePositiveId(clientKey);
    }

    private static int ComputeStablePositiveId(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        return value == 0 ? 1 : value;
    }
}
