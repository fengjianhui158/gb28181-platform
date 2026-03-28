namespace GB28181Platform.Sip.Server;

public class SipServerOptions
{
    /// <summary>
    /// SIP 服务器 ID (20位国标编号)
    /// </summary>
    public string ServerId { get; set; } = "34020000002000000001";

    /// <summary>
    /// SIP 域
    /// </summary>
    public string Realm { get; set; } = "3402000000";

    /// <summary>
    /// SIP 监听端口
    /// </summary>
    public int Port { get; set; } = 5060;

    /// <summary>
    /// SIP 监听 IP（0.0.0.0 表示所有）
    /// </summary>
    public string ListenIp { get; set; } = "0.0.0.0";

    /// <summary>
    /// 设备默认密码（用于 SIP 认证）
    /// </summary>
    public string DefaultPassword { get; set; } = "12345678";

    /// <summary>
    /// 心跳超时秒数
    /// </summary>
    public int KeepaliveTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 心跳检测间隔秒数
    /// </summary>
    public int KeepaliveCheckIntervalSeconds { get; set; } = 30;
}
