# 交互式 Playwright Live DOM 诊断实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将浏览器诊断主路径替换为交互式 Playwright live DOM 代理，使其像人工操作一样导航页面，并直接从可见控件中提取 GB28181 配置。

**Architecture:** 保留现有浏览器诊断入口和配置模型，但将主路径从“静态 HTML 优先提取”调整为三段式流水线：交互式导航、目标页识别、live DOM 字段提取。现有可见字段提取与截图/RPC2 路径继续保留为辅助或兜底，而不是主控制流。

**Tech Stack:** .NET 8、Microsoft.Playwright、xUnit，以及现有浏览器诊断服务与配置绑定。

---

## 文件结构

**新建:**
- `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationCandidate.cs`
- `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationStepResult.cs`
- `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractivePageStateSnapshot.cs`
- `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractivePageStateComparer.cs`
- `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationAgent.cs`
- `src/GB28181Platform.Diagnostic/Browser/Interactive/LiveDomFieldReader.cs`
- `tests/GB28181Platform.Tests/Diagnostic/InteractiveNavigationCandidateSelectorTests.cs`
- `tests/GB28181Platform.Tests/Diagnostic/InteractivePageStateComparerTests.cs`
- `tests/GB28181Platform.Tests/Diagnostic/LiveDomFieldReaderTests.cs`

**修改:**
- `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldConfigExtractor.cs`
- `src/GB28181Platform.Api/Program.cs`
- `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldBrowserWorkflowTests.cs`

**保留为兜底 / 复用:**
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldPageDetector.cs`
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldNavigationResolver.cs`
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleGbConfig.cs`

---

### Task 1: 新增导航候选与页面状态基础类型

**Files:**
- Create: `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationCandidate.cs`
- Create: `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationStepResult.cs`
- Create: `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractivePageStateSnapshot.cs`
- Create: `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractivePageStateComparer.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/InteractiveNavigationCandidateSelectorTests.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/InteractivePageStateComparerTests.cs`

- [ ] **Step 1: 先写候选排序与页面状态变化检测的失败测试**

```csharp
using GB28181Platform.Diagnostic.Browser.Interactive;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class InteractiveNavigationCandidateSelectorTests
{
    [Fact]
    public void SelectBest_PrefersVisibleExactMenuCandidate()
    {
        InteractiveNavigationCandidate[] candidates =
        [
            new("div", 0, "设置", true, false, 6),
            new("button", 0, "设置", true, true, 1),
            new("span", 0, "设置管理", true, false, 4)
        ];

        var best = InteractiveNavigationCandidate.SelectBest(candidates, "设置");

        Assert.NotNull(best);
        Assert.Equal("button", best!.TagName);
        Assert.True(best.IsSemanticMenu);
    }
}
```

```csharp
using GB28181Platform.Diagnostic.Browser.Interactive;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class InteractivePageStateComparerTests
{
    [Fact]
    public void HasMeaningfulChange_ReturnsTrue_WhenVisibleTextChanges()
    {
        var before = new InteractivePageStateSnapshot("/index", "预览 回放 设置", ["预览", "回放", "设置"]);
        var after = new InteractivePageStateSnapshot("/index", "预览 回放 设置 网络设置", ["预览", "回放", "设置", "网络设置"]);

        Assert.True(InteractivePageStateComparer.HasMeaningfulChange(before, after));
    }

    [Fact]
    public void HasMeaningfulChange_ReturnsFalse_WhenStateIsEquivalent()
    {
        var before = new InteractivePageStateSnapshot("/index", "预览 回放 设置", ["预览", "回放", "设置"]);
        var after = new InteractivePageStateSnapshot("/index", "预览 回放 设置", ["预览", "回放", "设置"]);

        Assert.False(InteractivePageStateComparer.HasMeaningfulChange(before, after));
    }
}
```

- [ ] **Step 2: 运行测试，确认测试按预期失败**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~InteractiveNavigationCandidateSelectorTests -v minimal`

