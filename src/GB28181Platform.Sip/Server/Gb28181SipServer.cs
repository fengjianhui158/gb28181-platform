using System.Net;
using System.Text;
using GB28181Platform.Sip.Handlers;
using GB28181Platform.Sip.Sessions;
using GB28181Platform.Sip.Xml.Parsers;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;

namespace GB28181Platform.Sip.Server;

public class Gb28181SipServer
{
    private SIPTransport _sipTransport = null!;
    private readonly SipServerOptions _options;
    private readonly RegisterHandler _registerHandler;
    private readonly KeepaliveHandler _keepaliveHandler;
    private readonly DeviceSessionManager _sessionManager;
    private readonly ILogger<Gb28181SipServer> _logger;

    public Gb28181SipServer(
        SipServerOptions options,
        RegisterHandler registerHandler,
        KeepaliveHandler keepaliveHandler,
        DeviceSessionManager sessionManager,
        ILogger<Gb28181SipServer> logger)
    {
        _options = options;
        _registerHandler = registerHandler;
        _keepaliveHandler = keepaliveHandler;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public Task StartAsync()
    {
        _sipTransport = new SIPTransport();

        var listenEP = new IPEndPoint(IPAddress.Parse(_options.ListenIp), _options.Port);

        // UDP 通道
        _sipTransport.AddSIPChannel(new SIPUDPChannel(listenEP));
        // TCP 通道
        _sipTransport.AddSIPChannel(new SIPTCPChannel(listenEP));

        _sipTransport.SIPTransportRequestReceived += OnRequestReceived;

        _logger.LogInformation("GB28181 SIP 服务器已启动, 监听 {Ip}:{Port} (UDP+TCP), ServerId={ServerId}",
            _options.ListenIp, _options.Port, _options.ServerId);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _sipTransport?.Shutdown();
        _logger.LogInformation("GB28181 SIP 服务器已停止");
        return Task.CompletedTask;
    }

    private async Task OnRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
    {
        try
        {
            switch (sipRequest.Method)
            {
                case SIPMethodsEnum.REGISTER:
                    var regResponse = await _registerHandler.HandleAsync(sipRequest, remoteEndPoint, localSIPEndPoint);
                    await _sipTransport.SendResponseAsync(regResponse);
                    break;

                case SIPMethodsEnum.MESSAGE:
                    await HandleMessageAsync(sipRequest, remoteEndPoint);
                    break;

                case SIPMethodsEnum.BYE:
                    var byeResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await _sipTransport.SendResponseAsync(byeResponse);
                    break;

                case SIPMethodsEnum.ACK:
                    // ACK 不需要响应
                    break;

                default:
                    var notAllowed = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
                    await _sipTransport.SendResponseAsync(notAllowed);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 SIP 请求异常: {Method} from {Remote}", sipRequest.Method, remoteEndPoint);
        }
    }

    private async Task HandleMessageAsync(SIPRequest request, SIPEndPoint remoteEP)
    {
        // 先回 200 OK
        var okResponse = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
        await _sipTransport.SendResponseAsync(okResponse);

        // 解析 XML 内容
        var body = request.Body;
        if (string.IsNullOrEmpty(body)) return;

        var rootName = ManscdpParser.GetRootName(body);
        var cmdType = ManscdpParser.GetCmdType(body);
        var deviceId = ManscdpParser.GetDeviceId(body);

        if (string.IsNullOrEmpty(deviceId)) return;

        _logger.LogDebug("MESSAGE: Root={Root}, CmdType={CmdType}, DeviceId={DeviceId}", rootName, cmdType, deviceId);

        switch (cmdType)
        {
            case "Keepalive":
                await _keepaliveHandler.HandleAsync(deviceId);
                break;

            case "Catalog":
                // 目录响应，解析通道列表
                if (rootName == "Response")
                {
                    await HandleCatalogResponseAsync(body, deviceId);
                }
                break;

            case "Alarm":
                _logger.LogWarning("收到报警: DeviceId={DeviceId}", deviceId);
                // TODO: 报警处理
                break;

            default:
                _logger.LogDebug("未处理的 MESSAGE 类型: CmdType={CmdType}", cmdType);
                break;
        }
    }

    private Task HandleCatalogResponseAsync(string xmlBody, string deviceId)
    {
        var items = ManscdpParser.ParseCatalogResponse(xmlBody);
        _logger.LogInformation("收到目录响应: DeviceId={DeviceId}, 通道数={Count}", deviceId, items.Count);
        // TODO: 保存通道到数据库
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发送 SIP 请求（供外部调用，如 INVITE）
    /// </summary>
    public Task SendRequestAsync(SIPRequest request)
    {
        return _sipTransport.SendRequestAsync(request);
    }

    /// <summary>
    /// 发送 SIP 响应
    /// </summary>
    public Task SendResponseAsync(SIPResponse response)
    {
        return _sipTransport.SendResponseAsync(response);
    }

    public SIPTransport Transport => _sipTransport;
}
