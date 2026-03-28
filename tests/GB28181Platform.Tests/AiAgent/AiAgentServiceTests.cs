using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Functions;
using GB28181Platform.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AiAgentServiceTests
{
    [Fact]
    public async Task ChatAsync_ReturnsContent_WhenNoFunctionCall()
    {
        var qwen = Substitute.For<IQwenClient>();
        qwen.ChatAsync(
                Arg.Any<List<ChatMessage>>(),
                Arg.Any<List<FunctionDefinition>>())
            .Returns(new ChatResponse
            {
                Content = "所有设备运行正常",
                FinishReason = "stop"
            });

        var registry = new FunctionRegistry(Array.Empty<IAgentFunction>());
        var db = MockDbHelper.CreateForAiAgent();
        var service = new AiAgentService(
            qwen, registry, db, NullLogger<AiAgentService>.Instance);

        var result = await service.ChatAsync("session1", "系统状态如何？");

        Assert.Equal("所有设备运行正常", result);
    }

    [Fact]
    public async Task ChatAsync_ExecutesFunctionCall_ThenReturnsFinalAnswer()
    {
        var callCount = 0;
        var qwen = Substitute.For<IQwenClient>();
        qwen.ChatAsync(
                Arg.Any<List<ChatMessage>>(),
                Arg.Any<List<FunctionDefinition>>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ChatResponse
                    {
                        FunctionCall = new FunctionCall
                        {
                            Name = "get_device_status",
                            Arguments = "{\"deviceId\":\"cam001\"}"
                        },
                        FinishReason = "function_call"
                    };
                }
                return new ChatResponse
                {
                    Content = "摄像机 cam001 已离线，最后心跳 10:00",
                    FinishReason = "stop"
                };
            });

        var func = Substitute.For<IAgentFunction>();
        func.Name.Returns("get_device_status");
        func.Description.Returns("查询设备状态");
        func.ParameterSchema.Returns(new { type = "object" });
        func.ExecuteAsync(Arg.Any<string>())
            .Returns("{\"status\":\"Offline\",\"lastKeepalive\":\"2026-03-28T10:00:00\"}");

        var registry = new FunctionRegistry(new[] { func });
        var db = MockDbHelper.CreateForAiAgent();
        var service = new AiAgentService(
            qwen, registry, db, NullLogger<AiAgentService>.Instance);

        var result = await service.ChatAsync("session1", "cam001 为什么离线了？");

        Assert.Equal("摄像机 cam001 已离线，最后心跳 10:00", result);
        await func.Received(1).ExecuteAsync("{\"deviceId\":\"cam001\"}");
    }

    [Fact]
    public async Task ChatAsync_EnforcesMaxFunctionCallsLimit()
    {
        var qwen = Substitute.For<IQwenClient>();
        qwen.ChatAsync(
                Arg.Any<List<ChatMessage>>(),
                Arg.Any<List<FunctionDefinition>>())
            .Returns(new ChatResponse
            {
                FunctionCall = new FunctionCall
                {
                    Name = "get_device_status",
                    Arguments = "{}"
                },
                FinishReason = "function_call"
            });

        var func = Substitute.For<IAgentFunction>();
        func.Name.Returns("get_device_status");
        func.Description.Returns("查询设备状态");
        func.ParameterSchema.Returns(new { type = "object" });
        func.ExecuteAsync(Arg.Any<string>()).Returns("{}");

        var registry = new FunctionRegistry(new[] { func });
        var db = MockDbHelper.CreateForAiAgent();
        var service = new AiAgentService(
            qwen, registry, db, NullLogger<AiAgentService>.Instance);

        var result = await service.ChatAsync("session1", "test");

        Assert.Equal("抱歉，分析过程超出了最大调用次数限制。", result);
        await func.Received(5).ExecuteAsync(Arg.Any<string>());
    }
}
