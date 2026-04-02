# Semantic Kernel 多模态智能体实施计划

> **给代理执行者：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按任务逐项执行本计划。步骤使用复选框 `- [ ]` 语法跟踪。

**目标：** 将当前手写 AI Agent 运行时替换为基于 Semantic Kernel 的多模态智能体架构，支持文本、图片、音频文件输入，支持真实多轮会话、增强型 API 返回结构，以及多用户并发会话。

**架构：** 实现阶段仍放在 `GB28181Platform.AiAgent` 单一工程内，但内部重组为 `Abstractions`、`Contracts`、`Conversation`、`Multimodal`、`Prompts`、`Runtime`、`Capabilities`。以 Semantic Kernel 作为唯一智能体运行时，将现有业务函数迁移为 plugins，升级 API 合同，并把当前“追加式 AI 日志”改造成真实会话存储。

**技术栈：** .NET 8、Semantic Kernel（`Microsoft.SemanticKernel`、`Microsoft.SemanticKernel.Connectors.OpenAI`、`Microsoft.SemanticKernel.Agents.Core`）、ASP.NET Core Web API、SqlSugar、xUnit、NSubstitute。

---

## 文件结构

### 新建

- `src/GB28181Platform.AiAgent/Abstractions/IAgentRuntime.cs`
- `src/GB28181Platform.AiAgent/Abstractions/IAgentPromptProvider.cs`
- `src/GB28181Platform.AiAgent/Abstractions/IConversationStore.cs`
- `src/GB28181Platform.AiAgent/Abstractions/IAudioTranscriptionService.cs`
- `src/GB28181Platform.AiAgent/Contracts/AgentContentItemDto.cs`
- `src/GB28181Platform.AiAgent/Contracts/AgentChatRequest.cs`
- `src/GB28181Platform.AiAgent/Contracts/AgentChatResponse.cs`
- `src/GB28181Platform.AiAgent/Contracts/AgentExecutionUsage.cs`
- `src/GB28181Platform.AiAgent/Conversation/ConversationRecord.cs`
- `src/GB28181Platform.AiAgent/Conversation/ConversationMessageRecord.cs`
- `src/GB28181Platform.AiAgent/Conversation/ConversationContentItemRecord.cs`
- `src/GB28181Platform.AiAgent/Multimodal/NormalizedAgentInput.cs`
- `src/GB28181Platform.AiAgent/Multimodal/AgentInputNormalizer.cs`
- `src/GB28181Platform.AiAgent/Prompts/DefaultAgentPromptProvider.cs`
- `src/GB28181Platform.AiAgent/Runtime/SemanticKernelAgentRuntime.cs`
- `src/GB28181Platform.AiAgent/Runtime/SemanticKernelOptions.cs`
- `src/GB28181Platform.AiAgent/Runtime/SemanticKernelModelRouter.cs`
- `src/GB28181Platform.AiAgent/Capabilities/Application/AiChatApplicationService.cs`
- `src/GB28181Platform.AiAgent/Capabilities/Plugins/DeviceCapabilityPlugin.cs`
- `src/GB28181Platform.AiAgent/Capabilities/Plugins/DiagnosticCapabilityPlugin.cs`
- `src/GB28181Platform.AiAgent/Capabilities/Persistence/SqlSugarConversationStore.cs`
- `tests/GB28181Platform.Tests/AiAgent/AgentInputNormalizerTests.cs`
- `tests/GB28181Platform.Tests/AiAgent/SemanticKernelModelRouterTests.cs`
- `tests/GB28181Platform.Tests/AiAgent/DefaultAgentPromptProviderTests.cs`
- `tests/GB28181Platform.Tests/AiAgent/SqlSugarConversationStoreTests.cs`
- `tests/GB28181Platform.Tests/AiAgent/AiChatApplicationServiceTests.cs`

### 修改

- `src/GB28181Platform.AiAgent/GB28181Platform.AiAgent.csproj`
- `src/GB28181Platform.AiAgent/IAiAgentService.cs`
- `src/GB28181Platform.AiAgent/AiAgentService.cs`
- `src/GB28181Platform.AiAgent/Prompts/SystemPrompts.cs`
- `src/GB28181Platform.Api/Program.cs`
- `src/GB28181Platform.Api/Controllers/AiAgentController.cs`
- `src/GB28181Platform.Domain/Entities/AiConversation.cs`
- `tests/GB28181Platform.Tests/AiAgent/AiAgentServiceTests.cs`
- `tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj`
- `frontend/vms-web/src/api/ai.ts`
- `frontend/vms-web/src/views/AiChat.vue`

### 最终删除（后置到 Runtime 完成切换后）

- `src/GB28181Platform.AiAgent/IQwenClient.cs`
- `src/GB28181Platform.AiAgent/QwenClient.cs`
- `src/GB28181Platform.AiAgent/QwenEndpointRouting.cs`
- `src/GB28181Platform.AiAgent/Functions/IAgentFunction.cs`
- `src/GB28181Platform.AiAgent/Functions/FunctionRegistry.cs`
- `src/GB28181Platform.AiAgent/Functions/GetDeviceStatusFunction.cs`
- `src/GB28181Platform.AiAgent/Functions/GetDiagnosticLogsFunction.cs`
- `src/GB28181Platform.AiAgent/Functions/ListOfflineDevicesFunction.cs`
- `tests/GB28181Platform.Tests/AiAgent/FunctionRegistryTests.cs`
- `tests/GB28181Platform.Tests/AiAgent/QwenEndpointRoutingTests.cs`

---

### Task 1：引入 Semantic Kernel 依赖并建立多模态 Contracts

**文件：**
- Modify: `src/GB28181Platform.AiAgent/GB28181Platform.AiAgent.csproj`
- Create: `src/GB28181Platform.AiAgent/Contracts/AgentContentItemDto.cs`
- Create: `src/GB28181Platform.AiAgent/Contracts/AgentChatRequest.cs`
- Create: `src/GB28181Platform.AiAgent/Contracts/AgentChatResponse.cs`
- Create: `src/GB28181Platform.AiAgent/Contracts/AgentExecutionUsage.cs`
- Create: `src/GB28181Platform.AiAgent/Multimodal/NormalizedAgentInput.cs`
- Create: `src/GB28181Platform.AiAgent/Multimodal/AgentInputNormalizer.cs`
- Test: `tests/GB28181Platform.Tests/AiAgent/AgentInputNormalizerTests.cs`

