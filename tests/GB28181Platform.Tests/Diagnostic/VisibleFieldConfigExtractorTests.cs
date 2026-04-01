using GB28181Platform.Diagnostic.Browser.VisibleField;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class VisibleFieldConfigExtractorTests
{
    [Fact]
    public void Extract_FromVisibleLabels_MapsAliasesToStandardFields()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = ["SIP服务器地址", "SIP服务器IP", "SIP Server IP"],
            ["SipServerId"] = ["SIP服务器编号"],
            ["Enable"] = ["接入使能"]
        });

        var html = """
        <div class="row">
          <span>SIP服务器地址</span><input value="188.18.35.191" />
          <span>SIP服务器编号</span><input value="34020000002000000001" />
          <span>接入使能</span><input type="checkbox" checked />
        </div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.True(result.IsSuccess);
        Assert.Equal("188.18.35.191", result.Config.SipServerIp);
        Assert.Equal("34020000002000000001", result.Config.SipServerId);
        Assert.True(result.Config.Enable);
        Assert.True(result.MatchedFields >= 3);
    }

    [Fact]
    public void Extract_FromSplitIpInputs_MergesIntoSingleIp()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = ["SIP服务器IP"]
        });

        var html = """
        <div class="row">
          <span>SIP服务器IP</span>
          <input value="188" />
          <span>.</span>
          <input value="18" />
          <span>.</span>
          <input value="35" />
          <span>.</span>
          <input value="191" />
        </div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.Equal("188.18.35.191", result.Config.SipServerIp);
    }

    [Fact]
    public void Extract_FromWrappedSplitIpInputs_MergesIntoSingleIp()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = ["SIP服务器IP"]
        });

        var html = """
        <div class="ui-form-item">
          <label>SIP服务器IP</label>
          <div class="u-input-group u-ip">
            <input value="188" />
            <span>.</span>
            <input value="18" />
            <span>.</span>
            <input value="33" />
            <span>.</span>
            <input value="138" />
          </div>
          <label>SIP服务器端口</label>
          <input value="5060" />
        </div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.Equal("188.18.33.138", result.Config.SipServerIp);
    }

    [Fact]
    public void Extract_FromSelectAndInput_PreservesDisplayValues()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["Heartbeat"] = ["心跳周期"],
            ["RegisterExpiry"] = ["注册有效期"],
            ["SipServerPort"] = ["SIP服务器端口"]
        });

        var html = """
        <div class="row"><span>心跳周期</span><input value="60" /></div>
        <div class="row"><span>注册有效期</span><input value="3600" /></div>
        <div class="row"><span>SIP服务器端口</span><select><option>5060</option><option selected>15060</option></select></div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.Equal("60", result.Config.Heartbeat);
        Assert.Equal("3600", result.Config.RegisterExpiry);
        Assert.Equal("15060", result.Config.SipServerPort);
    }
}
