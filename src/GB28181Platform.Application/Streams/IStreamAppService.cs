namespace GB28181Platform.Application.Streams;

public interface IStreamAppService
{
    Task<PlayResult> PlayAsync(string deviceId, string channelId);
    Task<bool> StopAsync(string deviceId, string channelId);
}

public class PlayResult
{
    public bool Success { get; set; }
    public string? WebRtcUrl { get; set; }
    public string? Message { get; set; }
}
