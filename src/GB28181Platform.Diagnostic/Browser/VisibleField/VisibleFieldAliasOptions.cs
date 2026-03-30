namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class VisibleFieldAliasOptions
{
    public Dictionary<string, List<string>> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
