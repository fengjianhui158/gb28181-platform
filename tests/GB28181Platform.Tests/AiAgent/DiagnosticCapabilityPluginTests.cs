using System.Linq.Expressions;
using GB28181Platform.AiAgent.Capabilities.Plugins;
using GB28181Platform.Domain.Entities;
using NSubstitute;
using SqlSugar;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class DiagnosticCapabilityPluginTests
{
    [Fact]
    public async Task GetDiagnosticLogsAsync_UsesLatestTaskConclusionAndTaskScopedLogs()
    {
        var db = Substitute.For<ISqlSugarClient>();

        var latestTask = new DiagnosticTask
        {
            Id = 200,
            DeviceId = "34020000001320000001",
            Conclusion = "浏览器配置检查失败，未命中有效国标配置页"
        };

        var taskQuery = Substitute.For<ISugarQueryable<DiagnosticTask>>();
        taskQuery.Where(Arg.Any<Expression<Func<DiagnosticTask, bool>>>()).Returns(taskQuery);
        taskQuery.OrderBy(Arg.Any<Expression<Func<DiagnosticTask, object>>>(), Arg.Any<OrderByType>()).Returns(taskQuery);
        taskQuery.FirstAsync().Returns(latestTask);
        db.Queryable<DiagnosticTask>().Returns(taskQuery);

        var latestTaskLogs = new List<DiagnosticLog>
        {
            new()
            {
                TaskId = 200,
                DeviceId = latestTask.DeviceId,
                StepName = "ICMP Ping",
                Success = true,
                Detail = "Ping 可达, RTT=3ms",
                DurationMs = 7,
                CreatedAt = new DateTime(2026, 4, 2, 10, 0, 0)
            },
            new()
            {
                TaskId = 200,
                DeviceId = latestTask.DeviceId,
                StepName = "TCP 端口检测",
                Success = true,
                Detail = "端口 80 可达",
                DurationMs = 3,
                CreatedAt = new DateTime(2026, 4, 2, 10, 0, 1)
            },
            new()
            {
                TaskId = 200,
                DeviceId = latestTask.DeviceId,
                StepName = "浏览器配置检查",
                Success = false,
                Detail = "DOM 模式未命中有效国标配置页: Visible field matches below threshold",
                DurationMs = 5582,
                CreatedAt = new DateTime(2026, 4, 2, 10, 0, 2)
            }
        };

        var logQuery = Substitute.For<ISugarQueryable<DiagnosticLog>>();
        logQuery.Where(Arg.Any<Expression<Func<DiagnosticLog, bool>>>()).Returns(logQuery);
        logQuery.OrderBy(Arg.Any<Expression<Func<DiagnosticLog, object>>>(), Arg.Any<OrderByType>()).Returns(logQuery);
        logQuery.ToListAsync().Returns(latestTaskLogs);
        db.Queryable<DiagnosticLog>().Returns(logQuery);

        var sut = new DiagnosticCapabilityPlugin(db);

        var result = await sut.GetDiagnosticLogsAsync(latestTask.DeviceId);

        Assert.Contains("最新诊断结论", result);
        Assert.Contains("浏览器配置检查失败", result);
        Assert.Contains("[浏览器配置检查]", result);
        Assert.DoesNotContain("网络连接异常", result);
        await taskQuery.Received(1).FirstAsync();
        await logQuery.Received(1).ToListAsync();
    }
}
