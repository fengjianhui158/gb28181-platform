using GB28181Platform.Domain.Common;
using GB28181Platform.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace GB28181Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly ISqlSugarClient _db;

    public DeviceController(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取设备列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<List<Device>>> GetDevices([FromQuery] string? status = null)
    {
        var query = _db.Queryable<Device>();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);

        var devices = await query.OrderBy(d => d.CreatedAt, OrderByType.Desc).ToListAsync();
        return ApiResponse<List<Device>>.Ok(devices);
    }

    /// <summary>
    /// 获取单个设备详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiResponse<Device>> GetDevice(string id)
    {
        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == id);
        if (device == null)
            return ApiResponse<Device>.Fail("设备不存在");

        // 查询通道
        device.Channels = await _db.Queryable<Channel>().Where(c => c.DeviceId == id).ToListAsync();
        return ApiResponse<Device>.Ok(device);
    }

    /// <summary>
    /// 获取设备的通道列表
    /// </summary>
    [HttpGet("{id}/channels")]
    public async Task<ApiResponse<List<Channel>>> GetChannels(string id)
    {
        var channels = await _db.Queryable<Channel>().Where(c => c.DeviceId == id).ToListAsync();
        return ApiResponse<List<Channel>>.Ok(channels);
    }

    /// <summary>
    /// 更新设备信息（如 Web 端口、密码等）
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResponse> UpdateDevice(string id, [FromBody] DeviceUpdateRequest request)
    {
        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == id);
        if (device == null)
            return ApiResponse.Fail("设备不存在");

        if (!string.IsNullOrEmpty(request.Name)) device.Name = request.Name;
        if (request.WebPort.HasValue) device.WebPort = request.WebPort.Value;
        if (!string.IsNullOrEmpty(request.Password)) device.Password = request.Password;
        device.UpdatedAt = DateTime.Now;

        await _db.Updateable(device).ExecuteCommandAsync();
        return ApiResponse.Ok();
    }

    /// <summary>
    /// 删除设备
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResponse> DeleteDevice(string id)
    {
        await _db.Deleteable<Channel>().Where(c => c.DeviceId == id).ExecuteCommandAsync();
        await _db.Deleteable<Device>().Where(d => d.Id == id).ExecuteCommandAsync();
        return ApiResponse.Ok();
    }
}

public class DeviceUpdateRequest
{
    public string? Name { get; set; }
    public int? WebPort { get; set; }
    public string? Password { get; set; }
}
