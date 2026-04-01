# 可见字段浏览器诊断实施计划

> **给代理执行者：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按任务逐项执行本计划。步骤使用复选框 `- [ ]` 语法跟踪。

**目标：** 让浏览器诊断优先从页面可见标签和附近控件读取 GB28181 配置，并通过配置驱动的字段别名和厂商导航路径工作，同时保留 RPC2 和 LLM 作为兜底。

**架构：** 在 `GB28181Platform.Diagnostic` 中新增“可见字段提取层”，负责标准化页面可见标签、按别名映射到统一国标字段、并从邻近控件提取值。`CameraBrowserAgent` 调整为浏览器采集协调器：登录、按厂商路径导航、优先调用可见字段提取器；只有当可见字段命中不足时，才进入 RPC2 或 LLM 兜底流程。

**技术栈：** .NET 8、Microsoft.Playwright、xUnit、NSubstitute、ASP.NET Core 配置绑定

---

## 文件结构

### 新增文件

- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleGbConfig.cs`
  可见字段提取后的统一输出模型。
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldAliasOptions.cs`
  `Diagnostic:VisibleFieldAliases` 配置模型。
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/ManufacturerNavigationOptions.cs`
  `Diagnostic:Manufacturers` 配置模型。
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldExtractionResult.cs`
  提取结果模型，包含命中字段数、原始匹配、失败原因和成功标记。
- `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldConfigExtractor.cs`
  核心提取服务，负责标签归一化、别名匹配和邻近控件取值。
- `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs`
  单元测试：别名匹配、标签归一化、checkbox/select/password 处理、IP 分段合并。
- `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs`
  单元测试：配置绑定和厂商路径解析。

### 修改文件

- `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
  将浏览器诊断主流程切到可见字段提取。
- `src/GB28181Platform.Api/appsettings.json`
  新增 `VisibleFieldAliases` 和 `Manufacturers` 配置。
- `src/GB28181Platform.Api/Program.cs`
  注册可见字段提取服务及相关配置。
- `src/GB28181Platform.Diagnostic/Steps/BrowserCheckStep.cs`
  保持入口稳定，但让分析说明符合新的主路径。

---

### Task 1：新增配置模型和配置绑定测试

**文件：**
- Create: `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldAliasOptions.cs`
- Create: `src/GB28181Platform.Diagnostic/Browser/VisibleField/ManufacturerNavigationOptions.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs`

- [ ] **Step 1：先写失败测试，覆盖字段别名和导航配置绑定**

```csharp
using System.Text;
using GB28181Platform.Diagnostic.Browser.VisibleField;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class VisibleFieldNavigationOptionsTests
{
    [Fact]
    public void Bind_VisibleFieldAliases_BindsStandardFieldAliases()
    {
        var json = """
        {
          "Diagnostic": {
            "VisibleFieldAliases": {
              "SipServerIp": [ "SIP服务器地址", "SIP服务器IP", "SIP Server IP" ],
              "Enable": [ "接入使能", "启用" ]
            }
          }
        }
        """;

        var config = BuildConfig(json);
        var options = new VisibleFieldAliasOptions();
        config.GetSection("Diagnostic:VisibleFieldAliases").Bind(options.Fields);

        Assert.Equal(3, options.Fields["SipServerIp"].Count);
        Assert.Contains("接入使能", options.Fields["Enable"]);
    }

    [Fact]
    public void Bind_Manufacturers_BindsMultipleNavigationPaths()
    {
        var json = """
        {
          "Diagnostic": {
            "Manufacturers": {
              "dahua": {
                "NavigationPaths": [
                  [ "设置", "网络设置", "平台接入" ],
                  [ "设置", "网络", "平台接入" ]
                ]
              }
            }
          }
        }
        """;

        var config = BuildConfig(json);
        var options = new ManufacturerNavigationOptions();
        config.GetSection("Diagnostic:Manufacturers").Bind(options.Manufacturers);

        Assert.Equal(2, options.Manufacturers["dahua"].NavigationPaths.Count);
        Assert.Equal("平台接入", options.Manufacturers["dahua"].NavigationPaths[0][2]);
    }

    private static IConfigurationRoot BuildConfig(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build();
    }
}
```

- [ ] **Step 2：运行测试，确认确实失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldNavigationOptionsTests -v minimal`