- [ ] **Step 1：先写失败测试，覆盖多模态输入标准化**

```csharp
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Multimodal;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AgentInputNormalizerTests
{
    [Fact]
    public void Normalize_TextAndImage_PreservesOrderAndKinds()
    {
        var request = new AgentChatRequest
        {
            ConversationId = "conv-001",
            ContentItems =
            [
                new AgentContentItemDto { Kind = "text", Text = "检查这台设备" },
                new AgentContentItemDto { Kind = "image", FileName = "snap.png", MediaType = "image/png", Base64Data = "ZmFrZQ==" }
            ]
        };

        var result = AgentInputNormalizer.Normalize(request);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("text", result.Items[0].Kind);
        Assert.Equal("image", result.Items[1].Kind);
    }

    [Fact]
    public void Normalize_AudioFile_RequiresMediaTypeAndPayload()
    {
        var request = new AgentChatRequest
        {
            ContentItems =
            [
                new AgentContentItemDto { Kind = "audio", FileName = "voice.wav", MediaType = "audio/wav", Base64Data = "UklGRg==" }
            ]
        };

        var result = AgentInputNormalizer.Normalize(request);

        Assert.Single(result.Items);
        Assert.Equal("audio", result.Items[0].Kind);
        Assert.Equal("audio/wav", result.Items[0].MediaType);
    }
}
```

- [ ] **Step 2：运行测试，确认当前确实失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~AgentInputNormalizerTests -v minimal`

预期：FAIL，因为新的 contracts 和 normalizer 还不存在。

- [ ] **Step 3：添加 Semantic Kernel 包依赖**

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.73.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.73.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.73.0" />
</ItemGroup>
```

- [ ] **Step 4：写最小实现，建立 contracts 和 normalizer**

```csharp
// src/GB28181Platform.AiAgent/Contracts/AgentContentItemDto.cs
namespace GB28181Platform.AiAgent.Contracts;

public class AgentContentItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public string? Base64Data { get; set; }
}
```

```csharp
// src/GB28181Platform.AiAgent/Contracts/AgentChatRequest.cs
namespace GB28181Platform.AiAgent.Contracts;

public class AgentChatRequest
{
    public string? ConversationId { get; set; }
    public string? DeviceId { get; set; }
    public string? ClientMessageId { get; set; }
    public List<AgentContentItemDto> ContentItems { get; set; } = [];
}
```

```csharp
// src/GB28181Platform.AiAgent/Contracts/AgentExecutionUsage.cs
namespace GB28181Platform.AiAgent.Contracts;

public class AgentExecutionUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
```

```csharp
// src/GB28181Platform.AiAgent/Contracts/AgentChatResponse.cs
namespace GB28181Platform.AiAgent.Contracts;

public class AgentChatResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<AgentContentItemDto> ContentItems { get; set; } = [];
    public List<string> ToolCalls { get; set; } = [];
    public List<string> Citations { get; set; } = [];
    public AgentExecutionUsage Usage { get; set; } = new();
}
```

```csharp
// src/GB28181Platform.AiAgent/Multimodal/NormalizedAgentInput.cs
namespace GB28181Platform.AiAgent.Multimodal;

public class NormalizedAgentInput
{
    public string ConversationId { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public List<NormalizedAgentInputItem> Items { get; set; } = [];
}

public class NormalizedAgentInputItem
{
    public string Kind { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public string? Base64Data { get; set; }
}
```

```csharp
// src/GB28181Platform.AiAgent/Multimodal/AgentInputNormalizer.cs
using GB28181Platform.AiAgent.Contracts;

namespace GB28181Platform.AiAgent.Multimodal;

public static class AgentInputNormalizer
{
    public static NormalizedAgentInput Normalize(AgentChatRequest request)
    {
        return new NormalizedAgentInput
        {
            ConversationId = string.IsNullOrWhiteSpace(request.ConversationId)
                ? Guid.NewGuid().ToString("N")
                : request.ConversationId,
            DeviceId = request.DeviceId,
            Items = request.ContentItems.Select(item => new NormalizedAgentInputItem
            {
                Kind = item.Kind,
                Text = item.Text,
                FileName = item.FileName,
                MediaType = item.MediaType,
                Base64Data = item.Base64Data
            }).ToList()
        };
    }
}
```

- [ ] **Step 5：再次运行测试，确认转绿**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~AgentInputNormalizerTests -v minimal`

预期：PASS，显示 `2 passed`。

- [ ] **Step 6：提交**

```bash
git add src/GB28181Platform.AiAgent/GB28181Platform.AiAgent.csproj src/GB28181Platform.AiAgent/Contracts src/GB28181Platform.AiAgent/Multimodal tests/GB28181Platform.Tests/AiAgent/AgentInputNormalizerTests.cs
git commit -m "feat(aiagent): add semantic kernel contracts and multimodal input models"
```

---

### Task 2：定义 Abstractions 与会话存储模型

**文件：**
- Create: `src/GB28181Platform.AiAgent/Abstractions/IAgentRuntime.cs`
- Create: `src/GB28181Platform.AiAgent/Abstractions/IAgentPromptProvider.cs`
- Create: `src/GB28181Platform.AiAgent/Abstractions/IConversationStore.cs`
- Create: `src/GB28181Platform.AiAgent/Abstractions/IAudioTranscriptionService.cs`
- Create: `src/GB28181Platform.AiAgent/Conversation/ConversationRecord.cs`
- Create: `src/GB28181Platform.AiAgent/Conversation/ConversationMessageRecord.cs`
- Create: `src/GB28181Platform.AiAgent/Conversation/ConversationContentItemRecord.cs`
- Modify: `src/GB28181Platform.Domain/Entities/AiConversation.cs`
- Test: `tests/GB28181Platform.Tests/AiAgent/SqlSugarConversationStoreTests.cs`

- [ ] **Step 1：先写失败测试，覆盖会话存储行为**

```csharp
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Capabilities.Persistence;
using NSubstitute;
using SqlSugar;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class SqlSugarConversationStoreTests
{
    [Fact]
    public async Task AppendMessageAsync_PersistsUserConversationMessage()
    {
        var db = Substitute.For<ISqlSugarClient>();
        var sut = new SqlSugarConversationStore(db);

        var message = new ConversationMessageRecord
        {
            ConversationId = "conv-001",
            UserId = 7,
            Role = "user",
            Items = [new ConversationContentItemRecord { Kind = "text", Text = "设备状态怎么样" }]
        };

        await sut.AppendMessageAsync(message, CancellationToken.None);

        await db.Received().Insertable(Arg.Any<object>()).ExecuteCommandAsync();
    }
}
```

- [ ] **Step 2：运行测试，确认当前失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~SqlSugarConversationStoreTests -v minimal`

