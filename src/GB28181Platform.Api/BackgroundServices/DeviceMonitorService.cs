using System.Threading.Channels;
using GB28181Platform.Api.BackgroundServices;
using GB28181Platform.Api.Hubs;
using GB28181Platform.Domain.Entities;
using GB28181Platform.Domain.Enums;
using GB28181Platform.Sip.Server;
using GB28181Platform.Sip.Sessions;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;

namespace GB28181Platform.Api.BackgroundServices;

/// <summary>
/// 设备心跳监控后台服务 — 检测心跳超时设备并标记离线
/// </summary>
public class DeviceMonitorService : BackgroundService
{
    private readonly DeviceSessionManager _sessionManager;
    private readonly SipServerOptions _options;
    private readonly ISqlSugarClient _db;
    private readonly IHubContext<DeviceStatusHub> _hubContext;
    private readonly Channel<DiagnosticRequest> _diagnosticQueue;
    private readonly ILogger<DeviceMonitorService> _logger;

    public DeviceMonitorService(
        DeviceSessionManager sessionManager,
        SipServerOptions options,
        ISqlSugarClient db,
        IHubContext<DeviceStatusHub> hubContext,
        Channel<DiagnosticRequest> diagnosticQueue,
        ILogger<DeviceMonitorService> logger)
    {
        _sessionManager = sessionManager;
        _options = options;
        _db = db;
        _hubContext = hubContext;
        _diagnosticQueue = diagnosticQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("设备监控服务已启动, 检测间隔={Interval}s, 超时={Timeout}s",
            _options.KeepaliveCheckIntervalSeconds, _options.KeepaliveTimeoutSeconds);

        // 启动时：把数据库中所有 Online 但内存中没有 session 的设备标记为 Offline
        // 解决平台重启后，设备未重新注册但数据库状态仍为 Online 的问题
        try
        {
            var onlineDevices = await _db.Queryable<Device>()
                .Where(d => d.Status == nameof(DeviceStatus.Online))
                .ToListAsync();

            var resetCount = 0;
            foreach (var device in onlineDevices)
            {
                if (_sessionManager.Get(device.Id) == null)
                {
                    device.Status = nameof(DeviceStatus.Offline);
                    device.UpdatedAt = DateTime.Now;
                    await _db.Updateable(device).ExecuteCommandAsync();
                    resetCount++;
                    _logger.LogInformation("启动同步: 设备 {DeviceId} 无活跃会话，状态重置为 Offline", device.Id);
                }
            }

            if (resetCount > 0)
                _logger.LogInformation("启动同步完成: 共 {Count} 台设备状态重置为 Offline", resetCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动时设备状态同步失败");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var expiredSessions = _sessionManager.GetExpiredSessions(_options.KeepaliveTimeoutSeconds);

                foreach (var session in expiredSessions)
                {
                    _logger.LogWarning("设备 {DeviceId} 心跳超时, 标记离线", session.DeviceId);

                    // 移除会话
                    _sessionManager.Remove(session.DeviceId);

                    // 更新数据库
                    var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == session.DeviceId);
                    if (device != null && device.Status == nameof(DeviceStatus.Online))
                    {
                        device.Status = nameof(DeviceStatus.Offline);
                        device.UpdatedAt = DateTime.Now;
                        await _db.Updateable(device).ExecuteCommandAsync();

                        // SignalR 推送
                        await _hubContext.Clients.All.SendAsync("DeviceStatusChanged",
                            session.DeviceId, "Offline", stoppingToken);

                        // 自动触发诊断
                        var diagTask = new DiagnosticTask
                        {
                            DeviceId = session.DeviceId,
                            TriggerType = "AUTO",
                            Status = "PENDING"
                        };
                        diagTask.Id = await _db.Insertable(diagTask).ExecuteReturnIdentityAsync();
                        await _diagnosticQueue.Writer.WriteAsync(
                            new DiagnosticRequest { TaskId = diagTask.Id, DeviceId = session.DeviceId },
                            stoppingToken);
                        _logger.LogInformation("已为离线设备 {DeviceId} 创建自动诊断任务 {TaskId}",
                            session.DeviceId, diagTask.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备监控检测异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.KeepaliveCheckIntervalSeconds), stoppingToken);
        }
    }
}