预期：FAIL，原因是 `VisibleFieldAliasOptions` 和 `ManufacturerNavigationOptions` 还不存在。

- [ ] **Step 3：写最小实现，只补配置模型**

```csharp
namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class VisibleFieldAliasOptions
{
    public Dictionary<string, List<string>> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ManufacturerNavigationOptions
{
    public Dictionary<string, ManufacturerNavigationDefinition> Manufacturers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ManufacturerNavigationDefinition
{
    public List<List<string>> NavigationPaths { get; set; } = [];
}
```

- [ ] **Step 4：再次运行测试，确认转绿**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldNavigationOptionsTests -v minimal`

预期：PASS，显示 `2 passed`。

- [ ] **Step 5：提交**

```bash
git add tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldAliasOptions.cs src/GB28181Platform.Diagnostic/Browser/VisibleField/ManufacturerNavigationOptions.cs
git commit -m "test(diagnostic): add visible field config binding coverage"
```

---

### Task 2：用 TDD 构建可见字段提取核心

**文件：**
- Create: `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleGbConfig.cs`
- Create: `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldExtractionResult.cs`
- Create: `src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldConfigExtractor.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs`

- [ ] **Step 1：先写失败测试，覆盖标签匹配和控件取值**

```csharp
using GB28181Platform.Diagnostic.Browser.VisibleField;
using Xunit;

namespace GB28181Platform.Tests.Diagnostic;

public class VisibleFieldConfigExtractorTests
{
    [Fact]
    public void Extract_FromVisibleLabels_MapsAliasesToStandardFields()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = [ "SIP服务器地址", "SIP服务器IP", "SIP Server IP" ],
            ["SipServerId"] = [ "SIP服务器编号" ],
            ["Enable"] = [ "接入使能" ]
        });

        var html = """
        <div class="row">
          <span>SIP服务器地址</span><input value="188.18.35.191" />
          <span>SIP服务器编号</span><input value="34020000002000000001" />
          <span>接入使能</span><input type="checkbox" checked />
        </div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.True(result.IsSuccess);
        Assert.Equal("188.18.35.191", result.Config.SipServerIp);
        Assert.Equal("34020000002000000001", result.Config.SipServerId);
        Assert.True(result.Config.Enable);
        Assert.True(result.MatchedFields >= 3);
    }

    [Fact]
    public void Extract_FromSplitIpInputs_MergesIntoSingleIp()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["SipServerIp"] = [ "SIP服务器IP" ]
        });

        var html = """
        <div class="row">
          <span>SIP服务器IP</span>
          <input value="188" />
          <span>.</span>
          <input value="18" />
          <span>.</span>
          <input value="35" />
          <span>.</span>
          <input value="191" />
        </div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.Equal("188.18.35.191", result.Config.SipServerIp);
    }

    [Fact]
    public void Extract_FromSelectAndPassword_PreservesDisplayValues()
    {
        var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
        {
            ["Heartbeat"] = [ "心跳周期" ],
            ["RegisterExpiry"] = [ "注册有效期" ],
            ["SipServerPort"] = [ "SIP服务器端口" ]
        });

        var html = """
        <div class="row"><span>心跳周期</span><input value="60" /></div>
        <div class="row"><span>注册有效期</span><input value="3600" /></div>
        <div class="row"><span>SIP服务器端口</span><select><option>5060</option><option selected>15060</option></select></div>
        """;

        var result = extractor.ExtractFromHtml(html);

        Assert.Equal("60", result.Config.Heartbeat);
        Assert.Equal("3600", result.Config.RegisterExpiry);
        Assert.Equal("15060", result.Config.SipServerPort);
    }
}
```

- [ ] **Step 2：运行测试，确认失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldConfigExtractorTests -v minimal`

预期：FAIL，原因是提取模型和提取器还不存在。

