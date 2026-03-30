namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class VisibleFieldExtractionResult
{
    public bool IsSuccess { get; set; }
    public int MatchedFields { get; set; }
    public string FailureReason { get; set; } = "";
    public VisibleGbConfig Config { get; set; } = new();
}