Expected: FAIL，因为新的交互式基础类型还不存在。

- [ ] **Step 3: 添加最小可用的交互式基础类型实现**

```csharp
namespace GB28181Platform.Diagnostic.Browser.Interactive;

public sealed record InteractiveNavigationCandidate(
    string TagName,
    int Index,
    string Text,
    bool IsVisible,
    bool IsSemanticMenu,
    int Priority)
{
    public static InteractiveNavigationCandidate? SelectBest(
        IEnumerable<InteractiveNavigationCandidate> candidates,
        string expectedText)
    {
        var normalizedExpected = Normalize(expectedText);

        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .OrderByDescending(x => x.IsVisible)
            .ThenByDescending(x => x.IsSemanticMenu)
            .ThenBy(x => MatchRank(x.Text, normalizedExpected))
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Text.Length)
            .FirstOrDefault();
    }

    private static int MatchRank(string text, string expected)
    {
        var normalized = Normalize(text);
        if (string.Equals(normalized, expected, StringComparison.OrdinalIgnoreCase)) return 0;
        if (normalized.Contains(expected, StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private static string Normalize(string value) =>
        string.Concat((value ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch)));
}
```

```csharp
namespace GB28181Platform.Diagnostic.Browser.Interactive;

public sealed record InteractivePageStateSnapshot(
    string UrlPath,
    string VisibleText,
    IReadOnlyList<string> VisibleTokens);
```

```csharp
namespace GB28181Platform.Diagnostic.Browser.Interactive;

public static class InteractivePageStateComparer
{
    public static bool HasMeaningfulChange(
        InteractivePageStateSnapshot before,
        InteractivePageStateSnapshot after)
    {
        if (!string.Equals(before.UrlPath, after.UrlPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(before.VisibleText, after.VisibleText, StringComparison.Ordinal))
        {
            return true;
        }

        return before.VisibleTokens.Count != after.VisibleTokens.Count;
    }
}
```

```csharp
namespace GB28181Platform.Diagnostic.Browser.Interactive;

public sealed record InteractiveNavigationStepResult(
    string Step,
    bool Success,
    string Selector,
    string MatchedText,
    string Source,
    string FailureReason = "");
```

- [ ] **Step 4: 运行聚焦测试，确认其通过**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~InteractiveNavigationCandidateSelectorTests -v minimal`

Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add tests/GB28181Platform.Tests/Diagnostic/InteractiveNavigationCandidateSelectorTests.cs tests/GB28181Platform.Tests/Diagnostic/InteractivePageStateComparerTests.cs src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationCandidate.cs src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationStepResult.cs src/GB28181Platform.Diagnostic/Browser/Interactive/InteractivePageStateSnapshot.cs src/GB28181Platform.Diagnostic/Browser/Interactive/InteractivePageStateComparer.cs
git commit -m "feat(diagnostic): add interactive navigation primitives"
```

---

### Task 2: 构建交互式导航代理

**Files:**
- Create: `src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationAgent.cs`
- Modify: `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldBrowserWorkflowTests.cs`

- [ ] **Step 1: 先写交互式导航行为的失败测试**

```csharp
[Fact]
public void NavigationPlan_UsesConfiguredStepsInOrder()
{
    var options = new ManufacturerNavigationOptions
    {
        Manufacturers = new Dictionary<string, ManufacturerNavigationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["dahua"] = new()
            {
                NavigationPaths =
                [
                    ["设置", "网络设置", "平台接入", "国标28181"]
                ]
            }
        }
    };

    var paths = VisibleFieldNavigationResolver.Resolve(options, "dahua");

    Assert.Single(paths);
    Assert.Equal("设置", paths[0][0]);
    Assert.Equal("国标28181", paths[0][3]);
}
```

