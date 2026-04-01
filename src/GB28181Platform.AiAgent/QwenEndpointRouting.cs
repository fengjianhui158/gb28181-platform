using Microsoft.Extensions.Configuration;

namespace GB28181Platform.AiAgent;

public sealed class QwenEndpointRouting
{
    public required QwenEndpointOptions Text { get; init; }
    public required QwenEndpointOptions Vision { get; init; }
    public bool HasDedicatedVisionEndpoint { get; init; }

    public static QwenEndpointRouting FromConfiguration(IConfiguration config)
    {
        var text = new QwenEndpointOptions
        {
            BaseUrl = (config["QwenApi:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/'),
            ApiKey = config["QwenApi:ApiKey"] ?? "",
            Model = config["QwenApi:Model"] ?? "qwen-3.5"
        };

        var visionBaseUrl = config["QwenApi:VisionBaseUrl"];
        var hasDedicatedVisionEndpoint = !string.IsNullOrWhiteSpace(visionBaseUrl);

        var vision = new QwenEndpointOptions
        {
            BaseUrl = hasDedicatedVisionEndpoint ? visionBaseUrl!.TrimEnd('/') : text.BaseUrl,
            ApiKey = hasDedicatedVisionEndpoint ? (config["QwenApi:VisionApiKey"] ?? "") : text.ApiKey,
            Model = hasDedicatedVisionEndpoint ? (config["QwenApi:VisionModel"] ?? text.Model) : text.Model
        };

        return new QwenEndpointRouting
        {
            Text = text,
            Vision = vision,
            HasDedicatedVisionEndpoint = hasDedicatedVisionEndpoint
        };
    }
}

public sealed class QwenEndpointOptions
{
    public string BaseUrl { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "";
}
