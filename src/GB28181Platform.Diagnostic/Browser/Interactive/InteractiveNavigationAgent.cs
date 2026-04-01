using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GB28181Platform.Diagnostic.Browser.Interactive;

public class InteractiveNavigationAgent
{
    private readonly ILogger _logger;

    public InteractiveNavigationAgent(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<InteractiveNavigationStepResult>> NavigateAsync(
        IPage page,
        IReadOnlyList<string> steps,
        CancellationToken cancellationToken = default)
    {
        var results = new List<InteractiveNavigationStepResult>();

        foreach (var step in steps)
        {
            var result = await ExecuteStepAsync(page, step, cancellationToken);
            results.Add(result);
            if (!result.Success)
            {
                break;
            }
        }

        return results;
    }

    private async Task<InteractiveNavigationStepResult> ExecuteStepAsync(
        IPage page,
        string step,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("\u4ea4\u4e92\u5f0f\u5bfc\u822a: \u67e5\u627e '{Step}'", step);

        // ===== 页面结构诊断 =====
        try
        {
            // 方式1: 不依赖 JS，直接获取页面 HTML
            var pageUrl = page.Url;
            var frameUrls = string.Join(", ", page.Frames.Select(f => f.Url));
            _logger.LogInformation("页面诊断(基础): pageUrl={Url}, frames=[{Frames}]", pageUrl, frameUrls);

            var html = await page.ContentAsync();
            var htmlSnippet = html.Length > 800 ? html[..800] : html;
            _logger.LogInformation("页面诊断(HTML): 长度={Len}, 内容={Html}", html.Length, htmlSnippet);

            // 方式2: 最简 JS 测试 — 看 JS 执行是否正常
            try
            {
                var title = await page.EvaluateAsync<string>("() => document.title || 'NO_TITLE'");
                _logger.LogInformation("页面诊断(JS测试): title={Title}", title);
            }
            catch (Exception jsEx)
            {
                _logger.LogWarning("页面诊断(JS测试失败): {Msg}", jsEx.Message);
            }

            // 方式3: 在每个子 frame 上也尝试获取 HTML
            foreach (var frame in page.Frames)
            {
                if (frame == page.MainFrame) continue;
                try
                {
                    var frameHtml = await frame.ContentAsync();
                    var frameSnippet = frameHtml.Length > 500 ? frameHtml[..500] : frameHtml;
                    _logger.LogInformation("页面诊断(子frame): url={Url}, 长度={Len}, 内容={Html}",
                        frame.Url, frameHtml.Length, frameSnippet);
                }
                catch (Exception fEx)
                {
                    _logger.LogDebug("页面诊断(子frame失败): url={Url}, {Msg}", frame.Url, fEx.Message);
                }
            }
        }
        catch (Exception diagEx)
        {
            _logger.LogWarning("页面诊断失败: {Msg}", diagEx.Message);
        }
        // ===== 诊断结束 =====

        var before = await CaptureSnapshotAsync(page);
        var match = await FindBestCandidateAsync(page, step);
        if (match == null)
        {
            return new InteractiveNavigationStepResult(
                step,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                "\u672a\u627e\u5230\u53ef\u89c1\u83dc\u5355\u9879");
        }

        await match.Locator.ScrollIntoViewIfNeededAsync();
        await match.Locator.ClickAsync();
        // 大华等摄像机 Web UI 是 SPA，点击菜单不会触发完整页面加载
        // NetworkIdle 可能永远不会到达（后台持续有心跳/轮询请求），所以用短超时兜底
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("交互式导航: WaitForLoadState 超时(5s)，继续执行");
        }
        await Task.Delay(1200, cancellationToken);

        var after = await CaptureSnapshotAsync(page);
        if (!InteractivePageStateComparer.HasMeaningfulChange(before, after))
        {
            // 页面没变化 — 可能已经在这个页面了（如从路径2重试时"配置"已经是当前页）
            // 不判定为失败，以 "已在当前页" 继续下一步，如果后续步骤找不到才会真正失败
            _logger.LogInformation(
                "交互式导航: 点击 '{Step}' 后页面未变化，视为已在当前页，继续", step);
        }

        _logger.LogInformation(
            "\u4ea4\u4e92\u5f0f\u5bfc\u822a: \u547d\u4e2d '{Step}' \u4e8e {Source}, selector={Selector}, text={Text}",
            step,
            match.Source,
            match.Selector,
            match.Candidate.Text);

        return new InteractiveNavigationStepResult(
            step,
            true,
            match.Selector,
            match.Candidate.Text,
            match.Source);
    }

