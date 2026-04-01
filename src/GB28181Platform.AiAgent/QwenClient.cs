using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.AiAgent;

public class QwenClient : IQwenClient
{
    private readonly HttpClient _http;
    private readonly HttpClient? _visionHttp;
    private readonly string _model;
    private readonly string _visionModel;
    private readonly bool _hasDedicatedVisionEndpoint;
    private readonly ILogger<QwenClient> _logger;

    public QwenClient(IConfiguration config, ILogger<QwenClient> logger)
    {
        var routing = QwenEndpointRouting.FromConfiguration(config);
        _model = routing.Text.Model;
        _visionModel = routing.Vision.Model;
        _hasDedicatedVisionEndpoint = routing.HasDedicatedVisionEndpoint;
        _logger = logger;

        _http = BuildClient(routing.Text);
        _visionHttp = _hasDedicatedVisionEndpoint ? BuildClient(routing.Vision) : null;
    }

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> messages, List<FunctionDefinition>? functions = null)
    {
        var msgList = messages.Select(m =>
        {
            var dict = new Dictionary<string, object> { ["role"] = m.Role };

            if (m.Role == "assistant" && m.FunctionCall != null)
            {
                dict["content"] = (object?)m.Content ?? "";
                dict["tool_calls"] = new[]
                {
                    new
                    {
                        id = m.ToolCallId ?? $"call_{m.FunctionCall.Name}",
                        type = "function",
                        function = new { name = m.FunctionCall.Name, arguments = m.FunctionCall.Arguments }
                    }
                };
            }
            else if (m.Role == "tool")
            {
                dict["tool_call_id"] = m.Name ?? "";
                dict["content"] = m.Content ?? "";
            }
            else
            {
                if (m.Content != null) dict["content"] = m.Content;
                if (m.Name != null) dict["name"] = m.Name;
            }

            return dict;
        }).ToList();

        var body = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["messages"] = msgList
        };

        if (functions != null && functions.Count > 0)
        {
            var tools = functions.Select(f => new
            {
                type = "function",
                function = new
                {
                    name = f.Name,
                    description = f.Description,
                    parameters = f.Parameters
                }
            }).ToList();
            body["tools"] = tools;
            body["tool_choice"] = "auto";
        }

        var response = await _http.PostAsJsonAsync("/v1/chat/completions", body);
        var rawJson = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Qwen API 原始响应: {Response}", rawJson);

        response.EnsureSuccessStatusCode();

        var json = JsonSerializer.Deserialize<JsonElement>(rawJson);
        var result = new ChatResponse();

        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];

            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String)
            {
                result.FinishReason = fr.GetString() ?? "";
            }

            if (choice.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
            {
                if (message.TryGetProperty("tool_calls", out var toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array &&
                    toolCalls.GetArrayLength() > 0)
                {
                    var tc = toolCalls[0];
                    if (tc.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                    {
                        result.FunctionCall = new FunctionCall
                        {
                            Name = fn.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Arguments = fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"
                        };

                        if (tc.TryGetProperty("id", out var id))
                        {
                            result.ToolCallId = id.GetString() ?? "";
                        }
                    }
                }
                else if (message.TryGetProperty("function_call", out var fc) && fc.ValueKind == JsonValueKind.Object)
                {
                    result.FunctionCall = new FunctionCall
                    {
                        Name = fc.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Arguments = fc.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"
                    };
                }
                else if (message.TryGetProperty("content", out var content))
                {
                    result.Content = content.ValueKind == JsonValueKind.String ? content.GetString() : content.ToString();
                }
            }
        }

        if (result.Content == null && result.FunctionCall == null)
        {
            _logger.LogWarning("Qwen API 返回格式异常: {Response}", rawJson);
            result.Content = "AI 返回了空内容，请重试。";
        }

        return result;
    }

    public async Task<ChatResponse> ChatWithImageAsync(string prompt, string imageBase64)
    {
        var client = _visionHttp ?? _http;
        var model = _hasDedicatedVisionEndpoint ? _visionModel : _model;

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new Dictionary<string, string>
                            {
                                ["url"] = $"data:image/png;base64,{imageBase64}"
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = prompt
                        }
                    }
                }
            }
        };

        _logger.LogInformation("调用视觉模型 {Model} 分析图片，prompt 长度: {Len}", model, prompt.Length);

        var response = await client.PostAsJsonAsync("/v1/chat/completions", body);
        var rawJson = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("视觉模型响应长度: {Len}", rawJson.Length);

        response.EnsureSuccessStatusCode();

        var json = JsonSerializer.Deserialize<JsonElement>(rawJson);
        var result = new ChatResponse();

        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                result.Content = content.ValueKind == JsonValueKind.String ? content.GetString() : content.ToString();
            }
        }

        if (result.Content == null)
        {
            _logger.LogWarning("视觉模型返回格式异常: {Response}", rawJson[..Math.Min(500, rawJson.Length)]);
            result.Content = "视觉模型未返回有效内容。";
        }

        return result;
    }

    private static HttpClient BuildClient(QwenEndpointOptions options)
    {
        var client = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        }

        return client;
    }
}
