using GB28181Platform.AiAgent.Capabilities.Persistence;
using GB28181Platform.AiAgent.Conversation;
using NSubstitute;
using SqlSugar;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class SqlSugarConversationStoreTests
{
    [Fact]
    public async Task AppendMessageAsync_PersistsUserConversationMessage()
    {
        var db = Substitute.For<ISqlSugarClient>();
        var insertable = Substitute.For<IInsertable<GB28181Platform.Domain.Entities.AiConversation>>();
        insertable.ExecuteCommandAsync().Returns(1);
        db.Insertable(Arg.Any<List<GB28181Platform.Domain.Entities.AiConversation>>()).Returns(insertable);

        var sut = new SqlSugarConversationStore(db);

        var message = new ConversationMessageRecord
        {
            ConversationId = "conv-001",
            UserId = 7,
            Role = "user",
            Items = [new ConversationContentItemRecord { Kind = "text", Text = "设备状态怎么样" }]
        };

        await sut.AppendMessageAsync(message, CancellationToken.None);

        await insertable.Received(1).ExecuteCommandAsync();
    }
}
