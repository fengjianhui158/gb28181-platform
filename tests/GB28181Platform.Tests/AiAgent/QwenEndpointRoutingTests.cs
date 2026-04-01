using GB28181Platform.AiAgent;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class QwenEndpointRoutingTests
{
    [Fact]
    public void FromConfiguration_UsesPrimaryModelForVision_WhenVisionOverridesAreMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QwenApi:BaseUrl"] = "https://api.deepseek.com",
                ["QwenApi:ApiKey"] = "primary-key",
                ["QwenApi:Model"] = "deepseek-vl"
            })
            .Build();

        var routing = QwenEndpointRouting.FromConfiguration(config);

        Assert.Equal("https://api.deepseek.com", routing.Text.BaseUrl);
        Assert.Equal("primary-key", routing.Text.ApiKey);
        Assert.Equal("deepseek-vl", routing.Text.Model);
        Assert.Equal("https://api.deepseek.com", routing.Vision.BaseUrl);
        Assert.Equal("primary-key", routing.Vision.ApiKey);
        Assert.Equal("deepseek-vl", routing.Vision.Model);
        Assert.False(routing.HasDedicatedVisionEndpoint);
    }

    [Fact]
    public void FromConfiguration_UsesVisionOverrides_WhenVisionConfigurationIsPresent()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QwenApi:BaseUrl"] = "https://api.deepseek.com",
                ["QwenApi:ApiKey"] = "primary-key",
                ["QwenApi:Model"] = "deepseek-chat",
                ["QwenApi:VisionBaseUrl"] = "https://dashscope.aliyuncs.com/compatible-mode",
                ["QwenApi:VisionApiKey"] = "vision-key",
                ["QwenApi:VisionModel"] = "qwen-vl-max"
            })
            .Build();

        var routing = QwenEndpointRouting.FromConfiguration(config);

        Assert.True(routing.HasDedicatedVisionEndpoint);
        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode", routing.Vision.BaseUrl);
        Assert.Equal("vision-key", routing.Vision.ApiKey);
        Assert.Equal("qwen-vl-max", routing.Vision.Model);
    }
}
