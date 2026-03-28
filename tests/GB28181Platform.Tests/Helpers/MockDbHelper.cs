using GB28181Platform.Domain.Entities;
using NSubstitute;
using SqlSugar;
using System.Linq.Expressions;

namespace GB28181Platform.Tests.Helpers;

/// <summary>
/// SqlSugar ISqlSugarClient Mock 工具类
/// </summary>
public static class MockDbHelper
{
    public static ISqlSugarClient CreateForDiagnostic(Device? device)
    {
        var db = Substitute.For<ISqlSugarClient>();

        var queryable = Substitute.For<ISugarQueryable<Device>>();
        queryable.FirstAsync(Arg.Any<Expression<Func<Device, bool>>>())
            .Returns(device!);
        db.Queryable<Device>().Returns(queryable);

        var updateable = Substitute.For<IUpdateable<DiagnosticTask>>();
        updateable.SetColumns(Arg.Any<Expression<Func<DiagnosticTask, bool>>>())
            .Returns(updateable);
        updateable.Where(Arg.Any<Expression<Func<DiagnosticTask, bool>>>())
            .Returns(updateable);
        updateable.ExecuteCommandAsync().Returns(1);
        db.Updateable<DiagnosticTask>().Returns(updateable);

        var insertable = Substitute.For<IInsertable<DiagnosticLog>>();
        insertable.ExecuteCommandAsync().Returns(1);
        db.Insertable(Arg.Any<DiagnosticLog>()).Returns(insertable);

        return db;
    }

    public static ISqlSugarClient CreateForAiAgent()
    {
        var db = Substitute.For<ISqlSugarClient>();

        var insertable = Substitute.For<IInsertable<AiConversation>>();
        insertable.ExecuteCommandAsync().Returns(2);
        db.Insertable(Arg.Any<List<AiConversation>>()).Returns(insertable);

        return db;
    }
}
