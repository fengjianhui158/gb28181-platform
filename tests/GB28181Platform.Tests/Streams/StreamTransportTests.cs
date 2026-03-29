using GB28181Platform.Application.Streams;
using GB28181Platform.Infrastructure.MediaServer;
using Xunit;

namespace GB28181Platform.Tests.Streams;

public class StreamTransportTests
{
    [Fact]
    public void ResolveMediaIp_UsesConfiguredValue_WhenPresent()
    {
        var result = StreamAppService.ResolveMediaIp("192.168.1.200", "192.168.1.2");

        Assert.Equal("192.168.1.200", result);
    }

    [Fact]
    public void ResolveMediaIp_FallsBackToSessionLocalIp_WhenMissing()
    {
        var result = StreamAppService.ResolveMediaIp(null, "192.168.1.2");

        Assert.Equal("192.168.1.2", result);
    }

    [Fact]
    public void BuildOpenRtpServerUrl_UsesUdpMode()
    {
        var result = ZlmClient.BuildOpenRtpServerUrl("secret", "stream001", 30000);

        Assert.Contains("enable_tcp=0", result);
        Assert.Contains("stream_id=stream001", result);
        Assert.Contains("port=30000", result);
    }
}
