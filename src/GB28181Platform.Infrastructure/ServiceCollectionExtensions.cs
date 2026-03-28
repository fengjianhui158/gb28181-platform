using GB28181Platform.Infrastructure.Database;
using GB28181Platform.Infrastructure.MediaServer;
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

        // 注册 ZLMediaKit 客户端
        services.AddSingleton<IZlmClient, ZlmClient>();

        return services;
    }
}