    private async Task<InteractivePageStateSnapshot> CaptureSnapshotAsync(IPage page)
    {
        var visibleText = await page.EvaluateAsync<string>(@"() => (document.body && document.body.innerText) || ''")
            ?? string.Empty;

        var tokens = visibleText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length <= 40)
            .Take(120)
            .ToArray();

        var urlPath = Uri.TryCreate(page.Url, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : page.Url;

        return new InteractivePageStateSnapshot(urlPath, visibleText, tokens);
    }

    private async Task<LocatorMatch?> FindBestCandidateAsync(IPage page, string text)
    {
        var frameMatches = new List<LocatorMatch>();

        var mainFrameMatch = await FindBestCandidateAsync(page.MainFrame, text, "main");
        if (mainFrameMatch != null)
        {
            frameMatches.Add(mainFrameMatch);
        }

        foreach (var frame in page.Frames)
        {
            if (frame == page.MainFrame)
            {
                continue;
            }

            var frameMatch = await FindBestCandidateAsync(frame, text, frame.Url);
            if (frameMatch != null)
            {
                frameMatches.Add(frameMatch);
            }
        }

        if (frameMatches.Count == 0)
        {
            return null;
        }

        var bestCandidate = InteractiveNavigationCandidate.SelectBest(
            frameMatches.Select(match => match.Candidate),
            text);

        if (bestCandidate == null)
        {
            return null;
        }

        return frameMatches.First(match =>
            ReferenceEquals(match.Candidate, bestCandidate) ||
            (match.Candidate.TagName == bestCandidate.TagName &&
             match.Candidate.Index == bestCandidate.Index &&
             match.Candidate.Text == bestCandidate.Text &&
             match.Candidate.Priority == bestCandidate.Priority &&
             match.Source == frameMatches
                 .Where(item =>
                     item.Candidate.TagName == bestCandidate.TagName &&
                     item.Candidate.Index == bestCandidate.Index &&
                     item.Candidate.Text == bestCandidate.Text &&
                     item.Candidate.Priority == bestCandidate.Priority)
                 .Select(item => item.Source)
                 .FirstOrDefault()));
    }

