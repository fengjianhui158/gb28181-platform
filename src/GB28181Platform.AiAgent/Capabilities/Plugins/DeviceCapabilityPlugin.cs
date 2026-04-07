using GB28181Platform.Domain.Entities;
using GB28181Platform.Domain.Enums;
using Microsoft.SemanticKernel;
using SqlSugar;

namespace GB28181Platform.AiAgent.Capabilities.Plugins;

public class DeviceCapabilityPlugin
{
    private readonly ISqlSugarClient _db;

    public DeviceCapabilityPlugin(ISqlSugarClient db)
    {
        _db = db;
    }

    [KernelFunction("get_device_status")]
    public async Task<string> GetDeviceStatusAsync(string deviceId)
    {
        var device = await _db.Queryable<Device>().FirstAsync(x => x.Id == deviceId);

        if (device == null)
        {
            return "未找到设备";
        }

        return $$"""
        设备状态:
        - Id: {{device.Id}}
        - Name: {{device.Name}}
        - Status: {{device.Status}}
        - RemoteIp: {{device.RemoteIp}}
        - RemotePort: {{device.RemotePort}}
        - Manufacturer: {{device.Manufacturer}}
        - LastKeepaliveAt: {{device.LastKeepaliveAt}}
        - LastRegisterAt: {{device.LastRegisterAt}}
        """;
    }

    [KernelFunction("list_offline_devices")]
    public async Task<string> ListOfflineDevicesAsync()
    {
        var devices = await _db.Queryable<Device>()
            .Where(x => x.Status == nameof(DeviceStatus.Offline))
            .ToListAsync();

        if (devices.Count == 0)
        {
            return "当前没有离线设备";
        }

        return string.Join('\n', devices.Select(x => $"{x.Id} {x.Name} {x.RemoteIp}"));
    }
}
