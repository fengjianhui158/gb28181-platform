using System.Text.Json;
using GB28181Platform.Domain.Entities;
using SqlSugar;

namespace GB28181Platform.AiAgent.Functions;

public class GetDeviceStatusFunction : IAgentFunction
{
    private readonly ISqlSugarClient _db;

    public GetDeviceStatusFunction(ISqlSugarClient db) => _db = db;

    public string Name => "get_device_status";
    public string Description => "查询指定设备的当前状态、IP地址、最后心跳时间等信息";
    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            device_id = new { type = "string", description = "设备编码(20位)" }
        },
        required = new[] { "device_id" }
    };

    public async Task<string> ExecuteAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        var deviceId = args.GetProperty("device_id").GetString() ?? "";

        var device = await _db.Queryable<Device>().FirstAsync(d => d.Id == deviceId);
        if (device == null) return JsonSerializer.Serialize(new { error = "设备不存在" });

        return JsonSerializer.Serialize(new
        {
            device.Id,
            device.Name,
            device.Status,
            device.RemoteIp,
            device.RemotePort,
            device.Manufacturer,
            device.LastKeepaliveAt,
            device.LastRegisterAt
        });
    }
}
