using GB28181Platform.Diagnostic.Steps;
using GB28181Platform.Domain.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace GB28181Platform.Diagnostic.Engine;

public class DiagnosticEngine : IDiagnosticEngine
{
    private readonly IEnumerable<IDiagnosticStep> _steps;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<DiagnosticEngine> _logger;

    public DiagnosticEngine(
        IEnumerable<IDiagnosticStep> steps,
        ISqlSugarClient db,
        ILogger<DiagnosticEngine> logger)
    {
        _steps = steps.OrderBy(s => s.StepType);
        _db = db;
        _logger = logger;
    }

    public async Task RunDiagnosticAsync(int taskId, string deviceId)
    {
        // 查询设备信息
        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == deviceId);
        if (device == null)
        {
            _logger.LogWarning("诊断失败: 设备 {DeviceId} 不存在", deviceId);
            return;
        }

        // 更新任务状态为 RUNNING
        await _db.Updateable<DiagnosticTask>()
            .SetColumns(t => t.Status == "RUNNING")
            .SetColumns(t => t.StartedAt == DateTime.Now)
            .Where(t => t.Id == taskId)
            .ExecuteCommandAsync();

        var context = new DiagnosticContext
        {
            DeviceId = deviceId,
            IpAddress = device.RemoteIp ?? "",
            WebPort = device.WebPort > 0 ? device.WebPort : 80,
            WebUsername = device.WebUsername,
            WebPassword = device.WebPassword,
            TaskId = taskId
        };

        var conclusions = new List<string>();

        foreach (var step in _steps)
        {
            if (!context.ShouldContinue) break;

            _logger.LogInformation("诊断 {DeviceId} - 执行步骤: {Step}", deviceId, step.StepName);
            var result = await step.ExecuteAsync(context);

            // 保存日志
            var log = new DiagnosticLog
            {
                TaskId = taskId,
                DeviceId = deviceId,
                StepName = step.StepName,
                Success = result.Success,
                Detail = result.Detail,
                DurationMs = result.DurationMs,
                CreatedAt = DateTime.Now
            };
            await _db.Insertable(log).ExecuteCommandAsync();

            conclusions.Add($"[{step.StepName}] {(result.Success ? "通过" : "失败")}: {result.Detail}");
            context.ShouldContinue = result.ContinueNext;
        }

        // 更新任务结论
        var conclusion = string.Join("\n", conclusions);
        await _db.Updateable<DiagnosticTask>()
            .SetColumns(t => t.Status == "COMPLETED")
            .SetColumns(t => t.CompletedAt == DateTime.Now)
            .SetColumns(t => t.Conclusion == conclusion)
            .Where(t => t.Id == taskId)
            .ExecuteCommandAsync();

        _logger.LogInformation("诊断完成 {DeviceId}: {Conclusion}", deviceId, conclusion);
    }
}
