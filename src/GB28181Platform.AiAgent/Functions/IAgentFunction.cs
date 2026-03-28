namespace GB28181Platform.AiAgent.Functions;

public interface IAgentFunction
{
    string Name { get; }
    string Description { get; }
    object ParameterSchema { get; }
    Task<string> ExecuteAsync(string arguments);
}
