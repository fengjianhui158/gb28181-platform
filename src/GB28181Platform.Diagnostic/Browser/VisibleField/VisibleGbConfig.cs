namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class VisibleGbConfig
{
    public bool? Enable { get; set; }
    public string SipServerId { get; set; } = "";
    public string SipDomain { get; set; } = "";
    public string SipServerIp { get; set; } = "";
    public string SipServerPort { get; set; } = "";
    public string LocalSipPort { get; set; } = "";
    public string RegisterExpiry { get; set; } = "";
    public string Heartbeat { get; set; } = "";
    public string HeartbeatTimeoutCount { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public Dictionary<string, string> RawMatches { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