- [ ] **Step 3：写最小实现，先让测试通过**

```csharp
namespace GB28181Platform.Diagnostic.Browser.VisibleField;

public class VisibleGbConfig
{
    public bool? Enable { get; set; }
    public string SipServerId { get; set; } = "";
    public string SipDomain { get; set; } = "";
    public string SipServerIp { get; set; } = "";
    public string SipServerPort { get; set; } = "";
    public string LocalSipPort { get; set; } = "";
    public string RegisterExpiry { get; set; } = "";
    public string Heartbeat { get; set; } = "";
    public string HeartbeatTimeoutCount { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public Dictionary<string, string> RawMatches { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class VisibleFieldExtractionResult
{
    public bool IsSuccess { get; set; }
    public int MatchedFields { get; set; }
    public string FailureReason { get; set; } = "";
    public VisibleGbConfig Config { get; set; } = new();
}
```

```csharp
using System.Text.RegularExpressions;
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
                var label = doc.All.FirstOrDefault(x => Normalize(x.TextContent) == Normalize(alias));
                if (label is null) continue;

                var value = TryReadNeighborValue(label);
                if (string.IsNullOrWhiteSpace(value) && field.Key != "Enable") continue;

                ApplyField(result.Config, field.Key, value, label);
                result.MatchedFields++;
                break;
            }
        }

        result.IsSuccess = result.MatchedFields >= 3 || !string.IsNullOrWhiteSpace(result.Config.SipServerIp);
        result.FailureReason = result.IsSuccess ? "" : "Visible field matches below threshold";
        return result;
    }

    private static string Normalize(string text) => Regex.Replace(text ?? "", @"\s+|：|:", "").Trim().ToLowerInvariant();

    private static string TryReadNeighborValue(AngleSharp.Dom.IElement label)
    {
        var parent = label.ParentElement;
        if (parent is null) return "";

        var controls = parent.QuerySelectorAll("input,select,textarea").ToList();
        if (controls.Count == 0) return "";

        if (controls.Count >= 4 && controls.Take(4).All(x => x.TagName.Equals("INPUT", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = controls.Take(4).Select(x => x.GetAttribute("value") ?? "").ToArray();
            if (parts.All(p => !string.IsNullOrWhiteSpace(p))) return string.Join(".", parts);
        }

        var control = controls.First();
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

        return control.GetAttribute("value") ?? "";
    }

    private static void ApplyField(VisibleGbConfig config, string fieldName, string value, AngleSharp.Dom.IElement label)
    {
        config.RawMatches[label.TextContent.Trim()] = value;
        switch (fieldName)
        {
            case "Enable": config.Enable = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
            case "SipServerId": config.SipServerId = value; break;
            case "SipDomain": config.SipDomain = value; break;
            case "SipServerIp": config.SipServerIp = value; break;
            case "SipServerPort": config.SipServerPort = value; break;
            case "LocalSipPort": config.LocalSipPort = value; break;
            case "RegisterExpiry": config.RegisterExpiry = value; break;
            case "Heartbeat": config.Heartbeat = value; break;
            case "HeartbeatTimeoutCount": config.HeartbeatTimeoutCount = value; break;
            case "DeviceId": config.DeviceId = value; break;
            case "ChannelId": config.ChannelId = value; break;
        }
    }
}
```

- [ ] **Step 4：添加 AngleSharp 依赖并重新运行测试**

修改 `tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj` 和 `src/GB28181Platform.Diagnostic/GB28181Platform.Diagnostic.csproj`：

