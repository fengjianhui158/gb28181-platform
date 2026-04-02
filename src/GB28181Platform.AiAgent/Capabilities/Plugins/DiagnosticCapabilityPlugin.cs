using GB28181Platform.Domain.Entities;
using Microsoft.SemanticKernel;
using SqlSugar;

namespace GB28181Platform.AiAgent.Capabilities.Plugins;

public class DiagnosticCapabilityPlugin
{
    private readonly ISqlSugarClient _db;

    public DiagnosticCapabilityPlugin(ISqlSugarClient db)
    {
        _db = db;
    }

    [KernelFunction("get_diagnostic_logs")]
    public async Task<string> GetDiagnosticLogsAsync(string deviceId)
    {
        var logs = await _db.Queryable<DiagnosticLog>()
            .Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(10)
            .ToListAsync();

        if (logs.Count == 0)
        {
            return "没有诊断日志";
        }

        return string.Join('\n', logs.Select(x =>
            $"{x.CreatedAt:yyyy-MM-dd HH:mm:ss} [{x.StepName}] success={x.Success} detail={x.Detail} durationMs={x.DurationMs}"));
    }
}