```csharp
[Fact]
public void InteractiveNavigationAgent_StopsAfterMeaningfulStateChangeAndSuccessfulStep()
{
    var before = new InteractivePageStateSnapshot("/index", "预览 回放 设置", ["预览", "回放", "设置"]);
    var after = new InteractivePageStateSnapshot("/index", "预览 回放 设置 网络设置", ["预览", "回放", "设置", "网络设置"]);

    Assert.True(InteractivePageStateComparer.HasMeaningfulChange(before, after));
}
```

- [ ] **Step 2: 运行测试，确认现有工作流按预期失败**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldBrowserWorkflowTests -v minimal`

Expected: FAIL，因为当前工作流仍然使用直接按文本点击，尚未接入交互式状态校验。

- [ ] **Step 3: 实现交互式导航代理**

```csharp
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
        var before = await CaptureSnapshotAsync(page);
        var locator = page.GetByText(step, new() { Exact = true }).First;

        if (!await locator.IsVisibleAsync())
        {
            return new InteractiveNavigationStepResult(step, false, "text=exact", step, "main", "未找到可见菜单项");
        }

        await locator.ScrollIntoViewIfNeededAsync();
        await locator.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1200, cancellationToken);

        var after = await CaptureSnapshotAsync(page);
        if (!InteractivePageStateComparer.HasMeaningfulChange(before, after))
        {
            return new InteractiveNavigationStepResult(step, false, "text=exact", step, "main", "点击后页面未发生有效变化");
        }

        return new InteractiveNavigationStepResult(step, true, "text=exact", step, "main");
    }

    private static async Task<InteractivePageStateSnapshot> CaptureSnapshotAsync(IPage page)
    {
        var urlPath = new Uri(page.Url).AbsolutePath;
        var visibleText = await page.EvaluateAsync<string>("() => document.body.innerText || ''") ?? string.Empty;
        var tokens = visibleText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length <= 30)
            .Take(80)
            .ToArray();

        return new InteractivePageStateSnapshot(urlPath, visibleText, tokens);
    }
}
```

```csharp
// CameraBrowserAgent.cs
var navigationAgent = new InteractiveNavigationAgent(_logger);
var navigationResults = await navigationAgent.NavigateAsync(page, navigationPath);
foreach (var stepResult in navigationResults)
{
    if (stepResult.Success)
    {
        _logger.LogInformation("交互式导航成功: {Step} -> {Source} -> {Selector}", stepResult.Step, stepResult.Source, stepResult.Selector);
    }
    else
    {
        _logger.LogWarning("交互式导航失败: {Step}, 原因={Reason}", stepResult.Step, stepResult.FailureReason);
    }
}
```

- [ ] **Step 4: 运行工作流测试并完成构建验证**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldBrowserWorkflowTests -v minimal`

Expected: PASS

Run: `dotnet build GB28181Platform.sln -nologo`

Expected: `0 warning, 0 error`

- [ ] **Step 5: 提交**

```bash
git add src/GB28181Platform.Diagnostic/Browser/Interactive/InteractiveNavigationAgent.cs src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs tests/GB28181Platform.Tests/Diagnostic/VisibleFieldBrowserWorkflowTests.cs
git commit -m "feat(diagnostic): add interactive playwright navigation agent"
```

---

### Task 3: 新增 Live DOM 字段读取器并提升为主提取路径

