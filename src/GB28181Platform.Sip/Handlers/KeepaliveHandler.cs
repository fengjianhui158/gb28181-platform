using GB28181Platform.Sip.Sessions;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.Sip.Handlers;

public class KeepaliveHandler
{
    private readonly DeviceSessionManager _sessionManager;
    private readonly ILogger<KeepaliveHandler> _logger;

    public KeepaliveHandler(DeviceSessionManager sessionManager, ILogger<KeepaliveHandler> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// 处理心跳消息 (MANSCDP Keepalive)
    /// </summary>
    public Task HandleAsync(string deviceId)
    {
        _sessionManager.UpdateKeepalive(deviceId);
        _logger.LogDebug("心跳: DeviceId={DeviceId}", deviceId);
        return Task.CompletedTask;
    }
}
