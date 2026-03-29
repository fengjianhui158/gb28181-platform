using GB28181Platform.Infrastructure.MediaServer;
using GB28181Platform.Sip.Handlers;
using GB28181Platform.Sip.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.Application.Streams;

public class StreamAppService : IStreamAppService
{
    private readonly IZlmClient _zlm;
    private readonly InviteHandler _invite;
    private readonly DeviceSessionManager _sessionManager;
    private readonly IConfiguration _config;
    private readonly ILogger<StreamAppService> _logger;

    public StreamAppService(IZlmClient zlm, InviteHandler invite, DeviceSessionManager sessionManager,
        IConfiguration config, ILogger<StreamAppService> logger)
    {
        _zlm = zlm;
        _invite = invite;
        _sessionManager = sessionManager;
        _config = config;
        _logger = logger;
    }

    public async Task<PlayResult> PlayAsync(string deviceId, string channelId)
    {
        var streamId = $"{deviceId}_{channelId}";

        // 1. 向 ZLMediaKit 申请 RTP 端口
        var rtp = await _zlm.OpenRtpServerAsync(streamId);
        if (rtp == null || rtp.Code != 0)
            return new PlayResult { Success = false, Message = "ZLMediaKit openRtpServer 失败" };

        // 2. 获取 ZLMediaKit 的媒体 IP，未配置时回退到设备注册到本机时的实际地址
        var session = _sessionManager.Get(deviceId);
        var fallbackIp = session?.LocalEndPoint.GetIPEndPoint().Address.ToString() ?? "127.0.0.1";
        var mediaIp = ResolveMediaIp(_config["ZLMediaKit:MediaIp"], fallbackIp);

        // 3. 向摄像机发送 INVITE
        var ok = await _invite.SendInviteAsync(deviceId, channelId, mediaIp, rtp.Port);
        if (!ok)
        {
            await _zlm.CloseRtpServerAsync(streamId);
            return new PlayResult { Success = false, Message = "INVITE 发送失败" };
        }

        // 4. 返回 WebRTC 播放地址
        var webRtcUrl = _zlm.GetWebRtcPlayUrl("rtp", streamId);
        return new PlayResult { Success = true, WebRtcUrl = webRtcUrl };
    }

    public async Task<bool> StopAsync(string deviceId, string channelId)
    {
        var streamId = $"{deviceId}_{channelId}";

        // 1. 先发 BYE 通知摄像机停止推流
        await _invite.SendByeAsync(streamId);

        // 2. 关闭 ZLMediaKit RTP 端口
        return await _zlm.CloseRtpServerAsync(streamId);
    }

    internal static string ResolveMediaIp(string? configuredMediaIp, string fallbackIp)
    {
        return string.IsNullOrWhiteSpace(configuredMediaIp) ? fallbackIp : configuredMediaIp;
    }
}
