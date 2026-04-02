using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Multimodal;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AgentInputNormalizerTests
{
    [Fact]
    public void Normalize_TextAndImage_PreservesOrderAndKinds()
    {
        var request = new AgentChatRequest
        {
            ConversationId = "conv-001",
            ContentItems =
            [
                new AgentContentItemDto { Kind = "text", Text = "检查这台设备" },
                new AgentContentItemDto { Kind = "image", FileName = "snap.png", MediaType = "image/png", Base64Data = "ZmFrZQ==" }
            ]
        };

        var result = AgentInputNormalizer.Normalize(request);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("text", result.Items[0].Kind);
        Assert.Equal("image", result.Items[1].Kind);
    }

    [Fact]
    public void Normalize_AudioFile_RequiresMediaTypeAndPayload()
    {
        var request = new AgentChatRequest
        {
            ContentItems =
            [
                new AgentContentItemDto { Kind = "audio", FileName = "voice.wav", MediaType = "audio/wav", Base64Data = "UklGRg==" }
            ]
        };

        var result = AgentInputNormalizer.Normalize(request);

        Assert.Single(result.Items);
        Assert.Equal("audio", result.Items[0].Kind);
        Assert.Equal("audio/wav", result.Items[0].MediaType);
    }
}