```xml
<PackageReference Include="AngleSharp" Version="1.1.2" />
```

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldConfigExtractorTests -v minimal`

预期：PASS，显示 `3 passed`。

- [ ] **Step 5：提交**

```bash
git add tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj src/GB28181Platform.Diagnostic/GB28181Platform.Diagnostic.csproj src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleGbConfig.cs src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldExtractionResult.cs src/GB28181Platform.Diagnostic/Browser/VisibleField/VisibleFieldConfigExtractor.cs
git commit -m "feat(diagnostic): add visible field extraction core"
```

---

### Task 3：把扁平导航配置替换成厂商导航和字段别名配置

**文件：**
- Modify: `src/GB28181Platform.Api/appsettings.json`
- Modify: `src/GB28181Platform.Api/Program.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs`

- [ ] **Step 1：先写失败测试，覆盖新的配置结构**

将以下测试追加到 `VisibleFieldNavigationOptionsTests.cs`：

```csharp
[Fact]
public void Bind_NewDiagnosticConfigShape_ContainsKnownManufacturersAndAliases()
{
    var json = """
    {
      "Diagnostic": {
        "VisibleFieldAliases": {
          "SipServerIp": [ "SIP服务器地址", "SIP服务器IP", "SIP Server IP" ],
          "Heartbeat": [ "心跳周期", "Heartbeat" ]
        },
        "Manufacturers": {
          "hikvision": {
            "NavigationPaths": [
              [ "配置", "网络", "高级配置", "平台接入" ]
            ]
          }
        }
      }
    }
    """;

    var config = BuildConfig(json);
    var aliases = new VisibleFieldAliasOptions();
    var manufacturers = new ManufacturerNavigationOptions();
    config.GetSection("Diagnostic:VisibleFieldAliases").Bind(aliases.Fields);
    config.GetSection("Diagnostic:Manufacturers").Bind(manufacturers.Manufacturers);

    Assert.Contains("SIP Server IP", aliases.Fields["SipServerIp"]);
    Assert.Equal("平台接入", manufacturers.Manufacturers["hikvision"].NavigationPaths[0][3]);
}
```

- [ ] **Step 2：运行测试，确认当前配置假设不满足**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldNavigationOptionsTests -v minimal`

预期：FAIL，或保持红灯，直到 `appsettings.json` 和服务注册改完。

- [ ] **Step 3：最小化修改配置和注册逻辑**

将 `src/GB28181Platform.Api/appsettings.json` 中当前的 `Diagnostic` 节点替换为：

```json
"Diagnostic": {
  "BrowserCheckMode": "ai-dom",
  "VisibleFieldAliases": {
    "Enable": [ "接入使能", "启用", "使能", "Enable" ],
    "SipServerId": [ "SIP服务器编号", "服务器编号", "SIP Server ID", "Server ID" ],
    "SipDomain": [ "SIP域", "域", "SIP Domain", "Realm" ],
    "SipServerIp": [ "SIP服务器地址", "SIP服务器IP", "SIP Server IP", "Server IP" ],
    "SipServerPort": [ "SIP服务器端口", "服务器端口", "SIP Port", "Server Port" ],
    "LocalSipPort": [ "本地SIP服务器端口", "本地SIP端口", "本地端口", "Local SIP Port" ],
    "RegisterExpiry": [ "注册有效期", "有效期", "Expires" ],
    "Heartbeat": [ "心跳周期", "心跳间隔", "Heartbeat" ],
    "HeartbeatTimeoutCount": [ "最大心跳超时次数", "心跳超时次数" ],
    "DeviceId": [ "设备编号", "设备ID", "Device ID" ],
    "ChannelId": [ "通道编号", "通道ID", "Channel ID" ]
  },
  "Manufacturers": {
    "dahua": {
      "NavigationPaths": [
        [ "设置", "网络设置", "平台接入" ],
        [ "设置", "网络", "平台接入" ]
      ]
    },
    "hikvision": {
      "NavigationPaths": [
        [ "配置", "网络", "高级配置", "平台接入" ],
        [ "配置", "网络", "平台接入" ]
      ]
    },
    "uniview": {
      "NavigationPaths": [
        [ "设置", "网络", "平台接入" ]
      ]
    }
  }
}
```

在 `Program.cs` 中注册：

```csharp
builder.Services.AddSingleton(sp =>
{
    var aliasOptions = new VisibleFieldAliasOptions();
    builder.Configuration.GetSection("Diagnostic:VisibleFieldAliases").Bind(aliasOptions.Fields);

    var navigationOptions = new ManufacturerNavigationOptions();
    builder.Configuration.GetSection("Diagnostic:Manufacturers").Bind(navigationOptions.Manufacturers);

    return new VisibleFieldConfigExtractor(aliasOptions.Fields);
});
```

