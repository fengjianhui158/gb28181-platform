using GB28181Platform.Domain.Common;
using GB28181Platform.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace GB28181Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticController : ControllerBase
{
    private readonly ISqlSugarClient _db;

    public DiagnosticController(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取设备的诊断任务列表
    /// </summary>
    [HttpGet("tasks")]
    public async Task<ApiResponse<List<DiagnosticTask>>> GetTasks([FromQuery] string? deviceId = null, [FromQuery] int limit = 20)
    {
        var query = _db.Queryable<DiagnosticTask>();
        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(t => t.DeviceId == deviceId);

        var tasks = await query.OrderBy(t => t.CreatedAt, OrderByType.Desc).Take(limit).ToListAsync();
        return ApiResponse<List<DiagnosticTask>>.Ok(tasks);
    }

    /// <summary>
    /// 获取诊断任务详情（含步骤日志）
    /// </summary>
    [HttpGet("tasks/{taskId}")]
    public async Task<ApiResponse<DiagnosticTask>> GetTask(int taskId)
    {
        var task = await _db.Queryable<DiagnosticTask>().FirstAsync(t => t.Id == taskId);
        if (task == null)
            return ApiResponse<DiagnosticTask>.Fail("诊断任务不存在");

        task.Logs = await _db.Queryable<DiagnosticLog>().Where(l => l.TaskId == taskId).ToListAsync();
        return ApiResponse<DiagnosticTask>.Ok(task);
    }

    /// <summary>
    /// 获取设备最近的诊断日志
    /// </summary>
    [HttpGet("logs")]
    public async Task<ApiResponse<List<DiagnosticLog>>> GetLogs([FromQuery] string deviceId, [FromQuery] int limit = 50)
    {
        var logs = await _db.Queryable<DiagnosticLog>()
            .Where(l => l.DeviceId == deviceId)
            .OrderBy(l => l.CreatedAt, OrderByType.Desc)
            .Take(limit)
            .ToListAsync();
        return ApiResponse<List<DiagnosticLog>>.Ok(logs);
    }

    /// <summary>
    /// 手动触发诊断
    /// </summary>
    [HttpPost("run/{deviceId}")]
    public async Task<ApiResponse<DiagnosticTask>> RunDiagnostic(string deviceId)
    {
        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == deviceId);
        if (device == null)
            return ApiResponse<DiagnosticTask>.Fail("设备不存在");

        var task = new DiagnosticTask
        {
            DeviceId = deviceId,
            TriggerType = "MANUAL",
            Status = "PENDING"
        };
        task.Id = await _db.Insertable(task).ExecuteReturnIdentityAsync();

        // TODO: 将任务放入诊断引擎队列执行

        return ApiResponse<DiagnosticTask>.Ok(task);
    }
}
