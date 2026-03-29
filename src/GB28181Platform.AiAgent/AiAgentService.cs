using GB28181Platform.AiAgent.Functions;
using GB28181Platform.AiAgent.Prompts;
using GB28181Platform.Domain.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace GB28181Platform.AiAgent;

public class AiAgentService : IAiAgentService
{
    private readonly IQwenClient _qwen;
    private readonly FunctionRegistry _registry;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<AiAgentService> _logger;
    private const int MaxFunctionCalls = 5;

    public AiAgentService(IQwenClient qwen, FunctionRegistry registry,
        ISqlSugarClient db, ILogger<AiAgentService> logger)
    {
        _qwen = qwen;
        _registry = registry;
        _db = db;
        _logger = logger;
    }

    public async Task<string> ChatAsync(string sessionId, string userMessage, string? deviceId = null)
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = SystemPrompts.DiagnosticAssistant },
            new() { Role = "user", Content = userMessage }
        };

        var functions = _registry.GetDefinitions();
        var callCount = 0;

        while (callCount < MaxFunctionCalls)
        {
            var response = await _qwen.ChatAsync(messages, functions);

            if (response.FunctionCall != null)
            {
                callCount++;
                _logger.LogInformation("AI 调用函数: {Name}({Args})",
                    response.FunctionCall.Name, response.FunctionCall.Arguments);

                var function = _registry.Get(response.FunctionCall.Name);
                var result = function != null
                    ? await function.ExecuteAsync(response.FunctionCall.Arguments)
                    : $"未知函数: {response.FunctionCall.Name}";

                // assistant 消息需要包含 tool_calls（新版格式要求）
                messages.Add(new() { Role = "assistant", Content = null, FunctionCall = response.FunctionCall, ToolCallId = response.ToolCallId });
                // 用 tool 角色回传结果，Name 存 tool_call_id
                messages.Add(new() { Role = "tool", Name = response.ToolCallId ?? response.FunctionCall.Name, Content = result });
            }
            else
            {
                await SaveConversationAsync(sessionId, deviceId, userMessage, response.Content ?? "");
                return response.Content ?? "抱歉，我无法回答这个问题。";
            }
        }

        return "抱歉，分析过程超出了最大调用次数限制。";
    }

    private async Task SaveConversationAsync(string sessionId, string? deviceId, string userMsg, string assistantMsg)
    {
        var records = new List<AiConversation>
        {
            new() { SessionId = sessionId, DeviceId = deviceId, Role = "user", Content = userMsg, CreatedAt = DateTime.Now },
            new() { SessionId = sessionId, DeviceId = deviceId, Role = "assistant", Content = assistantMsg, CreatedAt = DateTime.Now }
        };
        await _db.Insertable(records).ExecuteCommandAsync();
    }
}
