using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.AiAgent;

public class QwenClient : IQwenClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<QwenClient> _logger;

    public QwenClient(IConfiguration config, ILogger<QwenClient> logger)
    {
        var baseUrl = (config["QwenApi:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
        var apiKey = config["QwenApi:ApiKey"] ?? "";
        _model = config["QwenApi:Model"] ?? "qwen-3.5";
        _logger = logger;

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> messages, List<FunctionDefinition>? functions = null)
    {
        // 构建消息，兼容 tool_calls 格式
        var msgList = messages.Select(m =>
        {
            var dict = new Dictionary<string, object> { ["role"] = m.Role };

            if (m.Role == "assistant" && m.FunctionCall != null)
            {
                // assistant 带 tool_calls（新版格式）
                dict["content"] = (object?)m.Content ?? "";
                dict["tool_calls"] = new[] { new {
                    id = m.ToolCallId ?? $"call_{m.FunctionCall.Name}",
                    type = "function",
                    function = new { name = m.FunctionCall.Name, arguments = m.FunctionCall.Arguments }
                }};
            }
            else if (m.Role == "tool")
            {
                // tool 角色回传函数结果
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

        // 使用新版 tools 格式（兼容 DeepSeek、千问、OpenAI）
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
                result.FinishReason = fr.GetString() ?? "";

            if (choice.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
            {
                // 新版: tool_calls 数组
                if (message.TryGetProperty("tool_calls", out var toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array && toolCalls.GetArrayLength() > 0)
                {
                    var tc = toolCalls[0];
                    if (tc.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                    {
                        result.FunctionCall = new FunctionCall
                        {
                            Name = fn.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Arguments = fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"
                        };
                        // 保存 tool_call_id 用于后续消息
                        if (tc.TryGetProperty("id", out var id))
                            result.ToolCallId = id.GetString() ?? "";
                    }
                }
                // 旧版: function_call 对象
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
}