- [ ] **Step 4：重新运行测试，确认配置绑定通过**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldNavigationOptionsTests -v minimal`

预期：PASS，显示 `3 passed`。

- [ ] **Step 5：提交**

```bash
git add src/GB28181Platform.Api/appsettings.json src/GB28181Platform.Api/Program.cs tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs
git commit -m "feat(diagnostic): configure visible field aliases and navigation paths"
```

---

### Task 4：将可见字段提取切为浏览器诊断主路径

**文件：**
- Modify: `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- Modify: `src/GB28181Platform.Diagnostic/Steps/BrowserCheckStep.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs`

- [ ] **Step 1：先写失败测试，覆盖主路径判定规则**

将以下测试追加到 `VisibleFieldConfigExtractorTests.cs`：

```csharp
[Fact]
public void Extract_WhenMatchedFieldsBelowThreshold_ReturnsFailureReason()
{
    var extractor = VisibleFieldConfigExtractor.CreateForTests(new Dictionary<string, List<string>>
    {
        ["SipServerIp"] = [ "SIP服务器IP" ],
        ["SipServerId"] = [ "SIP服务器编号" ],
        ["Heartbeat"] = [ "心跳周期" ]
    });

    var html = """
    <div><span>无关字段</span><input value="x" /></div>
    <div><span>SIP服务器IP</span><input value="188.18.35.191" /></div>
    """;

    var result = extractor.ExtractFromHtml(html);

    Assert.False(result.IsSuccess);
    Assert.Equal("Visible field matches below threshold", result.FailureReason);
}
```

- [ ] **Step 2：运行测试，确认失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldConfigExtractorTests -v minimal`

预期：FAIL，因为当前成功规则还允许只命中一个 IP 字段就算成功。

- [ ] **Step 3：收紧成功阈值，并把提取器接入 `CameraBrowserAgent` 主流程**

更新提取器成功规则：

```csharp
result.IsSuccess = result.MatchedFields >= 3;
result.FailureReason = result.IsSuccess ? "" : "Visible field matches below threshold";
```

在 `CameraBrowserAgent` 中新增方法：

```csharp
private async Task<VisibleFieldExtractionResult> TryExtractVisibleConfigAsync(IPage page)
{
    var html = await page.EvaluateAsync<string>(@"() => document.body ? document.body.innerHTML : ''");
    return _visibleFieldExtractor.ExtractFromHtml(html ?? "");
}
```

将 `CheckCameraConfigByAiDomAsync` 中的主流程调整为：

```csharp
var visibleResult = await TryExtractVisibleConfigAsync(page);
if (visibleResult.IsSuccess)
{
    var comparePrompt = $@"以下是从摄像机国标配置页面可见字段提取到的结果：
SIP服务器IP: {visibleResult.Config.SipServerIp}
SIP服务器编号: {visibleResult.Config.SipServerId}
SIP域: {visibleResult.Config.SipDomain}
本地SIP端口: {visibleResult.Config.LocalSipPort}
心跳周期: {visibleResult.Config.Heartbeat}

平台期望配置：
- SIP 服务器 IP: {expectedSipServerIp}
- 服务器编码(Server ID): {expectedServerId}

请用中文输出逐项对比结论。";

    var compareResp = await _qwen.ChatAsync(new List<ChatMessage>
    {
        new() { Role = "user", Content = comparePrompt }
    });

    return new BrowserCheckResult
    {
        Success = true,
        Analysis = compareResp.Content ?? "可见字段采集成功，但 AI 未返回分析"
    };
}
```

只有这条主路径失败后，才继续进入 RPC2 和其他兜底分支。

- [ ] **Step 4：重新运行提取器测试，确认新规则通过**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldConfigExtractorTests -v minimal`

预期：PASS，显示 `4 passed`。

- [ ] **Step 5：提交**

