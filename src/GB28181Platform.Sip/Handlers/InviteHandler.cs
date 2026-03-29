using System.Collections.Concurrent;
using System.Net.Sockets;
using GB28181Platform.Sip.Server;
using GB28181Platform.Sip.Sessions;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;

namespace GB28181Platform.Sip.Handlers;

public class InviteHandler
{
    private readonly Gb28181SipServer _sipServer;
    private readonly DeviceSessionManager _sessionManager;
    private readonly SipServerOptions _options;
    private readonly ILogger<InviteHandler> _logger;

    /// <summary>活跃流会话，key = streamId (deviceId_channelId)</summary>
    private readonly ConcurrentDictionary<string, InviteDialog> _dialogs = new();

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
    public async Task<bool> SendInviteAsync(string deviceId, string channelId, string mediaIp, int mediaPort)
    {
        var session = _sessionManager.Get(deviceId);
        if (session == null)
        {
            _logger.LogWarning("设备 {DeviceId} 不在线，无法拉流", deviceId);
            return false;
        }

        var streamId = $"{deviceId}_{channelId}";

        // SSRC: 0 + channelId 后 9 位
        var ssrc = $"0{channelId.Substring(channelId.Length - 9)}";

        // 本机 SIP 地址（从设备注册时记录的 LocalEndPoint 获取）
        var localIp = session.LocalEndPoint.GetIPEndPoint().Address.ToString();
        var localUri = SIPURI.ParseSIPURIRelaxed($"sip:{_options.ServerId}@{localIp}:{_options.Port}");
        var remoteUri = SIPURI.ParseSIPURIRelaxed($"sip:{channelId}@{session.RemoteIp}:{session.RemotePort}");

        var fromTag = CallProperties.CreateNewTag();
        var callId = CallProperties.CreateNewCallId();

        // 构建 SDP（GB28181 PS 流, recvonly）
        var sdp = $"v=0\r\n" +
                  $"o={_options.ServerId} 0 0 IN IP4 {mediaIp}\r\n" +
                  $"s=Play\r\n" +
                  $"c=IN IP4 {mediaIp}\r\n" +
                  $"t=0 0\r\n" +
                  $"m=video {mediaPort} RTP/AVP 96\r\n" +
                  $"a=recvonly\r\n" +
                  $"a=rtpmap:96 PS/90000\r\n" +
                  $"y={ssrc}\r\n";

        // 构建 INVITE 请求
        var request = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, remoteUri,
            new SIPToHeader(null, remoteUri, null),
            new SIPFromHeader(null, localUri, fromTag));
        request.Header.CallId = callId;
        request.Header.Contact = new List<SIPContactHeader> { new SIPContactHeader(null, localUri) };
        request.Header.ContentType = "APPLICATION/SDP";
        request.Header.Subject = $"{channelId}:{ssrc},{_options.ServerId}:0";
        request.Header.UserAgent = "GB28181Platform";
        request.Body = sdp;

        _logger.LogInformation("发送 INVITE: {DeviceId}/{ChannelId} → {RemoteIp}:{RemotePort}, 媒体 {MediaIp}:{MediaPort}",
            deviceId, channelId, session.RemoteIp, session.RemotePort, mediaIp, mediaPort);

        // 通过 UACInviteTransaction 发送（自动重传 + 自动发送 ACK）
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transaction = new UACInviteTransaction(_sipServer.Transport, request, session.RemoteEndPoint);

        transaction.UACInviteTransactionFinalResponseReceived += (localEP, remoteEP, txn, response) =>
        {
            if (response.StatusCode >= 200 && response.StatusCode <= 299)
            {
                // 200 OK — ACK 由 UACInviteTransaction 自动发送
                _dialogs[streamId] = new InviteDialog
                {
                    CallId = callId,
                    FromTag = fromTag,
                    ToTag = response.Header.To.ToTag,
                    RemoteUri = remoteUri,
                    LocalUri = localUri,
                    CSeq = 1,
                    RemoteEndPoint = session.RemoteEndPoint
                };
                _logger.LogInformation("INVITE 成功, 流会话已建立: {StreamId}", streamId);
                tcs.TrySetResult(true);
            }
            else
            {
                _logger.LogWarning("INVITE 失败: {StatusCode} {Reason}", response.StatusCode, response.ReasonPhrase);
                tcs.TrySetResult(false);
            }
            return Task.FromResult(SocketError.Success);
        };

        transaction.UACInviteTransactionFailed += (txn, reason) =>
        {
            _logger.LogWarning("INVITE 失败: {StreamId}, 原因: {Reason}", streamId, reason);
            tcs.TrySetResult(false);
        };

        transaction.SendInviteRequest();

        // 等待响应，最多 10 秒
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => tcs.TrySetResult(false));

        return await tcs.Task;
    }

    /// <summary>
    /// 发送 BYE 挂断流会话
    /// </summary>
    public async Task SendByeAsync(string streamId)
    {
        if (!_dialogs.TryRemove(streamId, out var dialog))
        {
            _logger.LogDebug("未找到活跃会话: {StreamId}, 跳过 BYE", streamId);
            return;
        }

        var byeRequest = SIPRequest.GetRequest(SIPMethodsEnum.BYE, dialog.RemoteUri,
            new SIPToHeader(null, dialog.RemoteUri, dialog.ToTag),
            new SIPFromHeader(null, dialog.LocalUri, dialog.FromTag));
        byeRequest.Header.CallId = dialog.CallId;
        byeRequest.Header.CSeq = dialog.CSeq + 1;
        byeRequest.Header.Contact = new List<SIPContactHeader> { new SIPContactHeader(null, dialog.LocalUri) };

        try
        {
            await _sipServer.Transport.SendRequestAsync(dialog.RemoteEndPoint, byeRequest);
            _logger.LogInformation("已发送 BYE: {StreamId}", streamId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送 BYE 失败: {StreamId}", streamId);
        }
    }
}

/// <summary>活跃 INVITE 会话信息（用于发送 BYE）</summary>
internal class InviteDialog
{
    public string CallId { get; init; } = string.Empty;
    public string FromTag { get; init; } = string.Empty;
    public string ToTag { get; init; } = string.Empty;
    public SIPURI RemoteUri { get; init; } = null!;
    public SIPURI LocalUri { get; init; } = null!;
    public int CSeq { get; init; }
    public SIPEndPoint RemoteEndPoint { get; init; } = null!;
}
