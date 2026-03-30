namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class ManufacturerNavigationOptions
{
    public Dictionary<string, ManufacturerNavigationDefinition> Manufacturers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ManufacturerNavigationDefinition
{
    public List<List<string>> NavigationPaths { get; set; } = [];
}
