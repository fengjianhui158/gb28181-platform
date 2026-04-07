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
        var latestTask = await _db.Queryable<DiagnosticTask>()
            .Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .FirstAsync();

        if (latestTask == null)
        {
            return "没有诊断任务";
        }

        var logs = await _db.Queryable<DiagnosticLog>()
            .Where(x => x.TaskId == latestTask.Id)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync();

        var sections = new List<string>
        {
            $"最新诊断任务: taskId={latestTask.Id}, deviceId={latestTask.DeviceId}, status={latestTask.Status}, createdAt={latestTask.CreatedAt:yyyy-MM-dd HH:mm:ss}",
            $"最新诊断结论: {latestTask.Conclusion ?? "无"}"
        };

        if (logs.Count == 0)
        {
            sections.Add("该任务没有诊断日志");
            return string.Join('\n', sections);
        }

        sections.AddRange(logs.Select(x =>
            $"{x.CreatedAt:yyyy-MM-dd HH:mm:ss} [{x.StepName}] success={x.Success} detail={x.Detail} durationMs={x.DurationMs}"));

        return string.Join('\n', sections);
    }
}
