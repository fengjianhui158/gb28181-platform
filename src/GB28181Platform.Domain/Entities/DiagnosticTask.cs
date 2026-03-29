using SqlSugar;

namespace GB28181Platform.Domain.Entities;

/// <summary>
/// 诊断任务
/// </summary>
[SugarTable("diagnostic_tasks")]
public class DiagnosticTask
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 20)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// AUTO / MANUAL
    /// </summary>
    [SugarColumn(Length = 20)]
    public string TriggerType { get; set; } = "AUTO";

    /// <summary>
    /// PENDING / RUNNING / COMPLETED / FAILED
    /// </summary>
    [SugarColumn(Length = 20)]
    public string Status { get; set; } = "PENDING";

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Conclusion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public List<DiagnosticLog>? Logs { get; set; }
}