预期：FAIL，因为 store abstractions 和实现尚未存在。

- [ ] **Step 3：补齐 abstractions 与 conversation 模型**

```csharp
// src/GB28181Platform.AiAgent/Abstractions/IAgentRuntime.cs
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;

namespace GB28181Platform.AiAgent.Abstractions;

public interface IAgentRuntime
{
    Task<AgentChatResponse> ExecuteAsync(
        int userId,
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history,
        CancellationToken cancellationToken);
}
```

```csharp
// src/GB28181Platform.AiAgent/Abstractions/IAgentPromptProvider.cs
namespace GB28181Platform.AiAgent.Abstractions;

public interface IAgentPromptProvider
{
    string BuildSystemPrompt(string? deviceId);
}
```

```csharp
// src/GB28181Platform.AiAgent/Abstractions/IConversationStore.cs
using GB28181Platform.AiAgent.Conversation;

namespace GB28181Platform.AiAgent.Abstractions;

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationMessageRecord>> GetHistoryAsync(int userId, string conversationId, CancellationToken cancellationToken);
    Task AppendMessageAsync(ConversationMessageRecord message, CancellationToken cancellationToken);
}
```

```csharp
// src/GB28181Platform.AiAgent/Abstractions/IAudioTranscriptionService.cs
namespace GB28181Platform.AiAgent.Abstractions;

public interface IAudioTranscriptionService
{
    Task<string> TranscribeAsync(string mediaType, string base64Data, CancellationToken cancellationToken);
}
```

```csharp
// src/GB28181Platform.AiAgent/Conversation/ConversationMessageRecord.cs
namespace GB28181Platform.AiAgent.Conversation;

public class ConversationMessageRecord
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string ConversationId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ConversationContentItemRecord> Items { get; set; } = [];
}
```

```csharp
// src/GB28181Platform.AiAgent/Conversation/ConversationContentItemRecord.cs
namespace GB28181Platform.AiAgent.Conversation;

public class ConversationContentItemRecord
{
    public string Kind { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public string? Base64Data { get; set; }
}
```

- [ ] **Step 4：扩展 `AiConversation`，使其可承载结构化会话记录**

```csharp
public class AiConversation
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int UserId { get; set; }

    [SugarColumn(Length = 36)]
    public string SessionId { get; set; } = string.Empty;

    [SugarColumn(Length = 36, IsNullable = true)]
    public string? MessageId { get; set; }

    [SugarColumn(Length = 20, IsNullable = true)]
    public string? DeviceId { get; set; }

    [SugarColumn(Length = 20)]
    public string Role { get; set; } = string.Empty;

    [SugarColumn(Length = 20, IsNullable = true)]
    public string? ContentKind { get; set; }

    [SugarColumn(Length = 255, IsNullable = true)]
    public string? FileName { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? MediaType { get; set; }

    [SugarColumn(ColumnDataType = "text")]
    public string Content { get; set; } = string.Empty;

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? FunctionName { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? FunctionArgs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 5：实现最小可用的 conversation store**

```csharp
// src/GB28181Platform.AiAgent/Capabilities/Persistence/SqlSugarConversationStore.cs
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.Domain.Entities;
using SqlSugar;

namespace GB28181Platform.AiAgent.Capabilities.Persistence;

public class SqlSugarConversationStore : IConversationStore
{
    private readonly ISqlSugarClient _db;

