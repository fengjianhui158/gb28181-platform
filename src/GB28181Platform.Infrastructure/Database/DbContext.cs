using GB28181Platform.Domain.Entities;
using SqlSugar;

namespace GB28181Platform.Infrastructure.Database;

public static class DbContext
{
    public static void InitDatabase(ISqlSugarClient db)
    {
        // 自动建库（如果不存在）
        db.DbMaintenance.CreateDatabase();

        // CodeFirst 自动建表/更新表结构
        db.CodeFirst.InitTables(
            typeof(Device),
            typeof(Channel),
            typeof(DiagnosticTask),
            typeof(DiagnosticLog),
            typeof(AlarmRecord),
            typeof(AiConversation),
            typeof(User)
        );
    }

    public static SqlSugarScope CreateSqlSugar(string connectionString)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            DbType = DbType.MySql,
            ConnectionString = connectionString,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        },
        db =>
        {
            db.Aop.OnLogExecuting = (sql, pars) =>
            {
                // 可选：SQL 日志
            };
        });
    }
}
