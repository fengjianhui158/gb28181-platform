using GB28181Platform.Sip.Server;
using GB28181Platform.Sip.Sessions;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.Sip.Handlers;

public class InviteHandler
{
    private readonly Gb28181SipServer _sipServer;
    private readonly DeviceSessionManager _sessionManager;
    private readonly SipServerOptions _options;
    private readonly ILogger<InviteHandler> _logger;

    public InviteHandler(Gb28181SipServer sipServer, DeviceSessionManager sessionManager,
        SipServerOptions options, ILogger<InviteHandler> logger)
    {
        _sipServer = sipServer;
        _sessionManager = sessionManager;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 向摄像机发送 INVITE 请求拉流
    /// </summary>
    public Task<bool> SendInviteAsync(string deviceId, string channelId, string mediaIp, int mediaPort)
    {
        var session = _sessionManager.Get(deviceId);
        if (session == null)
        {
            _logger.LogWarning("设备 {DeviceId} 不在线，无法拉流", deviceId);
            return Task.FromResult(false);
        }

        // 构建 SDP (PS 流, RTP over UDP)
        var sdp = $@"v=0
o={_options.ServerId} 0 0 IN IP4 {mediaIp}
s=Play
c=IN IP4 {mediaIp}
t=0 0
m=video {mediaPort} RTP/AVP 96
a=recvonly
a=rtpmap:96 PS/90000
y=0100000001";

        // 通过 SIP 发送 INVITE
        var targetUri = session.RemoteEndPoint;
        _logger.LogInformation("发送 INVITE 到 {DeviceId}/{ChannelId}, 媒体地址 {Ip}:{Port}",
            deviceId, channelId, mediaIp, mediaPort);

        // TODO: 使用 sipsorcery SIPTransport 发送 INVITE 事务
        // 这里先记录流程，后续完善 SIP 事务处理
        return Task.FromResult(true);
    }
}