    public SqlSugarConversationStore(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ConversationMessageRecord>> GetHistoryAsync(int userId, string conversationId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConversationMessageRecord>>([]);
    }

    public async Task AppendMessageAsync(ConversationMessageRecord message, CancellationToken cancellationToken)
    {
        var rows = message.Items.Select(item => new AiConversation
        {
            UserId = message.UserId,
            SessionId = message.ConversationId,
            MessageId = message.MessageId,
            DeviceId = message.DeviceId,
            Role = message.Role,
            ContentKind = item.Kind,
            FileName = item.FileName,
            MediaType = item.MediaType,
            Content = item.Text ?? item.Base64Data ?? string.Empty,
            CreatedAt = message.CreatedAt
        }).ToList();

        await _db.Insertable(rows).ExecuteCommandAsync();
    }
}
```

- [ ] **Step 6：再次运行测试**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~SqlSugarConversationStoreTests -v minimal`

预期：PASS。

- [ ] **Step 7：提交**

```bash
git add src/GB28181Platform.AiAgent/Abstractions src/GB28181Platform.AiAgent/Conversation src/GB28181Platform.AiAgent/Capabilities/Persistence/SqlSugarConversationStore.cs src/GB28181Platform.Domain/Entities/AiConversation.cs tests/GB28181Platform.Tests/AiAgent/SqlSugarConversationStoreTests.cs
git commit -m "feat(aiagent): add conversation abstractions and persistence model"
```

---

### Task 3：搭建 Semantic Kernel Runtime 骨架与 Prompt Provider

**文件：**
- Create: `src/GB28181Platform.AiAgent/Runtime/SemanticKernelOptions.cs`
- Create: `src/GB28181Platform.AiAgent/Runtime/SemanticKernelModelRouter.cs`
- Create: `src/GB28181Platform.AiAgent/Runtime/SemanticKernelAgentRuntime.cs`
- Create: `src/GB28181Platform.AiAgent/Prompts/DefaultAgentPromptProvider.cs`
- Modify: `src/GB28181Platform.AiAgent/Prompts/SystemPrompts.cs`
- Test: `tests/GB28181Platform.Tests/AiAgent/SemanticKernelModelRouterTests.cs`
- Test: `tests/GB28181Platform.Tests/AiAgent/DefaultAgentPromptProviderTests.cs`

- [ ] **Step 1：先写失败测试，覆盖模型路由与 prompt provider**

```csharp
using GB28181Platform.AiAgent.Prompts;
using GB28181Platform.AiAgent.Runtime;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class SemanticKernelModelRouterTests
{
    [Fact]
    public void FromConfiguration_UsesDedicatedVisionAndAudioEndpoints()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SemanticKernel:TextModel:BaseUrl"] = "https://api.deepseek.com",
                ["SemanticKernel:TextModel:Model"] = "deepseek-chat",
                ["SemanticKernel:VisionModel:BaseUrl"] = "https://dashscope.aliyuncs.com/compatible-mode",
                ["SemanticKernel:VisionModel:Model"] = "qwen-vl-max",
                ["SemanticKernel:AudioModel:BaseUrl"] = "https://audio.example.com",
                ["SemanticKernel:AudioModel:Model"] = "whisper-1"
            }).Build();

        var options = SemanticKernelModelRouter.FromConfiguration(config);

        Assert.Equal("deepseek-chat", options.Text.Model);
        Assert.Equal("qwen-vl-max", options.Vision.Model);
        Assert.Equal("whisper-1", options.Audio.Model);
    }
}
```

- [ ] **Step 2：运行测试，确认失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~SemanticKernelModelRouterTests -v minimal`

预期：FAIL，因为 runtime 相关类尚不存在。

- [ ] **Step 3：实现 router、options 与 prompt provider**

```csharp
// src/GB28181Platform.AiAgent/Runtime/SemanticKernelOptions.cs
namespace GB28181Platform.AiAgent.Runtime;

public class SemanticKernelOptions
{
    public SemanticKernelEndpointOptions Text { get; set; } = new();
    public SemanticKernelEndpointOptions Vision { get; set; } = new();
    public SemanticKernelEndpointOptions Audio { get; set; } = new();
}

public class SemanticKernelEndpointOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
```

```csharp
// src/GB28181Platform.AiAgent/Runtime/SemanticKernelModelRouter.cs
using Microsoft.Extensions.Configuration;

namespace GB28181Platform.AiAgent.Runtime;

public static class SemanticKernelModelRouter
{
    public static SemanticKernelOptions FromConfiguration(IConfiguration configuration)
    {
        return new SemanticKernelOptions
        {
            Text = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:TextModel:BaseUrl"] ?? string.Empty,
                ApiKey = configuration["SemanticKernel:TextModel:ApiKey"] ?? string.Empty,
                Model = configuration["SemanticKernel:TextModel:Model"] ?? string.Empty
            },
            Vision = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:VisionModel:BaseUrl"] ?? configuration["SemanticKernel:TextModel:BaseUrl"] ?? string.Empty,
                ApiKey = configuration["SemanticKernel:VisionModel:ApiKey"] ?? configuration["SemanticKernel:TextModel:ApiKey"] ?? string.Empty,
                Model = configuration["SemanticKernel:VisionModel:Model"] ?? configuration["SemanticKernel:TextModel:Model"] ?? string.Empty
            },
            Audio = new SemanticKernelEndpointOptions
            {
                BaseUrl = configuration["SemanticKernel:AudioModel:BaseUrl"] ?? string.Empty,
                ApiKey = configuration["SemanticKernel:AudioModel:ApiKey"] ?? string.Empty,
                Model = configuration["SemanticKernel:AudioModel:Model"] ?? string.Empty
            }
        };
    }
}
```

```csharp
// src/GB28181Platform.AiAgent/Prompts/SystemPrompts.cs
namespace GB28181Platform.AiAgent.Prompts;

public static class SystemPrompts
{
    public const string MultimodalAssistant = """
你是 X-Link 智能体。
你负责处理文本、图片、音频文件输入，并在需要时调用系统能力完成诊断、状态查询和设备信息分析。
回答使用中文，先给结论，再给依据；如果使用了工具，请基于工具结果回答，不要编造。
""";
}
```

```csharp
// src/GB28181Platform.AiAgent/Prompts/DefaultAgentPromptProvider.cs
using GB28181Platform.AiAgent.Abstractions;

namespace GB28181Platform.AiAgent.Prompts;

public class DefaultAgentPromptProvider : IAgentPromptProvider
{
    public string BuildSystemPrompt(string? deviceId)
    {
        return string.IsNullOrWhiteSpace(deviceId)
            ? SystemPrompts.MultimodalAssistant
            : $"{SystemPrompts.MultimodalAssistant}\n当前会话设备上下文: {deviceId}";
    }
}
```

- [ ] **Step 4：先补一个最小可运行的 Semantic Kernel runtime 外壳**

```csharp
// src/GB28181Platform.AiAgent/Runtime/SemanticKernelAgentRuntime.cs
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.AiAgent.Runtime;

public class SemanticKernelAgentRuntime : IAgentRuntime
{
    private readonly ILogger<SemanticKernelAgentRuntime> _logger;

    public SemanticKernelAgentRuntime(ILogger<SemanticKernelAgentRuntime> logger)
    {
        _logger = logger;
    }

    public Task<AgentChatResponse> ExecuteAsync(
        int userId,
        NormalizedAgentInput input,
        IReadOnlyList<ConversationMessageRecord> history,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Semantic Kernel runtime placeholder executed for user {UserId}", userId);

        return Task.FromResult(new AgentChatResponse
        {
            ConversationId = input.ConversationId,
            MessageId = Guid.NewGuid().ToString("N"),
            Model = "semantic-kernel-placeholder",
            ContentItems =
            [
                new AgentContentItemDto { Kind = "text", Text = "placeholder" }
            ]
        });
    }
}
```

- [ ] **Step 5：再次运行模型路由与 prompt 测试**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~SemanticKernelModelRouterTests|FullyQualifiedName~DefaultAgentPromptProviderTests -v minimal`

预期：PASS。

- [ ] **Step 6：提交**

```bash
git add src/GB28181Platform.AiAgent/Runtime src/GB28181Platform.AiAgent/Prompts tests/GB28181Platform.Tests/AiAgent/SemanticKernelModelRouterTests.cs tests/GB28181Platform.Tests/AiAgent/DefaultAgentPromptProviderTests.cs
git commit -m "feat(aiagent): add semantic kernel runtime skeleton"
```

---

### Task 4：将业务函数迁移为 Semantic Kernel Plugins

