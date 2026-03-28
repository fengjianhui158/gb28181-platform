using GB28181Platform.Domain.Entities;
using GB28181Platform.Domain.Enums;
using GB28181Platform.Sip.Auth;
using GB28181Platform.Sip.Server;
using GB28181Platform.Sip.Sessions;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;
using SqlSugar;

namespace GB28181Platform.Sip.Handlers;

public class RegisterHandler
{
    private readonly SipServerOptions _options;
    private readonly DeviceSessionManager _sessionManager;
    private readonly SipDigestAuthenticator _authenticator;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<RegisterHandler> _logger;

    /// <summary>
    /// 设备上线事件
    /// </summary>
    public event Func<string, Task>? OnDeviceOnline;

    public RegisterHandler(
        SipServerOptions options,
        DeviceSessionManager sessionManager,
        ISqlSugarClient db,
        ILogger<RegisterHandler> logger)
    {
        _options = options;
        _sessionManager = sessionManager;
        _authenticator = new SipDigestAuthenticator(options.Realm);
        _db = db;
        _logger = logger;
    }

    public async Task<SIPResponse> HandleAsync(SIPRequest request, SIPEndPoint remoteEP, SIPEndPoint localEP)
    {
        var deviceId = request.Header.From.FromURI.User;
        _logger.LogInformation("收到 REGISTER: DeviceId={DeviceId}, Remote={Remote}", deviceId, remoteEP);

        // 注销请求 (Expires=0)
        if (request.Header.Expires == 0)
        {
            return await HandleDeregisterAsync(request, deviceId);
        }

        // 无认证头 → 返回 401
        if (request.Header.AuthenticationHeaders == null || request.Header.AuthenticationHeaders.Count == 0)
        {
            _logger.LogInformation("设备 {DeviceId} 首次注册，返回 401 要求认证", deviceId);
            return _authenticator.CreateUnauthorizedResponse(request);
        }

        // 验证认证
        if (!_authenticator.Authenticate(request, _options.DefaultPassword))
        {
            _logger.LogWarning("设备 {DeviceId} 认证失败", deviceId);
            return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Forbidden, "认证失败");
        }

        // 认证通过，注册设备
        var remoteIp = remoteEP.Address.ToString();
        var remotePort = remoteEP.Port;

        var session = new DeviceSession
        {
            DeviceId = deviceId,
            RemoteEndPoint = remoteEP,
            LocalEndPoint = localEP,
            RemoteIp = remoteIp,
            RemotePort = remotePort,
            Transport = request.Header.Vias.TopViaHeader.Transport.ToString()
        };
        _sessionManager.AddOrUpdate(deviceId, session);

        // 更新数据库
        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == deviceId);
        if (device == null)
        {
            device = new Device
            {
                Id = deviceId,
                RemoteIp = remoteIp,
                RemotePort = remotePort,
                Status = nameof(DeviceStatus.Online),
                LastRegisterAt = DateTime.Now,
                LastKeepaliveAt = DateTime.Now,
                Transport = session.Transport
            };
            await _db.Insertable(device).ExecuteCommandAsync();
        }
        else
        {
            device.RemoteIp = remoteIp;
            device.RemotePort = remotePort;
            device.Status = nameof(DeviceStatus.Online);
            device.LastRegisterAt = DateTime.Now;
            device.LastKeepaliveAt = DateTime.Now;
            device.Transport = session.Transport;
            device.UpdatedAt = DateTime.Now;
            await _db.Updateable(device).ExecuteCommandAsync();
        }

        _logger.LogInformation("设备 {DeviceId} 注册成功 ({Ip}:{Port})", deviceId, remoteIp, remotePort);

        // 触发上线事件
        if (OnDeviceOnline != null)
            await OnDeviceOnline.Invoke(deviceId);

        var response = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
        response.Header.Expires = 3600;
        return response;
    }

    private async Task<SIPResponse> HandleDeregisterAsync(SIPRequest request, string deviceId)
    {
        _logger.LogInformation("设备 {DeviceId} 注销", deviceId);
        _sessionManager.Remove(deviceId);

        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == deviceId);
        if (device != null)
        {
            device.Status = nameof(DeviceStatus.Offline);
            device.UpdatedAt = DateTime.Now;
            await _db.Updateable(device).ExecuteCommandAsync();
        }

        return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
    }
}
