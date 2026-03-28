using SqlSugar;

namespace GB28181Platform.Domain.Entities;

/// <summary>
/// 报警记录
/// </summary>
[SugarTable("alarm_records")]
public class AlarmRecord
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 20)]
    public string DeviceId { get; set; } = string.Empty;

    [SugarColumn(Length = 20)]
    public string ChannelId { get; set; } = string.Empty;

    public int AlarmPriority { get; set; }

    public int AlarmMethod { get; set; }

    public int AlarmType { get; set; }

    public DateTime? AlarmTime { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
