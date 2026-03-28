namespace GB28181Platform.Diagnostic.Engine;

public interface IDiagnosticEngine
{
    Task RunDiagnosticAsync(int taskId, string deviceId);
}
