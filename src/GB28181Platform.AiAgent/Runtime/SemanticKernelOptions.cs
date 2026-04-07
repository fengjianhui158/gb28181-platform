namespace GB28181Platform.AiAgent.Runtime;

public class SemanticKernelOptions
{
    public SemanticKernelEndpointOptions Text { get; set; } = new();
    public SemanticKernelEndpointOptions Vision { get; set; } = new();
    public SemanticKernelEndpointOptions Audio { get; set; } = new();
}

public class SemanticKernelEndpointOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
