using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GB28181Platform.AiAgent.Runtime;

internal static class SemanticKernelClientFactory
{
    public static HttpClient CreateHttpClient(SemanticKernelEndpointOptions options)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl)
        };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        return client;
    }

    public static IChatCompletionService CreateChatCompletionService(
        SemanticKernelEndpointOptions options,
        ILoggerFactory loggerFactory,
        string serviceId)
    {
        var httpClient = CreateHttpClient(options);
        return new OpenAIChatCompletionService(
            options.Model,
            new Uri(options.BaseUrl),
            options.ApiKey,
            serviceId,
            httpClient,
            loggerFactory);
    }

    public static Kernel CreateKernel(
        SemanticKernelEndpointOptions options,
        ILoggerFactory loggerFactory,
        string serviceId)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(CreateChatCompletionService(options, loggerFactory, serviceId));
        builder.Services.AddSingleton(loggerFactory);
        return builder.Build();
    }
}
