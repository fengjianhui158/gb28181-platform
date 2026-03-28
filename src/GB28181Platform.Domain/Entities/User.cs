using SqlSugar;

namespace GB28181Platform.Domain.Entities;

[SugarTable("users")]
public class User
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 100)]
    public string Username { get; set; } = string.Empty;

    [SugarColumn(Length = 255)]
    public string PasswordHash { get; set; } = string.Empty;

    [SugarColumn(Length = 50)]
    public string Role { get; set; } = "operator";

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
