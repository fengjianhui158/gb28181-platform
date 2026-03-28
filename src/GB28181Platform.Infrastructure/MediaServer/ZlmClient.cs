using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace GB28181Platform.Infrastructure.MediaServer;

public class ZlmClient : IZlmClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _secret;

    public ZlmClient(IConfiguration config)
    {
        _baseUrl = config["ZLMediaKit:BaseUrl"] ?? "http://127.0.0.1:8080";
        _secret = config["ZLMediaKit:Secret"] ?? "";
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    public async Task<OpenRtpResult?> OpenRtpServerAsync(string streamId, int? port = null)
    {
        var url = $"/index/api/openRtpServer?secret={_secret}&stream_id={streamId}&port={port ?? 0}&enable_tcp=1";
        var resp = await _http.GetFromJsonAsync<OpenRtpResult>(url);
        return resp;
    }

    public async Task<bool> CloseRtpServerAsync(string streamId)
    {
        var url = $"/index/api/closeRtpServer?secret={_secret}&stream_id={streamId}";
        var resp = await _http.GetFromJsonAsync<ZlmBaseResult>(url);
        return resp?.Code == 0;
    }

    public async Task<List<MediaStream>> GetMediaListAsync()
    {
        var url = $"/index/api/getMediaList?secret={_secret}";
        var resp = await _http.GetFromJsonAsync<ZlmMediaListResult>(url);
        return resp?.Data ?? new();
    }

    public async Task<bool> CloseStreamAsync(string schema, string vhost, string app, string stream)
    {
        var url = $"/index/api/close_streams?secret={_secret}&schema={schema}&vhost={vhost}&app={app}&stream={stream}";
        var resp = await _http.GetFromJsonAsync<ZlmCloseResult>(url);
        return (resp?.CountClosed ?? 0) > 0;
    }

    public string GetWebRtcPlayUrl(string app, string streamId)
    {
        return $"{_baseUrl}/index/api/webrtc?app={app}&stream={streamId}&type=play";
    }
}

internal class ZlmBaseResult { public int Code { get; set; } }
internal class ZlmMediaListResult { public int Code { get; set; } public List<MediaStream> Data { get; set; } = new(); }
internal class ZlmCloseResult { public int Code { get; set; } public int CountClosed { get; set; } }