    private async Task<LocatorMatch?> FindBestCandidateAsync(IFrame frame, string text, string source)
    {
        var domMatches = await FindDomCandidatesAsync(frame, text, source);
        _logger.LogInformation(
            "\u4ea4\u4e92\u5f0f\u5bfc\u822a\u5019\u9009: source={Source}, step={Step}, domCandidates={Count}",
            source,
            text,
            domMatches.Count);

        if (domMatches.Count > 0)
        {
            var domBest = InteractiveNavigationCandidate.SelectBest(domMatches.Select(match => match.Candidate), text);
            if (domBest != null)
            {
                return domMatches.First(match =>
                    ReferenceEquals(match.Candidate, domBest) ||
                    (match.Candidate.TagName == domBest.TagName &&
                     match.Candidate.Index == domBest.Index &&
                     match.Candidate.Text == domBest.Text &&
                     match.Candidate.Priority == domBest.Priority));
            }
        }

        var esc = EscapeForTextSelector(text);
        var selectorDefinitions = new (string Selector, int Priority, bool IsSemanticMenu)[]
        {
            // Element Plus UI 框架精确选择器（海康等，最高优先级）
            ($".el-menu-item:has-text(\"{esc}\")", -4, true),
            ($".el-submenu__title:has-text(\"{esc}\")", -3, true),
            ($".el-tabs__item:has-text(\"{esc}\")", -2, true),
            ($"[role='menuitem']:has-text(\"{esc}\")", -1, true),
            ($"[role='tab']:has-text(\"{esc}\")", -1, true),

            // 通用选择器
            ($":text-is(\"{esc}\")", 0, true),
            ($"[title=\"{esc}\"]", 1, true),
            ($"input[value=\"{esc}\"]", 2, true),
            ($"button:text-is(\"{esc}\")", 3, true),
            ($"a:text-is(\"{esc}\")", 4, true),
            ($"li:text-is(\"{esc}\")", 5, true),
            ($"td:text-is(\"{esc}\")", 6, false),
            ($"span:text-is(\"{esc}\")", 7, false),
            ($"div:text-is(\"{esc}\")", 8, false),
            ($"button:has-text(\"{esc}\")", 20, true),
            ($"a:has-text(\"{esc}\")", 21, true),
            ($"li:has-text(\"{esc}\")", 22, true),
            ($"td:has-text(\"{esc}\")", 23, false),
            ($"span:has-text(\"{esc}\")", 24, false),
            ($"div:has-text(\"{esc}\")", 25, false)
        };

        var candidates = new List<LocatorMatch>();

        foreach (var (selector, priority, isSemanticMenu) in selectorDefinitions)
        {
            try
            {
                var locator = frame.Locator(selector);
                var count = Math.Min(await locator.CountAsync(), 20);
                for (var index = 0; index < count; index++)
                {
                    var item = locator.Nth(index);
                    string itemText;
                    bool isVisible;
                    double top = 0;
                    double area = double.MaxValue;
                    var navigationContextScore = 0;
                    var hasPointerCursor = false;

                    try
                    {
                        itemText = (await item.InnerTextAsync()).Trim();
                    }
                    catch
                    {
                        continue;
                    }

                    // 跳过文本过长的容器元素（如整个侧边栏 div）
                    if (itemText.Length > text.Length * 8 && itemText.Length > 40)
                    {
                        continue;
                    }

                    try
                    {
                        isVisible = await item.IsVisibleAsync();
                    }
                    catch
                    {
                        isVisible = false;
                    }

                    try
                    {
                        var box = await item.BoundingBoxAsync();
                        if (box != null)
                        {
                            top = box.Y;
                            area = Math.Max(1, box.Width * box.Height);
                        }
                    }
                    catch
                    {
                        top = 0;
                        area = double.MaxValue;
                    }

                    try
                    {
                        var metadata = await item.EvaluateAsync<CandidateMetadata>(
                            """
                            (el) => {
                                const normalize = (value) => (value || '').replace(/\s+/g, '').trim();
                                const navigationKeywords = [
                                    '\u9884\u89c8',
                                    '\u56de\u653e',
                                    '\u8bbe\u7f6e',
                                    '\u914d\u7f6e',
                                    '\u62a5\u8b66',
                                    '\u6ce8\u9500',
                                    '\u7f51\u7edc\u8bbe\u7f6e',
                                    '\u7f51\u7edc',
                                    '\u5e73\u53f0\u63a5\u5165',
                                    '\u8bbe\u5907\u63a5\u5165',
                                    '\u56fd\u680728181',
                                    'GB28181',
                                    'ONVIF',
                                    'RTMP'
                                ];

                                let bestScore = 0;
                                let current = el;
                                for (let depth = 0; depth < 4 && current; depth++, current = current.parentElement) {
                                    const childTexts = Array.from(current.children || [])
                                        .map((child) => normalize(child.textContent || child.getAttribute('title') || ''))
                                        .filter(Boolean);

                                    const score = navigationKeywords.filter((keyword) =>
                                        childTexts.some((text) => text === keyword || text.includes(keyword))
                                    ).length;

                                    if (score > bestScore) {
                                        bestScore = score;
                                    }
                                }

                                const style = window.getComputedStyle(el);
                                return {
                                    NavigationContextScore: bestScore,
                                    HasPointerCursor: style.cursor === 'pointer'
                                };
                            }
                            """);

                        if (metadata != null)
                        {
                            navigationContextScore = metadata.NavigationContextScore;
                            hasPointerCursor = metadata.HasPointerCursor;
                        }
                    }
                    catch
                    {
                        navigationContextScore = 0;
                        hasPointerCursor = false;
                    }

                    var candidate = new InteractiveNavigationCandidate(
                        GetTagNameFromSelector(selector),
                        index,
                        itemText,
                        isVisible,
                        isSemanticMenu,
                        priority,
                        top,
                        area,
                        navigationContextScore,
                        hasPointerCursor,
                        HasNoiseKeywords(itemText),
                        CountLines(itemText));

                    candidates.Add(new LocatorMatch(candidate, item, selector, source));
                }
            }
            catch
            {
                // Ignore locator issues for legacy pages.
            }
        }

        var best = InteractiveNavigationCandidate.SelectBest(candidates.Select(match => match.Candidate), text);
        if (best != null)
        {
            return candidates.First(match =>
                ReferenceEquals(match.Candidate, best) ||
                (match.Candidate.TagName == best.TagName &&
                 match.Candidate.Index == best.Index &&
                 match.Candidate.Text == best.Text &&
                 match.Candidate.Priority == best.Priority));
        }

        // 快速直击兜底：大型 SPA 页面上详细扫描可能全部超时，用 .First + 短超时快速定位
        _logger.LogInformation("交互式导航: 详细扫描未命中 '{Step}'，尝试快速直击", text);
        var escapedText = EscapeForTextSelector(text);
        // 海康等厂商在导航文字中插入空格（如 "配  置"），构建正则匹配
        var regexPattern = string.Join(@"\s*", text.Select(ch => System.Text.RegularExpressions.Regex.Escape(ch.ToString())));
        var quickSelectors = new[]
        {
            // === 第1层：Element Plus UI 框架精确选择器（海康等厂商，不会误匹配容器） ===
            $".el-menu-item:has-text(/{regexPattern}/)",       // Element Plus 菜单项
            $".el-submenu__title:has-text(/{regexPattern}/)",  // Element Plus 子菜单标题
            $".el-tabs__item:has-text(/{regexPattern}/)",      // Element Plus 标签页
            $"[role='menuitem']:has-text(/{regexPattern}/)",   // ARIA 菜单项
            $"[role='tab']:has-text(/{regexPattern}/)",        // ARIA 标签页

            // === 第2层：Playwright 智能文本匹配（自动选最小元素） ===
            $"text=/{regexPattern}/",                          // 正则：配\s*置 匹配 "配  置"
            $"text={escapedText}",                             // 精确文本
            $"[title=\"{escapedText}\"]",                      // title 属性

            // === 第3层：具体标签（不含 div，避免匹配容器） ===
            $"p:text(/{regexPattern}/)",                       // <p> 标签（海康导航用 <p>）
            $"a:has-text(/{regexPattern}/)",                   // <a> 链接
            $"span:has-text(/{regexPattern}/)",                // <span>
            $"li:has-text(/{regexPattern}/)",                  // <li>（放最后，避免匹配外层 <li> 容器）
        };

        foreach (var sel in quickSelectors)
        {
            try
            {
                var loc = frame.Locator(sel).First;
                await loc.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
                if (await loc.IsVisibleAsync())
                {
                    var itemText = (await loc.InnerTextAsync(new() { Timeout = 2000 }))?.Trim() ?? "";

                    // 防御1：如果匹配到的元素文本远超目标（5倍以上且>20字符），说明命中了容器元素，跳过
                    if (itemText.Length > text.Length * 5 && itemText.Length > 20)
                    {
                        _logger.LogDebug(
                            "交互式导航: 快速直击 selector={Sel} 文本过长({Len}字符，目标{ExpLen}字符)，跳过容器",
                            sel, itemText.Length, text.Length);
                        continue;
                    }

                    // 防御2：验证可见文本确实包含目标（排除 has-text 匹配到隐藏子元素的容器）
                    var normalizedItem = string.Concat(itemText.Where(ch => !char.IsWhiteSpace(ch)));
                    var normalizedTarget = string.Concat(text.Where(ch => !char.IsWhiteSpace(ch)));
                    if (!normalizedItem.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug(
                            "交互式导航: 快速直击 selector={Sel} 文本不匹配('{ItemText}' 不含 '{Target}')，跳过",
                            sel, itemText, text);
                        continue;
                    }

                    _logger.LogInformation("交互式导航: 快速直击命中 '{Step}', selector={Sel}, text={Text}",
                        text, sel, itemText);

                    var quickCandidate = new InteractiveNavigationCandidate(
                        "quick", 0, itemText,
                        true, true, 0, 0, 1, 0, true, false, 1);
                    return new LocatorMatch(quickCandidate, loc, sel, source);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("交互式导航: 快速直击 selector={Sel} 失败: {Msg}", sel, ex.Message);
            }
        }

        return null;
    }

    private async Task<List<LocatorMatch>> FindDomCandidatesAsync(IFrame frame, string text, string source)
    {
        var candidates = new List<LocatorMatch>();
        List<DomCandidateDescriptor>? descriptors;

        try
        {
            descriptors = await frame.EvaluateAsync<List<DomCandidateDescriptor>>(
                """
                (expectedText) => {
                    const normalize = (value) => (value || '').replace(/\s+/g, '').trim();
                    const expected = normalize(expectedText);
                    const navigationKeywords = [
                        '\u9884\u89c8',
                        '\u56de\u653e',
                        '\u8bbe\u7f6e',
                        '\u914d\u7f6e',
                        '\u62a5\u8b66',
                        '\u6ce8\u9500',
                        '\u7f51\u7edc\u8bbe\u7f6e',
                        '\u7f51\u7edc',
                        '\u5e73\u53f0\u63a5\u5165',
                        '\u8bbe\u5907\u63a5\u5165',
                        '\u56fd\u680728181',
                        'GB28181',
                        'ONVIF',
                        'RTMP'
                    ];

                    const isVisible = (el) => {
                        if (!(el instanceof Element)) return false;
                        const style = window.getComputedStyle(el);
                        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return false;
                        const rect = el.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    };

                    const getSelector = (el) => {
                        if (el.id) return `#${CSS.escape(el.id)}`;

                        const parts = [];
                        let current = el;
                        while (current && current.nodeType === Node.ELEMENT_NODE && current !== document.body) {
                            const tag = current.tagName.toLowerCase();
                            const parent = current.parentElement;
                            if (!parent) break;
                            const sameTagSiblings = Array.from(parent.children).filter((child) => child.tagName === current.tagName);
                            const index = sameTagSiblings.indexOf(current) + 1;
                            parts.unshift(`${tag}:nth-of-type(${index})`);
                            current = parent;
                        }

                        return parts.length === 0 ? null : `body > ${parts.join(' > ')}`;
                    };

                    const scoreNavigationContext = (el) => {
                        let best = 0;
                        let current = el;
                        for (let depth = 0; depth < 4 && current; depth++, current = current.parentElement) {
                            const childTexts = Array.from(current.children || [])
                                .map((child) => normalize(child.textContent || child.getAttribute('title') || ''))
                                .filter(Boolean);

                            const score = navigationKeywords.filter((keyword) =>
                                childTexts.some((text) => text === keyword || text.includes(keyword))
                            ).length;

                            if (score > best) {
                                best = score;
                            }
                        }

                        return best;
                    };

                    return Array.from(document.querySelectorAll('body *'))
                        .filter((el) => isVisible(el))
                        .map((el) => {
                            const rawText = (el.textContent || '').trim();
                            const titleText = (el.getAttribute('title') || '').trim();
                            const mergedText = rawText || titleText;
                            const normalizedText = normalize(mergedText);
                            if (!normalizedText) return null;

                            const matchRank = normalizedText === expected
                                ? 0
                                : normalizedText.includes(expected)
                                    ? 1
                                    : 2;

                            if (matchRank > 1) return null;

                            const rect = el.getBoundingClientRect();
                            const style = window.getComputedStyle(el);
                            const selector = getSelector(el);
                            if (!selector) return null;

                            return {
                                Selector: selector,
                                Text: mergedText,
                                TagName: el.tagName.toLowerCase(),
                                IsVisible: true,
                                IsSemanticMenu: ['li', 'a', 'button', 'input'].includes(el.tagName.toLowerCase()),
                                Priority: matchRank,
                                Top: rect.top,
                                Area: Math.max(1, rect.width * rect.height),
                                NavigationContextScore: scoreNavigationContext(el),
                                HasPointerCursor: style.cursor === 'pointer',
                                HasNoiseKeywords: /(\u7528\u6237\u540d|\u5bc6\u7801|\u767b\u5f55|\u53d6\u6d88|\u5fd8\u8bb0\u5bc6\u7801|user|password|login|cancel)/i.test(mergedText),
                                LineCount: mergedText.split(/\r?\n/).filter(Boolean).length
                            };
                        })
                        .filter(Boolean)
                        .slice(0, 24);
                }
                """,
                text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("DOM 扫描 JS 执行失败: source={Source}, step={Step}, error={Error}",
                source, text, ex.Message);
            return candidates;
        }

        if (descriptors == null)
        {
            _logger.LogInformation("DOM 扫描返回 null: source={Source}, step={Step}", source, text);
            return candidates;
        }

        for (var index = 0; index < descriptors.Count; index++)
        {
            var descriptor = descriptors[index];
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Selector))
            {
                continue;
            }

            try
            {
                var locator = frame.Locator(descriptor.Selector).First;
                candidates.Add(new LocatorMatch(
                    new InteractiveNavigationCandidate(
                        descriptor.TagName ?? "unknown",
                        index,
                        descriptor.Text ?? string.Empty,
                        descriptor.IsVisible,
                        descriptor.IsSemanticMenu,
                        descriptor.Priority,
                        descriptor.Top,
                        descriptor.Area,
                        descriptor.NavigationContextScore,
                        descriptor.HasPointerCursor,
                        descriptor.HasNoiseKeywords,
                        descriptor.LineCount),
                    locator,
                    descriptor.Selector,
                    source));
            }
            catch
            {
                // Ignore invalid selectors emitted from the page scan.
            }
        }

        return candidates;
    }

