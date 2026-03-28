using SqlSugar;

namespace GB28181Platform.Domain.Entities;

/// <summary>
/// GB28181 通道
/// </summary>
[SugarTable("channels")]
public class Channel
{
    [SugarColumn(IsPrimaryKey = true, Length = 20)]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(Length = 20)]
    public string DeviceId { get; set; } = string.Empty;

    [SugarColumn(Length = 255)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 100)]
    public string Manufacturer { get; set; } = string.Empty;

    [SugarColumn(Length = 45)]
    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; }

    [SugarColumn(Length = 10)]
    public string Status { get; set; } = "ON";

    /// <summary>
    /// 0未知 1球机 2半球 3固定枪机
    /// </summary>
    public int PtzType { get; set; }

    public double Longitude { get; set; }

    public double Latitude { get; set; }

    [SugarColumn(Length = 20)]
    public string ParentId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
