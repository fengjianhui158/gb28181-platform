using GB28181Platform.Domain.Enums;

namespace GB28181Platform.Diagnostic.Steps;

public class PingCheckStep : IDiagnosticStep
{
    public string StepName => "ICMP Ping";
    public DiagnosticStepType StepType => DiagnosticStepType.Ping;

    public async Task<StepResult> ExecuteAsync(DiagnosticContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(context.IpAddress, 5000);
            sw.Stop();

            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
            {
                return new StepResult
                {
                    Success = true,
                    Detail = $"Ping 可达, RTT={reply.RoundtripTime}ms",
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    ContinueNext = true
                };
            }

            return new StepResult
            {
                Success = false,
                Detail = $"Ping 超时, 状态={reply.Status}",
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
                Detail = $"Ping 异常: {ex.Message}",
                DurationMs = (int)sw.ElapsedMilliseconds,
                ContinueNext = false
            };
        }
    }
}
