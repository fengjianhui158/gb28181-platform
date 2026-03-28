using GB28181Platform.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace GB28181Platform.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string mysqlConnectionString)
    {
        // 注册 SqlSugar
        var db = DbContext.CreateSqlSugar(mysqlConnectionString);
        services.AddSingleton<ISqlSugarClient>(db);

        // 启动时自动建表
        DbContext.InitDatabase(db);

        return services;
    }
}
