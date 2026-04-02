using Microsoft.Extensions.Configuration;

namespace GB28181Platform.AiAgent.Runtime;

public static class SemanticKernelModelRouter
{
    public static SemanticKernelOptions FromConfiguration(IConfiguration configuration)
    {
        return new SemanticKernelOptions
        {
            Text = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:TextModel:BaseUrl"] ?? string.Empty,
                ApiKey = configuration["SemanticKernel:TextModel:ApiKey"] ?? string.Empty,
                Model = configuration["SemanticKernel:TextModel:Model"] ?? string.Empty
            },
            Vision = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:VisionModel:BaseUrl"] ?? configuration["SemanticKernel:TextModel:BaseUrl"] ?? string.Empty,
                ApiKey = configuration["SemanticKernel:VisionModel:ApiKey"] ?? configuration["SemanticKernel:TextModel:ApiKey"] ?? string.Empty,
                Model = configuration["SemanticKernel:VisionModel:Model"] ?? configuration["SemanticKernel:TextModel:Model"] ?? string.Empty
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
