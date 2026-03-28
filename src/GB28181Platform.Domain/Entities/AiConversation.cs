using SqlSugar;

namespace GB28181Platform.Domain.Entities;

/// <summary>
/// AI 对话记录
/// </summary>
[SugarTable("ai_conversations")]
public class AiConversation
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int UserId { get; set; }

    [SugarColumn(Length = 36)]
    public string SessionId { get; set; } = string.Empty;

    [SugarColumn(Length = 20, IsNullable = true)]
    public string? DeviceId { get; set; }

    /// <summary>
    /// user / assistant / function
    /// </summary>
    [SugarColumn(Length = 20)]
    public string Role { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string Content { get; set; } = string.Empty;

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? FunctionName { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? FunctionArgs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