**Files:**
- Create: `src/GB28181Platform.Diagnostic/Browser/Interactive/LiveDomFieldReader.cs`
- Modify: `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- Modify: `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldConfigExtractor.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/LiveDomFieldReaderTests.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs`

- [ ] **Step 1: 先写 live DOM 提取的失败测试**

```csharp
using GB28181Platform.Diagnostic.Browser.VisibleField;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class LiveDomFieldReaderTests
{
    [Fact]
    public void ExtractVisibleConfig_CombinesSplitIpInputsIntoSingleValue()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = ["SIP服务器IP"],
            ["SipServerId"] = ["SIP服务器编号"],
            ["Enable"] = ["接入使能"]
        });

        var html = """
        <div class="ui-form-item">
          <label>SIP服务器IP</label>
          <div class="u-input-group u-ip">
            <input value="188" />
            <input value="18" />
            <input value="33" />
            <input value="138" />
          </div>
        </div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.Equal("188.18.33.138", result.Config.SipServerIp);
    }
}
```

- [ ] **Step 2: 运行测试，确认测试按预期失败**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~LiveDomFieldReaderTests -v minimal`

Expected: FAIL，因为 live DOM 字段读取器还不存在。

- [ ] **Step 3: 实现 live DOM 字段读取器，并将主路径切换为使用它**

```csharp
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

    public async Task<VisibleFieldExtractionResult> ReadAsync(IPage page)
    {
        var html = await page.EvaluateAsync<string>(@"() => {
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
                    if (source.checked) target.setAttribute('checked', 'checked');
                    else target.removeAttribute('checked');
                }
                if (target.tagName === 'SELECT') {
                    const targetOptions = target.querySelectorAll('option');
                    for (let j = 0; j < targetOptions.length; j++) {
                        if (j === source.selectedIndex) targetOptions[j].setAttribute('selected', 'selected');
                        else targetOptions[j].removeAttribute('selected');
                    }
                }
            }
            return clone.outerHTML;
        }") ?? string.Empty;

        return _extractor.ExtractFromHtml(html);
    }
}
```

```csharp
// CameraBrowserAgent.cs
var liveDomReader = new LiveDomFieldReader(_visibleFieldExtractor);
var extraction = await liveDomReader.ReadAsync(page);
if (extraction.IsSuccess)
{
    return new BrowserCheckResult
    {
        Success = true,
        Analysis = AnalyzeVisibleConfig(extraction.Config, expectedSipServerIp, expectedServerId)
    };
}
```

- [ ] **Step 4: 运行提取测试并完成整体验证**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~LiveDomFieldReaderTests -v minimal`

Expected: PASS

Run: `dotnet build GB28181Platform.sln -nologo`

Expected: `0 warning, 0 error`

- [ ] **Step 5: 提交**

```bash
git add src/GB28181Platform.Diagnostic/Browser/Interactive/LiveDomFieldReader.cs src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldConfigExtractor.cs tests/GB28181Platform.Tests/Diagnostic/LiveDomFieldReaderTests.cs tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs
git commit -m "feat(diagnostic): switch main path to live dom field reader"
```

---

### Task 4: 通过 DI 接入新的主路径，并保留兜底链路

**Files:**
- Modify: `src/GB28181Platform.Api/Program.cs`
- Modify: `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- Modify: `src/GB28181Platform.Diagnostic/Steps/BrowserCheckStep.cs`

- [ ] **Step 1: 在现有工作流测试中先写失败预期**

```csharp
[Fact]
public void BrowserCheckMode_DefaultsToDom()
{
    var mode = ("dom").ToLowerInvariant();
    Assert.Equal("dom", mode);
}
```

```csharp
[Fact]
public void NavigationResolver_ReturnsManufacturerPaths()
{
    var options = new ManufacturerNavigationOptions
    {
        Manufacturers = new Dictionary<string, ManufacturerNavigationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["dahua"] = new()
            {
                NavigationPaths =
                [
                    ["设置", "网络设置", "平台接入", "国标28181"]
                ]
            }
        }
    };

    var paths = VisibleFieldNavigationResolver.Resolve(options, "dahua");

    Assert.Single(paths);
}
```

- [ ] **Step 2: 运行测试，确认当前工作流仍依赖旧路径排序**

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldBrowserWorkflowTests -v minimal`

Expected: FAIL，直到交互式路径被完整接入为主路径。

- [ ] **Step 3: 注册并接入新的主路径**

```csharp
// Program.cs
builder.Services.AddSingleton<VisibleFieldConfigExtractor>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return VisibleFieldConfigExtractor.FromConfiguration(configuration.GetSection("Diagnostic:VisibleFieldAliases"));
});
builder.Services.AddSingleton<CameraBrowserAgent>();
```

```csharp
// BrowserCheckStep.cs
var mode = (_config["Diagnostic:BrowserCheckMode"] ?? "dom").ToLowerInvariant();
```

```csharp
// CameraBrowserAgent.cs main dom path ordering
var navigationPaths = VisibleFieldNavigationResolver.Resolve(_manufacturerNavigationOptions, manufacturer);
var extraction = await TryExtractVisibleFieldConfigAsync(page, navigationPaths);
if (extraction.IsSuccess)
{
    return new BrowserCheckResult
    {
        Success = true,
        Analysis = AnalyzeVisibleConfig(extraction.Config, expectedSipServerIp, expectedServerId)
    };
}

// existing RPC2 / screenshot branches remain below as fallback
```

- [ ] **Step 4: 运行全量解决方案验证**

Run: `dotnet build GB28181Platform.sln -nologo`

Expected: `0 warning, 0 error`

Run: `dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj -v minimal`

Expected: all tests pass

- [ ] **Step 5: 提交**

```bash
git add src/GB28181Platform.Api/Program.cs src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs src/GB28181Platform.Diagnostic/Steps/BrowserCheckStep.cs
git commit -m "refactor(diagnostic): promote interactive dom path and preserve fallbacks"
```

---

### Task 5: 在大华真机上验证并留存回归证据

**Files:**
- Modify: `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- Modify: `src/GB28181Platform.Api/appsettings.json`
- Test/Verify: `src/GB28181Platform.Api/bin/Debug/net8.0/logs/gb28181-*.log`

- [ ] **Step 1: 补充每一步导航的最终证据型日志**

```csharp
_logger.LogInformation(
    "交互式导航步骤: step={Step}, selector={Selector}, source={Source}, success={Success}, reason={Reason}",
    stepResult.Step,
    stepResult.Selector,
    stepResult.Source,
    stepResult.Success,
    stepResult.FailureReason);
```

```csharp
_logger.LogInformation(
    "live DOM 提取结果: matched={Count}, fields={Fields}",
    extraction.MatchedFields,
    string.Join("; ", extraction.Config.RawMatches.Select(x => $"{x.Key}={x.Value}")));
```

- [ ] **Step 2: 构建并重启 API 可执行文件**

Run: `dotnet build GB28181Platform.sln -nologo`

Expected: `0 warning, 0 error`

Then restart:

```powershell
Stop-Process -Name GB28181Platform.Api -Force
Start-Process "d:\Project\RailTransit\轨道交通_智慧城市\SPI\02 源码\branches\fengjianhui\VMS\Platform\newvmsplat\.worktrees\visible-field-browser-diagnostic\src\GB28181Platform.Api\bin\Debug\net8.0\GB28181Platform.Api.exe"
```

- [ ] **Step 3: 执行大华设备诊断**

Trigger through the existing front end or:

```powershell
Invoke-WebRequest -Uri "http://localhost:5000/api/diagnostic/run/34020000001320000001" -Method Post -UseBasicParsing
```

Expected: 浏览器检查成功进入 `设置 -> 网络设置 -> 平台接入 -> 国标28181`

- [ ] **Step 4: 校验日志证据**

Check:

```powershell
Get-Content "d:\Project\RailTransit\轨道交通_智慧城市\SPI\02 源码\branches\fengjianhui\VMS\Platform\newvmsplat\.worktrees\visible-field-browser-diagnostic\src\GB28181Platform.Api\bin\Debug\net8.0\logs\gb28181-20260330.log" -Tail 120
```

Expected evidence:
- `交互式导航步骤` for all four Dahua steps
- target page reaches `国标28181`
- `live DOM 提取结果` contains SIP server IP / server ID / enable

- [ ] **Step 5: 提交**

```bash
git add src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs src/GB28181Platform.Api/appsettings.json
git commit -m "test(diagnostic): validate interactive dom path on dahua device"
```