```bash
git add src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs src/GB28181Platform.Diagnostic/Steps/BrowserCheckStep.cs tests/GB28181Platform.Tests/Diagnostic/VisibleFieldConfigExtractorTests.cs
git commit -m "feat(diagnostic): use visible field extraction as primary browser path"
```

---

### Task 5：补导航日志、兜底边界日志和最终验证

**文件：**
- Modify: `src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs`
- Test: `tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs`

- [ ] **Step 1：先写失败测试，覆盖厂商路径查找**

将以下测试追加到 `VisibleFieldNavigationOptionsTests.cs`：

```csharp
[Fact]
public void ManufacturerNavigationDefinition_ReturnsConfiguredPathsForKnownManufacturer()
{
    var options = new ManufacturerNavigationOptions
    {
        Manufacturers = new Dictionary<string, ManufacturerNavigationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["dahua"] = new()
            {
                NavigationPaths =
                [
                    [ "设置", "网络设置", "平台接入" ],
                    [ "设置", "网络", "平台接入" ]
                ]
            }
        }
    };

    Assert.Equal(2, options.Manufacturers["DAHUA"].NavigationPaths.Count);
}
```

- [ ] **Step 2：运行测试，确认大小写不敏感行为正确**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleFieldNavigationOptionsTests -v minimal`

预期：PASS。如果失败，修正字典初始化逻辑。

- [ ] **Step 3：补主路径和兜底边界日志**

在 `CameraBrowserAgent.cs` 中添加类似日志：

```csharp
_logger.LogInformation("Visible-field path: trying manufacturer {Manufacturer}", manufacturer);
_logger.LogInformation("Visible-field path: trying navigation path {Path}", string.Join(" -> ", steps));
_logger.LogInformation("Visible-field path: matched {Count} standard fields", visibleResult.MatchedFields);
_logger.LogWarning("Visible-field path failed: {Reason}", visibleResult.FailureReason);
_logger.LogInformation("Fallback path: trying RPC2 extraction");
_logger.LogInformation("Fallback path: trying LLM interpretation");
```

并且每个字段命中都记录：

```csharp
_logger.LogInformation("Visible-field match: {Field} <= {RawLabel} => {Value}", fieldName, rawLabel, value);
```

- [ ] **Step 4：运行目标测试和完整构建验证**

运行：

```bash
dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~VisibleField -v minimal
dotnet build GB28181Platform.sln -nologo
```

预期：

- visible-field 相关测试全部 PASS
- 整个解决方案构建成功，输出 `Build succeeded.`

- [ ] **Step 5：提交**

```bash
git add src/GB28181Platform.Diagnostic/Browser/CameraBrowserAgent.cs tests/GB28181Platform.Tests/Diagnostic/VisibleFieldNavigationOptionsTests.cs
git commit -m "fix(diagnostic): add visible field path logging and fallback boundaries"
```

---

## 规格覆盖检查

- 页面可见标签作为主信号：Task 2、Task 4 覆盖。
- 配置驱动字段 aliases：Task 1、Task 3 覆盖。
- 厂商导航路径：Task 1、Task 3、Task 5 覆盖。
- 统一标准字段模型：Task 2 覆盖。
- 可见字段为主、RPC2/LLM 为兜底：Task 4 覆盖。
- 日志和失败原因：Task 5 覆盖。
- TDD 和回归覆盖：所有任务都按测试先行拆解。

## 占位词检查

- 没有 `TODO`、`TBD`、`implement later` 这类占位词。
- 每个涉及改代码的步骤都给出了具体代码块。
- 每个测试步骤都给出了精确命令和预期结果。

## 类型一致性检查

- `VisibleFieldAliasOptions.Fields` 在 Task 1、Task 3、Task 4 中保持一致。
- `ManufacturerNavigationOptions.Manufacturers` 和 `ManufacturerNavigationDefinition.NavigationPaths` 在 Task 1、Task 3、Task 5 中保持一致。
- `VisibleGbConfig` 和 `VisibleFieldExtractionResult` 在 Task 2、Task 4 中保持一致。
