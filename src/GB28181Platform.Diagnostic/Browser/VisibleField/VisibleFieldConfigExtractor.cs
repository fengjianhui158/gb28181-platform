using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class VisibleFieldConfigExtractor
{
    private readonly Dictionary<string, List<string>> _aliases;

    public VisibleFieldConfigExtractor(Dictionary<string, List<string>> aliases)
    {
        _aliases = aliases;
    }

    public static VisibleFieldConfigExtractor CreateForTests(Dictionary<string, List<string>> aliases) => new(aliases);

    public VisibleFieldExtractionResult ExtractFromHtml(string html)
    {
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);
        var result = new VisibleFieldExtractionResult();

        foreach (var field in _aliases)
        {
            foreach (var alias in field.Value)
            {
                var label = FindMatchingLabel(doc, alias);
                if (label is null)
                {
                    continue;
                }

                var value = TryReadNeighborValue(label);
                if (string.IsNullOrWhiteSpace(value) && !field.Key.Equals("Enable", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ApplyField(result.Config, field.Key, value, label);
                result.MatchedFields++;
                break;
            }
        }

        result.IsSuccess = result.MatchedFields >= 3 || !string.IsNullOrWhiteSpace(result.Config.SipServerIp);
        result.FailureReason = result.IsSuccess ? "" : "Visible field matches below threshold";
        return result;
    }

    private static IElement? FindMatchingLabel(AngleSharp.Dom.IDocument doc, string alias)
    {
        var normalizedAlias = Normalize(alias);
        return doc.All
            .Where(x => Normalize(x.TextContent) == normalizedAlias)
            .OrderBy(x => x.QuerySelectorAll("input,select,textarea").Length)
            .ThenBy(x => x.Children.Length)
            .FirstOrDefault();
    }

    private static string Normalize(string text)
    {
        return Regex.Replace(text ?? "", @"\s+|：|:", "").Trim().ToLowerInvariant();
    }

    private static string TryReadNeighborValue(IElement label)
    {
        var controls = new List<IElement>();
        var current = label.NextElementSibling;

        while (current is not null)
        {
            if (current.QuerySelectorAll("input,select,textarea").Length > 0)
            {
                var nestedControls = current.QuerySelectorAll("input,select,textarea").ToArray();
                if (nestedControls.Length > 0)
                {
                    controls.AddRange(nestedControls);
                    current = current.NextElementSibling;
                    continue;
                }
            }

            if (IsControl(current))
            {
                controls.Add(current);
                current = current.NextElementSibling;
                continue;
            }

            var normalized = Normalize(current.TextContent);
            if (controls.Count > 0 && normalized != ".")
            {
                break;
            }

            current = current.NextElementSibling;
        }

        if (controls.Count == 0)
        {
            return "";
        }

        if (controls.Count >= 4 && controls.Take(4).All(x => x.TagName.Equals("INPUT", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = controls.Take(4)
                .Select(x => x.GetAttribute("value") ?? "")
                .ToArray();
            if (parts.All(p => !string.IsNullOrWhiteSpace(p)))
            {
                return string.Join(".", parts);
            }
        }

        return ReadControlValue(controls[0]);
    }

    private static bool IsControl(IElement element)
    {
        return element.TagName.Equals("INPUT", StringComparison.OrdinalIgnoreCase)
            || element.TagName.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
            || element.TagName.Equals("TEXTAREA", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadControlValue(IElement control)
    {
        if (control.TagName.Equals("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return control.QuerySelector("option[selected]")?.TextContent.Trim()
                ?? control.QuerySelector("option")?.TextContent.Trim()
                ?? "";
        }

        if ((control.GetAttribute("type") ?? "").Equals("checkbox", StringComparison.OrdinalIgnoreCase))
        {
            return control.HasAttribute("checked") ? "true" : "false";
        }

        if (control is IHtmlInputElement input)
        {
            return input.Value ?? "";
        }

        if (control is IHtmlTextAreaElement textarea)
        {
            return textarea.Value ?? "";
        }

        return control.GetAttribute("value") ?? "";
    }

    private static void ApplyField(VisibleGbConfig config, string fieldName, string value, IElement label)
    {
        config.RawMatches[label.TextContent.Trim()] = value;
        switch (fieldName)
        {
            case "Enable":
                config.Enable = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case "SipServerId":
                config.SipServerId = value;
                break;
            case "SipDomain":
                config.SipDomain = value;
                break;
            case "SipServerIp":
                config.SipServerIp = value;
                break;
            case "SipServerPort":
                config.SipServerPort = value;
                break;
            case "LocalSipPort":
                config.LocalSipPort = value;
                break;
            case "RegisterExpiry":
                config.RegisterExpiry = value;
                break;
            case "Heartbeat":
                config.Heartbeat = value;
                break;
            case "HeartbeatTimeoutCount":
                config.HeartbeatTimeoutCount = value;
                break;
            case "DeviceId":
                config.DeviceId = value;
                break;
            case "ChannelId":
                config.ChannelId = value;
                break;
        }
    }
}
