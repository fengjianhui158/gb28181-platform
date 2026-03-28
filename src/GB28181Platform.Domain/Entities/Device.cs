using GB28181Platform.Domain.Enums;
using SqlSugar;

namespace GB28181Platform.Domain.Entities;

/// <summary>
/// GB28181 设备
/// </summary>
[SugarTable("devices")]
public class Device
{
    /// <summary>
    /// GB28181 设备编号 (20位)
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, Length = 20)]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(Length = 255)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string Manufacturer { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string Model { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string Firmware { get; set; } = string.Empty;

    [SugarColumn(Length = 10)]
    public string Transport { get; set; } = "UDP";

    [SugarColumn(Length = 45)]
    public string RemoteIp { get; set; } = string.Empty;

    public int RemotePort { get; set; } = 5060;

    public int WebPort { get; set; } = 80;

    [SugarColumn(Length = 100)]
    public string Password { get; set; } = string.Empty;

    [SugarColumn(Length = 20)]
    public string Status { get; set; } = nameof(DeviceStatus.Offline);

    [SugarColumn(Length = 20)]
    public string Charset { get; set; } = "GB2312";

    public DateTime? LastRegisterAt { get; set; }

    public DateTime? LastKeepaliveAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public List<Channel>? Channels { get; set; }
}