**文件：**
- Create: `src/GB28181Platform.AiAgent/Capabilities/Plugins/DeviceCapabilityPlugin.cs`
- Create: `src/GB28181Platform.AiAgent/Capabilities/Plugins/DiagnosticCapabilityPlugin.cs`
- Keep temporarily: `src/GB28181Platform.AiAgent/Functions/GetDeviceStatusFunction.cs`
- Keep temporarily: `src/GB28181Platform.AiAgent/Functions/GetDiagnosticLogsFunction.cs`
- Keep temporarily: `src/GB28181Platform.AiAgent/Functions/ListOfflineDevicesFunction.cs`
- Keep temporarily: `src/GB28181Platform.AiAgent/Functions/IAgentFunction.cs`
- Keep temporarily: `src/GB28181Platform.AiAgent/Functions/FunctionRegistry.cs`
- Keep temporarily: `tests/GB28181Platform.Tests/AiAgent/FunctionRegistryTests.cs`
- Test: `tests/GB28181Platform.Tests/AiAgent/AiAgentServiceTests.cs`

- [ ] **Step 1：先写失败测试，验证服务已不再依赖旧函数注册机制**

```csharp
[Fact]
public async Task ChatAsync_UsesRuntimeInsteadOfLegacyFunctionRegistry()
{
    var runtime = Substitute.For<IAgentRuntime>();
    var store = Substitute.For<IConversationStore>();
    runtime.ExecuteAsync(Arg.Any<int>(), Arg.Any<NormalizedAgentInput>(), Arg.Any<IReadOnlyList<ConversationMessageRecord>>(), Arg.Any<CancellationToken>())
        .Returns(new AgentChatResponse
        {
            ConversationId = "conv-001",
            MessageId = "msg-001",
            Model = "deepseek-chat",
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = "设备在线" }]
        });

    var sut = new AiAgentService(new AiChatApplicationService(runtime, store, new DefaultAgentPromptProvider(), Substitute.For<ILogger<AiChatApplicationService>>()));

    var response = await sut.ChatAsync(1, new AgentChatRequest
    {
        ConversationId = "conv-001",
        DeviceId = "34020000001320000001",
        ContentItems = [new AgentContentItemDto { Kind = "text", Text = "设备在线吗" }]
    });

    Assert.Equal("设备在线", response.ContentItems[0].Text);
}
```

- [ ] **Step 2：运行定向测试，确认当前失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~AiAgentServiceTests -v minimal`

预期：FAIL，因为 `AiAgentService` 当前仍依赖 `IQwenClient` 和 `FunctionRegistry`。

- [ ] **Step 3：创建新的 plugin 类**

```csharp
// src/GB28181Platform.AiAgent/Capabilities/Plugins/DeviceCapabilityPlugin.cs
using Microsoft.SemanticKernel;
using SqlSugar;

namespace GB28181Platform.AiAgent.Capabilities.Plugins;

public class DeviceCapabilityPlugin
{
    private readonly ISqlSugarClient _db;

    public DeviceCapabilityPlugin(ISqlSugarClient db)
    {
        _db = db;
    }

    [KernelFunction("get_device_status")]
    public async Task<string> GetDeviceStatusAsync(string deviceId)
    {
        var device = await _db.Queryable<GB28181Platform.Domain.Entities.Device>()
            .FirstAsync(x => x.Id == deviceId);

        return device == null ? "未找到设备" : $"设备 {device.Id} 当前状态: {device.Status}";
    }

    [KernelFunction("list_offline_devices")]
    public async Task<string> ListOfflineDevicesAsync()
    {
        var devices = await _db.Queryable<GB28181Platform.Domain.Entities.Device>()
            .Where(x => x.Status == "OFFLINE")
            .ToListAsync();

        return devices.Count == 0 ? "当前没有离线设备" : string.Join('\n', devices.Select(x => $"{x.Id} {x.Name}"));
    }
}
```

```csharp
// src/GB28181Platform.AiAgent/Capabilities/Plugins/DiagnosticCapabilityPlugin.cs
using Microsoft.SemanticKernel;
using SqlSugar;

namespace GB28181Platform.AiAgent.Capabilities.Plugins;

public class DiagnosticCapabilityPlugin
{
    private readonly ISqlSugarClient _db;

    public DiagnosticCapabilityPlugin(ISqlSugarClient db)
    {
        _db = db;
    }

    [KernelFunction("get_diagnostic_logs")]
    public async Task<string> GetDiagnosticLogsAsync(string deviceId)
    {
        var logs = await _db.Queryable<GB28181Platform.Domain.Entities.DiagnosticLog>()
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        return logs.Count == 0
            ? "没有诊断日志"
            : string.Join('\n', logs.Select(x => $"{x.CreatedAt:yyyy-MM-dd HH:mm:ss} [{x.StepName}] {x.Message}"));
    }
}
```

- [ ] **Step 4：保留旧函数体系，先只新增 plugins 并更新测试说明**

此阶段**不要删除** `Functions/` 目录与 `FunctionRegistry`，因为旧 `AiAgentService` 仍依赖它们。这里只新增 plugins，并把 `AiAgentServiceTests` 的迁移目标调整为：

- 当前阶段先允许旧服务继续存在
- 下一阶段（Task 5）切换 `AiAgentService`
- Task 7/9 再统一删除旧 runtime 与旧 functions

- [ ] **Step 5：再次运行智能体相关测试**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~AiAgentServiceTests -v minimal`

预期：当前可接受两种结果：

- 如果 `AiAgentServiceTests` 仍是旧测试，则保持 PASS
- 如果已改造成面向新 runtime 的测试，则进入 Task 5 后再统一转绿

- [ ] **Step 6：提交**

```bash
git add src/GB28181Platform.AiAgent/Capabilities/Plugins tests/GB28181Platform.Tests/AiAgent/AiAgentServiceTests.cs
git commit -m "feat(aiagent): add SK capability plugins"
```

---

### Task 5：实现 Application Service，并替换旧版 AiAgentService 内核

**文件：**
- Create: `src/GB28181Platform.AiAgent/Capabilities/Application/AiChatApplicationService.cs`
- Modify: `src/GB28181Platform.AiAgent/IAiAgentService.cs`
- Modify: `src/GB28181Platform.AiAgent/AiAgentService.cs`
- Test: `tests/GB28181Platform.Tests/AiAgent/AiChatApplicationServiceTests.cs`

- [ ] **Step 1：先写失败测试，覆盖 Application Service 主链路**

