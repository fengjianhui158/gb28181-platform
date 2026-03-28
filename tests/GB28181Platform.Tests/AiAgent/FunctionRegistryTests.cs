using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Functions;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class FunctionRegistryTests
{
    [Fact]
    public void Get_ReturnsRegisteredFunction()
    {
        var func = Substitute.For<IAgentFunction>();
        func.Name.Returns("get_device_status");

        var registry = new FunctionRegistry(new[] { func });

        Assert.Same(func, registry.Get("get_device_status"));
    }

    [Fact]
    public void Get_ReturnsNull_WhenNotRegistered()
    {
        var registry = new FunctionRegistry(Array.Empty<IAgentFunction>());

        Assert.Null(registry.Get("nonexistent_function"));
    }

    [Fact]
    public void GetDefinitions_ReturnsAllRegistered()
    {
        var func1 = Substitute.For<IAgentFunction>();
        func1.Name.Returns("get_device_status");
        func1.Description.Returns("查询设备状态");
        func1.ParameterSchema.Returns(new { type = "object" });

        var func2 = Substitute.For<IAgentFunction>();
        func2.Name.Returns("list_offline_devices");
        func2.Description.Returns("列出所有离线设备");
        func2.ParameterSchema.Returns(new { type = "object" });

        var registry = new FunctionRegistry(new[] { func1, func2 });

        var definitions = registry.GetDefinitions();

        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.Name == "get_device_status");
        Assert.Contains(definitions, d => d.Name == "list_offline_devices");
    }
}
