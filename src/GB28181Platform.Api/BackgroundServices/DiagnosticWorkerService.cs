using System.Threading.Channels;
using GB28181Platform.Diagnostic.Engine;

namespace GB28181Platform.Api.BackgroundServices;

public class DiagnosticWorkerService : BackgroundService
{
    private readonly Channel<DiagnosticRequest> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiagnosticWorkerService> _logger;

    public DiagnosticWorkerService(
        Channel<DiagnosticRequest> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DiagnosticWorkerService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("诊断工作线程已启动");
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IDiagnosticEngine>();
                await engine.RunDiagnosticAsync(request.TaskId, request.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "诊断任务执行失败: TaskId={TaskId}", request.TaskId);
            }
        }
    }
}

public class DiagnosticRequest
{
    public int TaskId { get; set; }
    public string DeviceId { get; set; } = "";
}
