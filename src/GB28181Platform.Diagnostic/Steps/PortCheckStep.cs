using GB28181Platform.Domain.Enums;

namespace GB28181Platform.Diagnostic.Steps;

public class PortCheckStep : IDiagnosticStep
{
    public string StepName => "TCP 端口检测";
    public DiagnosticStepType StepType => DiagnosticStepType.PortCheck;

    public async Task<StepResult> ExecuteAsync(DiagnosticContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var port = context.WebPort;

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(context.IpAddress, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(5000));
            sw.Stop();

            if (completed == connectTask && client.Connected)
            {
                return new StepResult
                {
                    Success = true,
                    Detail = $"端口 {port} 可达",
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    ContinueNext = true
                };
            }

            return new StepResult
            {
                Success = false,
                Detail = $"端口 {port} 不可达 (超时 5s)",
                DurationMs = (int)sw.ElapsedMilliseconds,
                ContinueNext = false
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult
            {
                Success = false,
                Detail = $"端口 {port} 连接失败: {ex.Message}",
                DurationMs = (int)sw.ElapsedMilliseconds,
                ContinueNext = false
            };
        }
    }
}
