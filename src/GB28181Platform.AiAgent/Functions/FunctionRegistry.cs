namespace GB28181Platform.AiAgent.Functions;

public class FunctionRegistry
{
    private readonly Dictionary<string, IAgentFunction> _functions = new();

    public FunctionRegistry(IEnumerable<IAgentFunction> functions)
    {
        foreach (var f in functions)
            _functions[f.Name] = f;
    }

    public IAgentFunction? Get(string name) => _functions.GetValueOrDefault(name);

    public List<FunctionDefinition> GetDefinitions() => _functions.Values.Select(f => new FunctionDefinition
    {
        Name = f.Name,
        Description = f.Description,
        Parameters = f.ParameterSchema
    }).ToList();
}
