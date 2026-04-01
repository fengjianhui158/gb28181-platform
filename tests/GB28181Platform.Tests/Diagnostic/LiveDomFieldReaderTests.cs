using GB28181Platform.Diagnostic.Browser.Interactive;
using GB28181Platform.Diagnostic.Browser.VisibleField;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class LiveDomFieldReaderTests
{
    [Fact]
    public void ExtractFromHtmlSnapshot_CombinesSplitIpInputsIntoSingleValue()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = ["SIP服务器IP"],
            ["SipServerId"] = ["SIP服务器编号"],
            ["Enable"] = ["接入使能"]
        });

        var reader = new LiveDomFieldReader(extractor);
        var html = """
        <div class="ui-form-item">
          <label>SIP服务器IP</label>
          <div class="u-input-group u-ip">
            <input value="188" />
            <input value="18" />
            <input value="33" />
            <input value="138" />
          </div>
        </div>
        """;

        var result = reader.ExtractFromHtmlSnapshot(html);

        Assert.Equal("188.18.33.138", result.Config.SipServerIp);
    }
}
