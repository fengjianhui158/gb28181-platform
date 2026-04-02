using GB28181Platform.AiAgent.Runtime;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class SemanticKernelModelRouterTests
{
    [Fact]
    public void FromConfiguration_UsesDedicatedVisionAndAudioEndpoints()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SemanticKernel:TextModel:BaseUrl"] = "https://api.deepseek.com",
                ["SemanticKernel:TextModel:Model"] = "deepseek-chat",
                ["SemanticKernel:VisionModel:BaseUrl"] = "https://dashscope.aliyuncs.com/compatible-mode",
                ["SemanticKernel:VisionModel:Model"] = "qwen-vl-max",
                ["SemanticKernel:AudioModel:BaseUrl"] = "https://audio.example.com",
                ["SemanticKernel:AudioModel:Model"] = "whisper-1"
            })
            .Build();

        var options = SemanticKernelModelRouter.FromConfiguration(config);

        Assert.Equal("deepseek-chat", options.Text.Model);
        Assert.Equal("qwen-vl-max", options.Vision.Model);
        Assert.Equal("whisper-1", options.Audio.Model);
    }
}
