namespace GB28181Platform.Infrastructure.MediaServer;

public interface IZlmClient
{
    /// <summary>打开 RTP 收流端口</summary>
    Task<OpenRtpResult?> OpenRtpServerAsync(string streamId, int? port = null);

    /// <summary>关闭 RTP 收流端口</summary>
    Task<bool> CloseRtpServerAsync(string streamId);

    /// <summary>获取流列表</summary>
    Task<List<MediaStream>> GetMediaListAsync();

    /// <summary>关闭流</summary>
    Task<bool> CloseStreamAsync(string schema, string vhost, string app, string stream);

    /// <summary>获取 WebRTC 播放地址</summary>
    string GetWebRtcPlayUrl(string app, string streamId);
}

public class OpenRtpResult
{
    public int Code { get; set; }
    public int Port { get; set; }
}

public class MediaStream
{
    public string App { get; set; } = "";
    public string Stream { get; set; } = "";
    public string Schema { get; set; } = "";
    public int TotalReaderCount { get; set; }
}
