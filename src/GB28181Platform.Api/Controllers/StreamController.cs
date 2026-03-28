using GB28181Platform.Application.Streams;
using Microsoft.AspNetCore.Mvc;

namespace GB28181Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly IStreamAppService _streamService;

    public StreamController(IStreamAppService streamService)
    {
        _streamService = streamService;
    }

    [HttpPost("play")]
    public async Task<IActionResult> Play([FromBody] PlayRequest req)
    {
        var result = await _streamService.PlayAsync(req.DeviceId, req.ChannelId);
        return Ok(result);
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop([FromBody] StopRequest req)
    {
        var ok = await _streamService.StopAsync(req.DeviceId, req.ChannelId);
        return Ok(new { success = ok });
    }
}

public class PlayRequest
{
    public string DeviceId { get; set; } = "";
    public string ChannelId { get; set; } = "";
}

public class StopRequest
{
    public string DeviceId { get; set; } = "";
    public string ChannelId { get; set; } = "";
}