    private static string EscapeForTextSelector(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string GetTagNameFromSelector(string selector)
    {
        var separatorIndex = selector.IndexOf(':');
        return separatorIndex > 0 ? selector[..separatorIndex] : selector;
    }

    private static bool HasNoiseKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var noiseKeywords = new[]
        {
            "\u7528\u6237\u540d",
            "\u5bc6\u7801",
            "\u767b\u5f55",
            "\u53d6\u6d88",
            "\u5fd8\u8bb0\u5bc6\u7801",
            "user",
            "password",
            "login",
            "cancel"
        };

        return noiseKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    private sealed record LocatorMatch(
        InteractiveNavigationCandidate Candidate,
        ILocator Locator,
        string Selector,
        string Source);

    private sealed class CandidateMetadata
    {
        public int NavigationContextScore { get; init; }
        public bool HasPointerCursor { get; init; }
    }

    private sealed class DomCandidateDescriptor
    {
        public string? Selector { get; init; }
        public string? Text { get; init; }
        public string? TagName { get; init; }
        public bool IsVisible { get; init; }
        public bool IsSemanticMenu { get; init; }
        public int Priority { get; init; }
        public double Top { get; init; }
        public double Area { get; init; }
        public int NavigationContextScore { get; init; }
        public bool HasPointerCursor { get; init; }
        public bool HasNoiseKeywords { get; init; }
        public int LineCount { get; init; }
    }

    private sealed class PageDiagnostic
    {
        public string? BodyTag { get; init; }
        public string? DocSnippet { get; init; }
        public int TotalElements { get; init; }
        public string? VisibleShortTexts { get; init; }
        public int FrameCount { get; init; }
        public string? PageUrl { get; init; }
    }
}
