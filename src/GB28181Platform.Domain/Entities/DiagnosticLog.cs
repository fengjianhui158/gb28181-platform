using SqlSugar;

namespace GB28181Platform.Domain.Entities;

/// <summary>
/// 诊断步骤日志
/// </summary>
[SugarTable("diagnostic_logs")]
public class DiagnosticLog
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int TaskId { get; set; }

    [SugarColumn(Length = 20)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// PING / PORT_CHECK / BROWSER_CHECK
    /// </summary>
    [SugarColumn(Length = 50)]
    public string StepName { get; set; } = string.Empty;

    public bool Success { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Detail { get; set; }

    public int DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
