using GB28181Platform.Diagnostic.Engine;
using GB28181Platform.Diagnostic.Steps;
using GB28181Platform.Domain.Entities;
using GB28181Platform.Domain.Enums;
using GB28181Platform.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class DiagnosticEngineTests
{
    [Fact]
    public async Task RunDiagnostic_ExecutesStepsInStepTypeOrder()
    {
        var executionLog = new List<string>();
        var device = new Device
        {
            Id = "34020000001320000001",
            RemoteIp = "192.168.1.100",
            WebPort = 80
        };
        var db = MockDbHelper.CreateForDiagnostic(device);

        var browserStep = CreateStep("Browser", DiagnosticStepType.BrowserCheck, true,
            "配置正常", () => executionLog.Add("Browser"));
        var pingStep = CreateStep("Ping", DiagnosticStepType.Ping, true,
            "RTT=2ms", () => executionLog.Add("Ping"));
        var portStep = CreateStep("Port", DiagnosticStepType.PortCheck, true,
            "端口 80 开放", () => executionLog.Add("Port"));

        var engine = new DiagnosticEngine(
            new[] { browserStep, pingStep, portStep },
            db,
            NullLogger<DiagnosticEngine>.Instance);

        await engine.RunDiagnosticAsync(1, device.Id);

        Assert.Equal(3, executionLog.Count);
        Assert.Equal("Ping", executionLog[0]);
        Assert.Equal("Port", executionLog[1]);
        Assert.Equal("Browser", executionLog[2]);
    }

    [Fact]
    public async Task RunDiagnostic_StopsWhenContinueNextIsFalse()
    {
        var executionLog = new List<string>();
        var device = new Device
        {
            Id = "34020000001320000002",
            RemoteIp = "10.0.0.1",
            WebPort = 80
        };
        var db = MockDbHelper.CreateForDiagnostic(device);

        var pingStep = CreateStep("Ping", DiagnosticStepType.Ping, false,
            "Ping 超时 (5s)", () => executionLog.Add("Ping"), continueNext: false);
        var portStep = CreateStep("Port", DiagnosticStepType.PortCheck, true,
            "端口开放", () => executionLog.Add("Port"));

        var engine = new DiagnosticEngine(
            new[] { pingStep, portStep },
            db,
            NullLogger<DiagnosticEngine>.Instance);

        await engine.RunDiagnosticAsync(1, device.Id);

        Assert.Single(executionLog);
        Assert.Equal("Ping", executionLog[0]);
    }

    [Fact]
    public async Task RunDiagnostic_DeviceNotFound_NoStepsExecuted()
    {
        var stepExecuted = false;
        var db = MockDbHelper.CreateForDiagnostic(null);

        var step = CreateStep("Ping", DiagnosticStepType.Ping, true,
            "OK", () => stepExecuted = true);

        var engine = new DiagnosticEngine(
            new[] { step },
            db,
            NullLogger<DiagnosticEngine>.Instance);

        await engine.RunDiagnosticAsync(1, "nonexistent_device");

        Assert.False(stepExecuted);
    }

    private static IDiagnosticStep CreateStep(
        string name,
        DiagnosticStepType type,
        bool success,
        string detail,
        Action? onExecute = null,
        bool continueNext = true)
    {
        var step = Substitute.For<IDiagnosticStep>();
        step.StepName.Returns(name);
        step.StepType.Returns(type);
        step.ExecuteAsync(Arg.Any<DiagnosticContext>()).Returns(_ =>
        {
            onExecute?.Invoke();
            return new StepResult
            {
                Success = success,
                Detail = detail,
                DurationMs = 10,
                ContinueNext = continueNext
            };
        });
        return step;
    }
}
