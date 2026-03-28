using SIPSorcery.SIP;

namespace GB28181Platform.Sip.Sessions;

public class DeviceSession
{
    public string DeviceId { get; set; } = string.Empty;
    public SIPEndPoint RemoteEndPoint { get; set; } = null!;
    public SIPEndPoint LocalEndPoint { get; set; } = null!;
    public DateTime LastKeepaliveAt { get; set; } = DateTime.Now;
    public DateTime RegisteredAt { get; set; } = DateTime.Now;
    public string RemoteIp { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string Transport { get; set; } = "UDP";
}