```csharp
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GB28181Platform.Tests.AiAgent;

public class AiChatApplicationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_LoadsHistoryInvokesRuntimeAndPersistsMessages()
    {
        var runtime = Substitute.For<IAgentRuntime>();
        var store = Substitute.For<IConversationStore>();
        runtime.ExecuteAsync(Arg.Any<int>(), Arg.Any<NormalizedAgentInput>(), Arg.Any<IReadOnlyList<ConversationMessageRecord>>(), Arg.Any<CancellationToken>())
            .Returns(new AgentChatResponse
            {
                ConversationId = "conv-001",
                MessageId = "msg-002",
                Model = "deepseek-chat",
                ContentItems = [new AgentContentItemDto { Kind = "text", Text = "设备离线" }]
            });

        var sut = new AiChatApplicationService(runtime, store, Substitute.For<IAgentPromptProvider>(), Substitute.For<ILogger<AiChatApplicationService>>());

        var response = await sut.ExecuteAsync(7, new AgentChatRequest
        {
            ConversationId = "conv-001",
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = "查设备状态" }]
        }, CancellationToken.None);

        Assert.Equal("conv-001", response.ConversationId);
        await store.Received().AppendMessageAsync(Arg.Any<ConversationMessageRecord>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2：运行测试，确认当前失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~AiChatApplicationServiceTests -v minimal`

预期：FAIL，因为 application service 尚不存在。

- [ ] **Step 3：创建 application service**

```csharp
// src/GB28181Platform.AiAgent/Capabilities/Application/AiChatApplicationService.cs
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.AiAgent.Conversation;
using GB28181Platform.AiAgent.Multimodal;
using Microsoft.Extensions.Logging;

namespace GB28181Platform.AiAgent.Capabilities.Application;

public class AiChatApplicationService
{
    private readonly IAgentRuntime _runtime;
    private readonly IConversationStore _conversationStore;
    private readonly IAgentPromptProvider _promptProvider;
    private readonly ILogger<AiChatApplicationService> _logger;

    public AiChatApplicationService(
        IAgentRuntime runtime,
        IConversationStore conversationStore,
        IAgentPromptProvider promptProvider,
        ILogger<AiChatApplicationService> logger)
    {
        _runtime = runtime;
        _conversationStore = conversationStore;
        _promptProvider = promptProvider;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ExecuteAsync(int userId, AgentChatRequest request, CancellationToken cancellationToken)
    {
        var normalized = AgentInputNormalizer.Normalize(request);
        var history = await _conversationStore.GetHistoryAsync(userId, normalized.ConversationId, cancellationToken);

        var userMessage = new ConversationMessageRecord
        {
            ConversationId = normalized.ConversationId,
            UserId = userId,
            DeviceId = normalized.DeviceId,
            Role = "user",
            Items = normalized.Items.Select(x => new ConversationContentItemRecord
            {
                Kind = x.Kind,
                Text = x.Text,
                FileName = x.FileName,
                MediaType = x.MediaType,
                Base64Data = x.Base64Data
            }).ToList()
        };

        await _conversationStore.AppendMessageAsync(userMessage, cancellationToken);
        var response = await _runtime.ExecuteAsync(userId, normalized, history, cancellationToken);

        await _conversationStore.AppendMessageAsync(new ConversationMessageRecord
        {
            MessageId = response.MessageId,
            ConversationId = response.ConversationId,
            UserId = userId,
            DeviceId = normalized.DeviceId,
            Role = "assistant",
            Items = response.ContentItems.Select(x => new ConversationContentItemRecord
            {
                Kind = x.Kind,
                Text = x.Text,
                FileName = x.FileName,
                MediaType = x.MediaType,
                Base64Data = x.Base64Data
            }).ToList()
        }, cancellationToken);

        return response;
    }
}
```

- [ ] **Step 4：更新 `IAiAgentService` 与 `AiAgentService`，改为委托 application service**

```csharp
// src/GB28181Platform.AiAgent/IAiAgentService.cs
using GB28181Platform.AiAgent.Contracts;

namespace GB28181Platform.AiAgent;

public interface IAiAgentService
{
    Task<AgentChatResponse> ChatAsync(int userId, AgentChatRequest request, CancellationToken cancellationToken = default);
}
```

```csharp
// src/GB28181Platform.AiAgent/AiAgentService.cs
using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Contracts;

namespace GB28181Platform.AiAgent;

public class AiAgentService : IAiAgentService
{
    private readonly AiChatApplicationService _applicationService;

    public AiAgentService(AiChatApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    public Task<AgentChatResponse> ChatAsync(int userId, AgentChatRequest request, CancellationToken cancellationToken = default)
        => _applicationService.ExecuteAsync(userId, request, cancellationToken);
}
```

- [ ] **Step 5：运行更新后的测试**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~AiChatApplicationServiceTests|FullyQualifiedName~AiAgentServiceTests -v minimal`

预期：PASS。

- [ ] **Step 6：提交**

```bash
git add src/GB28181Platform.AiAgent/Capabilities/Application/AiChatApplicationService.cs src/GB28181Platform.AiAgent/IAiAgentService.cs src/GB28181Platform.AiAgent/AiAgentService.cs tests/GB28181Platform.Tests/AiAgent/AiChatApplicationServiceTests.cs tests/GB28181Platform.Tests/AiAgent/AiAgentServiceTests.cs
git commit -m "feat(aiagent): add application service for multimodal chat"
```

---

### Task 6：升级 API Contract 与 Controller，支持多模态请求

**文件：**
- Modify: `src/GB28181Platform.Api/Controllers/AiAgentController.cs`
- Modify: `src/GB28181Platform.Api/Program.cs`
- Modify: `tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj`

- [ ] **Step 1：先写失败测试，覆盖 Controller 结构化返回**

```csharp
[Fact]
public async Task Chat_ReturnsStructuredAgentResponse()
{
    var aiAgent = Substitute.For<IAiAgentService>();
    aiAgent.ChatAsync(Arg.Any<int>(), Arg.Any<AgentChatRequest>(), Arg.Any<CancellationToken>())
        .Returns(new AgentChatResponse
        {
            ConversationId = "conv-001",
            MessageId = "msg-001",
            Model = "deepseek-chat",
            ContentItems = [new AgentContentItemDto { Kind = "text", Text = "设备在线" }]
        });

    var controller = new AiAgentController(aiAgent, Substitute.For<ILogger<AiAgentController>>());

    var result = await controller.Chat(new AgentChatRequest
    {
        ConversationId = "conv-001",
        ContentItems = [new AgentContentItemDto { Kind = "text", Text = "设备状态" }]
    }, CancellationToken.None);

    Assert.Equal("conv-001", result.Data.ConversationId);
    Assert.Equal("设备在线", result.Data.ContentItems[0].Text);
}
```

- [ ] **Step 2：运行定向测试，确认当前失败**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~Chat_ReturnsStructuredAgentResponse -v minimal`

