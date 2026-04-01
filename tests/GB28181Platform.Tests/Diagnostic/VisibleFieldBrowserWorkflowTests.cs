using GB28181Platform.Diagnostic.Browser.VisibleField;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class VisibleFieldBrowserWorkflowTests
{
    [Fact]
    public void ResolveNavigationPaths_PrefersExactManufacturerAndFallsBackToFuzzyMatch()
    {
        var options = new ManufacturerNavigationOptions
        {
            Manufacturers = new Dictionary<string, ManufacturerNavigationDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["dahua"] = new()
                {
                    NavigationPaths =
                    [
                        ["设置", "网络设置", "平台接入"]
                    ]
                },
                ["hikvision"] = new()
                {
                    NavigationPaths =
                    [
                        ["配置", "网络", "高级配置", "平台接入"]
                    ]
                }
            }
        };

        var exactPaths = VisibleFieldNavigationResolver.Resolve(options, "hikvision");
        var fuzzyPaths = VisibleFieldNavigationResolver.Resolve(options, "hik");

        Assert.Single(exactPaths);
        Assert.Equal("平台接入", exactPaths[0][3]);
        Assert.Single(fuzzyPaths);
        Assert.Equal("平台接入", fuzzyPaths[0][3]);
    }

    [Fact]
    public void EvaluatePage_SucceedsWhenVisibleFieldsMeetThreshold()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["Enable"] = ["接入使能"],
            ["SipServerIp"] = ["SIP服务器地址"],
            ["SipServerId"] = ["SIP服务器编号"]
        });

        const string html = """
        <div class='form-row'>
          <label>接入使能</label>
          <input type='checkbox' checked='checked' />
        </div>
        <div class='form-row'>
          <label>SIP服务器地址</label>
          <input type='text' value='188.18.35.191' />
        </div>
        <div class='form-row'>
          <label>SIP服务器编号</label>
          <input type='text' value='34020000002000000001' />
        </div>
        """;

        var result = VisibleFieldPageDetector.Evaluate(extractor, html);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.MatchedFields);
        Assert.Equal("188.18.35.191", result.Config.SipServerIp);
    }

    [Fact]
    public void EvaluatePage_FailsWhenVisibleFieldsDoNotMeetThreshold()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = ["SIP服务器地址"],
            ["SipServerId"] = ["SIP服务器编号"]
        });

        const string html = """
        <div class='form-row'>
          <label>SIP服务器地址</label>
          <input type='text' value='188.18.35.191' />
        </div>
        <div class='form-row'>
          <label>其他字段</label>
          <input type='text' value='noop' />
        </div>
        """;

        var result = VisibleFieldPageDetector.Evaluate(extractor, html);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.MatchedFields);
        Assert.Equal("页面命中标准字段数量不足", result.FailureReason);
    }
}
