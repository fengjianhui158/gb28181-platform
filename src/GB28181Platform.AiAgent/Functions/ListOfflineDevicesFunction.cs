using System.Text.Json;
using GB28181Platform.Domain.Entities;
using GB28181Platform.Domain.Enums;
using SqlSugar;

namespace GB28181Platform.AiAgent.Functions;

public class ListOfflineDevicesFunction : IAgentFunction
{
    private readonly ISqlSugarClient _db;

    public ListOfflineDevicesFunction(ISqlSugarClient db) => _db = db;

    public string Name => "list_offline_devices";
    public string Description => "列出当前所有离线的设备，包括设备名称、IP和最后心跳时间";
    public object ParameterSchema => new
    {
        type = "object",
        properties = new { }
    };

    public async Task<string> ExecuteAsync(string arguments)
    {
        var devices = await _db.Queryable<Device>()
            .Where(d => d.Status == nameof(DeviceStatus.Offline))
            .ToListAsync();

        return JsonSerializer.Serialize(devices.Select(d => new
        {
            d.Id,
            d.Name,
            d.RemoteIp,
            d.LastKeepaliveAt,
            d.LastRegisterAt
        }));
    }
}