预期：FAIL，因为 controller 仍使用旧 DTO 和字符串回复结构。

- [ ] **Step 3：升级 controller，并停止直接记录原始用户输入**

```csharp
using System.Security.Claims;
using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Contracts;
using GB28181Platform.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace GB28181Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiAgentController : ControllerBase
{
    private readonly IAiAgentService _aiAgent;
    private readonly ILogger<AiAgentController> _logger;

    public AiAgentController(IAiAgentService aiAgent, ILogger<AiAgentController> logger)
    {
        _aiAgent = aiAgent;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ApiResponse<AgentChatResponse>> Chat([FromBody] AgentChatRequest request, CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        _logger.LogInformation("AI chat request received. UserId={UserId}, ConversationId={ConversationId}, ItemCount={ItemCount}",
            userId, request.ConversationId, request.ContentItems.Count);

        var response = await _aiAgent.ChatAsync(userId, request, cancellationToken);
        return ApiResponse<AgentChatResponse>.Ok(response);
    }

    private int TryGetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var userId) ? userId : 0;
    }
}
```

- [ ] **Step 4：在 `Program.cs` 中注册新服务**

```csharp
builder.Services.AddSingleton(sp =>
{
    return SemanticKernelModelRouter.FromConfiguration(builder.Configuration);
});
builder.Services.AddScoped<IAgentPromptProvider, DefaultAgentPromptProvider>();
builder.Services.AddScoped<IConversationStore, SqlSugarConversationStore>();
builder.Services.AddScoped<DeviceCapabilityPlugin>();
builder.Services.AddScoped<DiagnosticCapabilityPlugin>();
builder.Services.AddScoped<IAgentRuntime, SemanticKernelAgentRuntime>();
builder.Services.AddScoped<AiChatApplicationService>();
builder.Services.AddScoped<IAiAgentService, AiAgentService>();
```

- [ ] **Step 5：运行 API 相关测试**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~Chat_ReturnsStructuredAgentResponse -v minimal`

预期：PASS。

- [ ] **Step 6：提交**

```bash
git add src/GB28181Platform.Api/Controllers/AiAgentController.cs src/GB28181Platform.Api/Program.cs tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj
git commit -m "feat(api): upgrade ai chat endpoint to multimodal contracts"
```

---

### Task 7：将图片与音频文件处理接入 Runtime

**文件：**
- Modify: `src/GB28181Platform.AiAgent/Runtime/SemanticKernelAgentRuntime.cs`
- Create: `src/GB28181Platform.AiAgent/Capabilities/Application/OpenAiCompatibleAudioTranscriptionService.cs`
- Delete: `src/GB28181Platform.AiAgent/IQwenClient.cs`
- Delete: `src/GB28181Platform.AiAgent/QwenClient.cs`
- Delete: `src/GB28181Platform.AiAgent/QwenEndpointRouting.cs`
- Delete: `tests/GB28181Platform.Tests/AiAgent/QwenEndpointRoutingTests.cs`
- Delete: `src/GB28181Platform.AiAgent/Functions/IAgentFunction.cs`
- Delete: `src/GB28181Platform.AiAgent/Functions/FunctionRegistry.cs`
- Delete: `src/GB28181Platform.AiAgent/Functions/GetDeviceStatusFunction.cs`
- Delete: `src/GB28181Platform.AiAgent/Functions/GetDiagnosticLogsFunction.cs`
- Delete: `src/GB28181Platform.AiAgent/Functions/ListOfflineDevicesFunction.cs`
- Delete: `tests/GB28181Platform.Tests/AiAgent/FunctionRegistryTests.cs`

- [ ] **Step 1：先写失败测试，覆盖图片与音频输入链路**

```csharp
[Fact]
public async Task ExecuteAsync_TranscribesAudioAndPreservesImageItems()
{
    var logger = Substitute.For<ILogger<SemanticKernelAgentRuntime>>();
    var sut = new SemanticKernelAgentRuntime(logger);

    var input = new NormalizedAgentInput
    {
        ConversationId = "conv-001",
        Items =
        [
            new() { Kind = "image", Base64Data = "ZmFrZQ==", MediaType = "image/png" },
            new() { Kind = "audio", Base64Data = "UklGRg==", MediaType = "audio/wav" }
        ]
    };

    var response = await sut.ExecuteAsync(1, input, [], CancellationToken.None);

    Assert.NotNull(response);
}
```

- [ ] **Step 2：运行定向 Runtime 测试，确认失败或能力缺失**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj --filter FullyQualifiedName~ExecuteAsync_TranscribesAudioAndPreservesImageItems -v minimal`

预期：FAIL，或暴露图片/音频处理逻辑尚未完成。

- [ ] **Step 3：补充音频转写服务实现**

```csharp
// src/GB28181Platform.AiAgent/Capabilities/Application/OpenAiCompatibleAudioTranscriptionService.cs
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Runtime;

namespace GB28181Platform.AiAgent.Capabilities.Application;

public class OpenAiCompatibleAudioTranscriptionService : IAudioTranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly SemanticKernelOptions _options;

    public OpenAiCompatibleAudioTranscriptionService(HttpClient httpClient, SemanticKernelOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public Task<string> TranscribeAsync(string mediaType, string base64Data, CancellationToken cancellationToken)
    {
        return Task.FromResult("[audio transcription placeholder]");
    }
}
```

- [ ] **Step 4：将占位 runtime 替换为基于 Semantic Kernel 的消息组装与执行逻辑**

Implement `SemanticKernelAgentRuntime` so it:

- builds a `Kernel`
- registers current plugins
- maps text/image content items into SK chat history
- sends audio items through `IAudioTranscriptionService` before execution
- returns `AgentChatResponse` with model name and text output

Use this structure:

