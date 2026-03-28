using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace GB28181Platform.AiAgent;

public class QwenClient : IQwenClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    public QwenClient(IConfiguration config)
    {
        var baseUrl = config["QwenApi:BaseUrl"] ?? "http://localhost:8000";
        var apiKey = config["QwenApi:ApiKey"] ?? "";
        _model = config["QwenApi:Model"] ?? "qwen-3.5";

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> messages, List<FunctionDefinition>? functions = null)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["messages"] = messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                name = m.Name,
                function_call = m.FunctionCall == null ? null : new
                {
                    name = m.FunctionCall.Name,
                    arguments = m.FunctionCall.Arguments
                }
            }).ToList()
        };

        if (functions != null && functions.Count > 0)
        {
            body["functions"] = functions;
            body["function_call"] = "auto";
        }

        var response = await _http.PostAsJsonAsync("/v1/chat/completions", body);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var choice = json.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var finishReason = choice.GetProperty("finish_reason").GetString() ?? "";

        var result = new ChatResponse { FinishReason = finishReason };

        if (message.TryGetProperty("function_call", out var fc))
        {
            result.FunctionCall = new FunctionCall
            {
                Name = fc.GetProperty("name").GetString() ?? "",
                Arguments = fc.GetProperty("arguments").GetString() ?? "{}"
            };
        }
        else
        {
            result.Content = message.GetProperty("content").GetString();
        }

        return result;
    }
}
