using GB28181Platform.Diagnostic.Browser.VisibleField;
using Microsoft.Playwright;

namespace GB28181Platform.Diagnostic.Browser.Interactive;

public class LiveDomFieldReader
{
    private readonly VisibleFieldConfigExtractor _extractor;

    public LiveDomFieldReader(VisibleFieldConfigExtractor extractor)
    {
        _extractor = extractor;
    }

    public VisibleFieldExtractionResult ExtractFromHtmlSnapshot(string html)
    {
        return _extractor.ExtractFromHtml(html);
    }

    public async Task<VisibleFieldExtractionResult> ReadAsync(IPage page)
    {
        var html = await CaptureLiveDomHtmlAsync(page);
        return ExtractFromHtmlSnapshot(html);
    }

    public async Task<VisibleFieldExtractionResult> ReadAsync(IFrame frame)
    {
        var html = await CaptureLiveDomHtmlAsync(frame);
        return ExtractFromHtmlSnapshot(html);
    }

    public static Task<string> CaptureLiveDomHtmlAsync(IPage page) =>
        page.EvaluateAsync<string>(CaptureLiveDomScript);

    public static Task<string> CaptureLiveDomHtmlAsync(IFrame frame) =>
        frame.EvaluateAsync<string>(CaptureLiveDomScript);

    private const string CaptureLiveDomScript = @"() => {
        const clone = document.documentElement.cloneNode(true);
        const sourceInputs = document.querySelectorAll('input, textarea, select');
        const clonedInputs = clone.querySelectorAll('input, textarea, select');

        for (let i = 0; i < sourceInputs.length && i < clonedInputs.length; i++) {
            const source = sourceInputs[i];
            const target = clonedInputs[i];

            if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
                target.setAttribute('value', source.value || '');
            }

            if (target.tagName === 'INPUT' && source.type === 'checkbox') {
                if (source.checked) {
                    target.setAttribute('checked', 'checked');
                } else {
                    target.removeAttribute('checked');
                }
            }

            if (target.tagName === 'SELECT') {
                const targetOptions = target.querySelectorAll('option');
                for (let j = 0; j < targetOptions.length; j++) {
                    if (j === source.selectedIndex) {
                        targetOptions[j].setAttribute('selected', 'selected');
                    } else {
                        targetOptions[j].removeAttribute('selected');
                    }
                }
            }
        }

        return clone.outerHTML;
    }";
}
