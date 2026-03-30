using System.Text;
using GB28181Platform.Diagnostic.Browser.VisibleField;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class VisibleFieldNavigationOptionsTests
{
    [Fact]
    public void Bind_VisibleFieldAliases_BindsStandardFieldAliases()
    {
        var json = """
        {
          "Diagnostic": {
            "VisibleFieldAliases": {
              "SipServerIp": [ "SIP服务器地址", "SIP服务器IP", "SIP Server IP" ],
              "Enable": [ "接入使能", "启用" ]
            }
          }
        }
        """;

        var config = BuildConfig(json);
        var options = new VisibleFieldAliasOptions();
        config.GetSection("Diagnostic:VisibleFieldAliases").Bind(options.Fields);

        Assert.Equal(3, options.Fields["SipServerIp"].Count);
        Assert.Contains("接入使能", options.Fields["Enable"]);
    }

    [Fact]
    public void Bind_Manufacturers_BindsMultipleNavigationPaths()
    {
        var json = """
        {
          "Diagnostic": {
            "Manufacturers": {
              "dahua": {
                "NavigationPaths": [
                  [ "设置", "网络设置", "平台接入" ],
                  [ "设置", "网络", "平台接入" ]
                ]
              }
            }
          }
        }
        """;

        var config = BuildConfig(json);
        var options = new ManufacturerNavigationOptions();
        config.GetSection("Diagnostic:Manufacturers").Bind(options.Manufacturers);

        Assert.Equal(2, options.Manufacturers["dahua"].NavigationPaths.Count);
        Assert.Equal("平台接入", options.Manufacturers["dahua"].NavigationPaths[0][2]);
    }

    private static IConfigurationRoot BuildConfig(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build();
    }
}
