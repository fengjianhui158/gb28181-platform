namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public static class VisibleFieldNavigationResolver
{
    public static IReadOnlyList<IReadOnlyList<string>> Resolve(
        ManufacturerNavigationOptions options,
        string? manufacturer)
    {
        if (options.Manufacturers.Count == 0 || string.IsNullOrWhiteSpace(manufacturer))
        {
            return [];
        }

        if (options.Manufacturers.TryGetValue(manufacturer, out var exact))
        {
            return exact.NavigationPaths;
        }

        foreach (var (key, definition) in options.Manufacturers)
        {
            if (manufacturer.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                key.Contains(manufacturer, StringComparison.OrdinalIgnoreCase))
            {
                return definition.NavigationPaths;
            }
        }

        return [];
    }
}
