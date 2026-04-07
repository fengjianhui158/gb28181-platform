using Microsoft.Extensions.Configuration;

namespace GB28181Platform.AiAgent.Runtime;

public static class SemanticKernelModelRouter
{
    public static SemanticKernelOptions FromConfiguration(IConfiguration configuration)
    {
        var legacyBaseUrl = configuration["QwenApi:BaseUrl"] ?? string.Empty;
        var legacyApiKey = configuration["QwenApi:ApiKey"] ?? string.Empty;
        var legacyModel = configuration["QwenApi:Model"] ?? string.Empty;

        return new SemanticKernelOptions
        {
            Text = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:TextModel:BaseUrl"] ?? legacyBaseUrl,
                ApiKey = configuration["SemanticKernel:TextModel:ApiKey"] ?? legacyApiKey,
                Model = configuration["SemanticKernel:TextModel:Model"] ?? legacyModel
            },
            Vision = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:VisionModel:BaseUrl"] ?? configuration["SemanticKernel:TextModel:BaseUrl"] ?? legacyBaseUrl,
                ApiKey = configuration["SemanticKernel:VisionModel:ApiKey"] ?? configuration["SemanticKernel:TextModel:ApiKey"] ?? legacyApiKey,
                Model = configuration["SemanticKernel:VisionModel:Model"] ?? configuration["SemanticKernel:TextModel:Model"] ?? legacyModel
            },
            Audio = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:AudioModel:BaseUrl"] ?? string.Empty,
                ApiKey = configuration["SemanticKernel:AudioModel:ApiKey"] ?? string.Empty,
                Model = configuration["SemanticKernel:AudioModel:Model"] ?? string.Empty
            }
        };
    }
}
