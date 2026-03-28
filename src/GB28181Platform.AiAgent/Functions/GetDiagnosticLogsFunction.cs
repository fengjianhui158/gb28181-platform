using System.Text.Json;
using GB28181Platform.Domain.Entities;
using SqlSugar;

namespace GB28181Platform.AiAgent.Functions;

public class GetDiagnosticLogsFunction : IAgentFunction
{
    private readonly ISqlSugarClient _db;

    public GetDiagnosticLogsFunction(ISqlSugarClient db) => _db = db;

    public string Name => "get_diagnostic_logs";
    public string Description => "查询指定设备最近的诊断日志，包括Ping、端口检测、浏览器检查的结果";
    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            device_id = new { type = "string", description = "设备编码" },
            limit = new { type = "integer", description = "返回条数，默认10" }
        },
        required = new[] { "device_id" }
    };

    public async Task<string> ExecuteAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        var deviceId = args.GetProperty("device_id").GetString() ?? "";
        var limit = args.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

        var logs = await _db.Queryable<DiagnosticLog>()
            .Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(limit)
            .ToListAsync();

        return JsonSerializer.Serialize(logs.Select(x => new
        {
            x.StepName,
            x.Success,
            x.Detail,
            x.DurationMs,
            x.CreatedAt
        }));
    }
}
