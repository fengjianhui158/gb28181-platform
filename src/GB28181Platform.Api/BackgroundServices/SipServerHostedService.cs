using GB28181Platform.Api.Hubs;
using GB28181Platform.Sip.Handlers;
using GB28181Platform.Sip.Server;
using Microsoft.AspNetCore.SignalR;

namespace GB28181Platform.Api.BackgroundServices;

public class SipServerHostedService : IHostedService
{
    private readonly Gb28181SipServer _sipServer;
    private readonly RegisterHandler _registerHandler;
    private readonly IHubContext<DeviceStatusHub> _hubContext;
    private readonly ILogger<SipServerHostedService> _logger;

    public SipServerHostedService(
        Gb28181SipServer sipServer,
        RegisterHandler registerHandler,
        IHubContext<DeviceStatusHub> hubContext,
        ILogger<SipServerHostedService> logger)
    {
        _sipServer = sipServer;
        _registerHandler = registerHandler;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 订阅设备上线事件，通过 SignalR 推送
        _registerHandler.OnDeviceOnline += async deviceId =>
        {
            await _hubContext.Clients.All.SendAsync("DeviceStatusChanged", deviceId, "Online", cancellationToken);
            _logger.LogInformation("SignalR 推送: 设备 {DeviceId} 上线", deviceId);
        };

        await _sipServer.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _sipServer.StopAsync();
    }
}