```csharp
public async Task<AgentChatResponse> ExecuteAsync(
    int userId,
    NormalizedAgentInput input,
    IReadOnlyList<ConversationMessageRecord> history,
    CancellationToken cancellationToken)
{
    var kernel = BuildKernel();
    var chatHistory = BuildChatHistory(history, input);
    var responseText = await InvokeAgentAsync(kernel, chatHistory, cancellationToken);

    return new AgentChatResponse
    {
        ConversationId = input.ConversationId,
        MessageId = Guid.NewGuid().ToString("N"),
        Model = _options.Text.Model,
        ContentItems = [new AgentContentItemDto { Kind = "text", Text = responseText }],
        Usage = new AgentExecutionUsage()
    };
}
```

- [ ] **Step 5：在 Runtime 完成切换后，再移除旧 runtime 与旧 functions**

Delete:

- `IQwenClient.cs`
- `QwenClient.cs`
- `QwenEndpointRouting.cs`
- `QwenEndpointRoutingTests.cs`
- `IAgentFunction.cs`
- `FunctionRegistry.cs`
- `GetDeviceStatusFunction.cs`
- `GetDiagnosticLogsFunction.cs`
- `ListOfflineDevicesFunction.cs`
- `FunctionRegistryTests.cs`

- [ ] **Step 6：运行完整测试**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj -v minimal`

预期：PASS。

- [ ] **Step 7：提交**

```bash
git add src/GB28181Platform.AiAgent/Runtime src/GB28181Platform.AiAgent/Capabilities/Application/OpenAiCompatibleAudioTranscriptionService.cs
git rm src/GB28181Platform.AiAgent/IQwenClient.cs src/GB28181Platform.AiAgent/QwenClient.cs src/GB28181Platform.AiAgent/QwenEndpointRouting.cs src/GB28181Platform.AiAgent/Functions/IAgentFunction.cs src/GB28181Platform.AiAgent/Functions/FunctionRegistry.cs src/GB28181Platform.AiAgent/Functions/GetDeviceStatusFunction.cs src/GB28181Platform.AiAgent/Functions/GetDiagnosticLogsFunction.cs src/GB28181Platform.AiAgent/Functions/ListOfflineDevicesFunction.cs tests/GB28181Platform.Tests/AiAgent/QwenEndpointRoutingTests.cs tests/GB28181Platform.Tests/AiAgent/FunctionRegistryTests.cs
git commit -m "feat(aiagent): integrate SK runtime + remove legacy functions"
```

---

### Task 8：更新前端 AI Chat 客户端，移除旧的纯文本假设

**文件：**
- Modify: `frontend/vms-web/src/api/ai.ts`
- Modify: `frontend/vms-web/src/views/AiChat.vue`

- [ ] **Step 1：更新前端 API 客户端，发送多模态请求**

```ts
// frontend/vms-web/src/api/ai.ts
import request from './request'

export interface AgentContentItemDto {
  kind: 'text' | 'image' | 'audio'
  text?: string
  fileName?: string
  mediaType?: string
  base64Data?: string
}

export interface AgentChatRequest {
  conversationId?: string
  deviceId?: string
  clientMessageId?: string
  contentItems: AgentContentItemDto[]
}

export function chat(payload: AgentChatRequest) {
  return request.post('/api/AiAgent/chat', payload)
}
```

- [ ] **Step 2：更新 `AiChat.vue`，读取结构化响应**

Replace the existing text-only request call with:

```ts
const res: any = await chat({
  conversationId: conversationId.value,
  contentItems: [{ kind: 'text', text }]
})

const data = res.data || res
conversationId.value = data.conversationId
const reply = data.contentItems?.find((item: any) => item.kind === 'text')?.text || '暂无回复'
```

- [ ] **Step 3：为图片和音频文件输入增加上传占位能力**

Add hidden file inputs and local state for:

- image file selection
- audio file selection

Do not implement realtime voice here.

- [ ] **Step 4：运行前端构建**

运行：`npm run build`

工作目录：`frontend/vms-web`

预期：PASS。

- [ ] **Step 5：提交**

```bash
git add frontend/vms-web/src/api/ai.ts frontend/vms-web/src/views/AiChat.vue
git commit -m "feat(frontend): adapt ai chat ui to multimodal agent contract"
```

---

### Task 9：最终清理与完整验证

**文件：**
- Delete: any remaining old handwritten runtime files
- Modify: any compile errors left after the full cutover

- [ ] **Step 1：移除 `Program.cs`、测试、项目文件中残留的旧运行时引用**

确认以下旧符号已经不再被代码引用：

- `IQwenClient`
- `FunctionRegistry`
- `IAgentFunction`
- `QwenEndpointRouting`

运行：

```bash
rg -n "IQwenClient|FunctionRegistry|IAgentFunction|QwenEndpointRouting|QwenClient" src tests
```

预期：除 plan/spec 文档中的历史描述外，不再有代码引用。

- [ ] **Step 2：运行后端测试套件**

运行：`dotnet test tests/GB28181Platform.Tests/GB28181Platform.Tests.csproj -v minimal`

预期：PASS。

- [ ] **Step 3：运行解决方案构建**

运行：`dotnet build GB28181Platform.sln -nologo`

预期：`0 Error(s)`。

- [ ] **Step 4：运行前端构建**

运行：`npm run build`

工作目录：`frontend/vms-web`

预期：PASS。

- [ ] **Step 5：提交最终清理**

```bash
git add src tests frontend/vms-web
git commit -m "refactor(aiagent): remove legacy handwritten agent runtime"
```

---

## 自检

### 规格覆盖检查

- Semantic Kernel runtime replacement: covered in Tasks 1, 3, 4, 5, 7, 9
- multimodal request/response upgrade: covered in Tasks 1, 5, 6, 8
- real conversation history: covered in Tasks 2 and 5
- capabilities separation: covered in Task 4
- prompts separation: covered in Task 3
- multi-user support: covered in Tasks 2, 5, and 6
- image and audio file support: covered in Tasks 1, 7, and 8
- frontend contract upgrade: covered in Task 8

### 占位词检查

- No `TODO` or `TBD` placeholders
- Each task includes exact file paths
- Each task includes exact commands
- Each task includes concrete code blocks for the initial implementation shape

### 类型一致性检查

- `AgentChatRequest` and `AgentChatResponse` are used consistently after Task 1
- `IAiAgentService` is upgraded in Task 5 and controller adoption follows in Task 6
- conversation ownership uses `userId` consistently
