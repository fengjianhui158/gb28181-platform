namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public static class VisibleFieldPageDetector
{
    public const int DefaultMinimumMatchedFields = 3;

    public static VisibleFieldExtractionResult Evaluate(
        VisibleFieldConfigExtractor extractor,
        string html,
        int minimumMatchedFields = DefaultMinimumMatchedFields)
    {
        var result = extractor.ExtractFromHtml(html);
        result.IsSuccess = result.MatchedFields >= minimumMatchedFields;

        if (!result.IsSuccess && string.IsNullOrWhiteSpace(result.FailureReason))
        {
            result.FailureReason = "页面命中标准字段数量不足";
        }

        return result;
    }
}
