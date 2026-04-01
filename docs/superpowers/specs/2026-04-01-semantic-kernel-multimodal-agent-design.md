# Semantic Kernel Multimodal Agent Design

> Date: 2026-04-01
> Status: Draft Approved For Planning
> Workspace: `.worktrees/visible-field-browser-diagnostic`

---

## 1. Background

The current `GB28181Platform.AiAgent` implementation is a lightweight handwritten runtime around OpenAI-compatible chat completion:

- `AiAgentService` manually builds message arrays
- `QwenClient` manually calls `/v1/chat/completions`
- `FunctionRegistry` and `IAgentFunction` manually expose tools
- `AiConversation` is used mainly as an append-only log, not a real multi-turn conversation source
- image analysis is a separate vision path, not part of a unified agent runtime

This design is enough for text-first function calling, but it has structural limitations:

- text, image, and future audio input are not unified
- session handling is weaker than a real multi-turn conversation model
- response shape is too narrow for model metadata, tool traces, citations, and usage
- model/runtime concerns are mixed with business capability concerns
- the current structure is hard to transplant into another project later

The target is to replace the handwritten runtime with a unified Semantic Kernel based multimodal agent architecture while keeping implementation in the current project first and designing it so the reusable core can be extracted later.

---

## 2. Goals

### 2.1 Primary Goals

- Replace the handwritten `QwenClient + FunctionRegistry + AiAgentService` runtime with Semantic Kernel.
- Unify text, image, and audio-file input into one agent architecture.
- Support multiple users, multiple browsers, and multiple computers talking concurrently.
- Make conversation state a real multi-turn context source instead of write-only records.
- Upgrade the API contract to support multimodal input and richer output metadata.
- Separate reusable agent runtime concerns from project-specific business capability concerns.
- Keep the implementation in the current `GB28181Platform.AiAgent` project for now, while designing it for later extraction.

### 2.2 Secondary Goals

- Reduce prompt/runtime coupling.
- Allow future support for realtime voice without redesigning the whole system.
- Keep the HTTP entry route conceptually stable: one main chat endpoint, one main application entry.

### 2.3 Non-Goals

- First-phase realtime voice streaming.
- Splitting the current solution into multiple new projects immediately.
- Full backward compatibility with the current text-only request/response DTOs.
- Long-lived dual runtime support between old and new agent architectures.

---

## 3. Chosen Approach

The chosen approach is:

**Option 2: Unified multimodal Semantic Kernel platform, realtime voice deferred**

This means:

- full internal migration to Semantic Kernel
- request/response model upgrade
- real conversation model
- unified text/image/audio-file input
- runtime, prompts, conversations, and capabilities reorganized into clear sublayers
- realtime voice intentionally left as a future input channel built on the same foundation

This approach is preferred over a compatibility-only migration because the current request, session, and multimodal limitations are structural. It is preferred over an all-at-once realtime design because realtime voice adds a separate class of transport and latency problems that would make the migration significantly riskier.

---

## 4. High-Level Architecture

The `GB28181Platform.AiAgent` project remains a single project initially, but its internal structure is reorganized into focused sublayers:

```text
GB28181Platform.AiAgent/
├── Abstractions/
├── Contracts/
├── Conversation/
├── Multimodal/
├── Prompts/
├── Runtime/
└── Capabilities/
    ├── Application/
    ├── Plugins/
    └── Persistence/
```

### 4.1 Layer Responsibilities

#### `Abstractions/`

Reusable interfaces for the new architecture, such as:

- `IAgentRuntime`
- `IConversationStore`
- `IAgentPromptProvider`
- `IAudioTranscriptionService`
- `IModelRoutingStrategy`

These abstractions must avoid direct dependency on `SqlSugar`, API controllers, or GB28181-specific domain behavior.

#### `Contracts/`

External and internal contracts for multimodal chat:

- request DTOs
- response DTOs
- content item models
- usage/tool/citation metadata models
- message result models

This layer defines the shape of data exchanged across API, application, runtime, and persistence boundaries.

#### `Conversation/`

The conversation domain for agent interactions:

- conversation identity
- conversation message identity
- conversation content items
- sequencing and role semantics
- tool trace representation

This layer is responsible for representing real multi-turn state, not just individual request logs.

#### `Multimodal/`

Input normalization and content conversion:

- text content normalization
- image content normalization
- audio file normalization
- conversion to Semantic Kernel chat content/history structures

This layer is responsible for "how input types become agent-readable content".

#### `Prompts/`

System prompt and instruction composition:

- default system prompt
- multimodal-specific instruction additions
- tool-usage policy
- answer formatting policy
- citation and diagnostic explanation rules

This layer controls behavior, not capabilities.

#### `Runtime/`

Semantic Kernel orchestration:

- kernel creation
- chat completion service registration
- multimodal-capable routing
- audio-to-text integration
- plugin registration
- tool invocation policy
- execution pipeline

This layer controls "how the agent runs".

#### `Capabilities/`

Project-specific behavior for the current platform:

##### `Capabilities/Application/`

Application services that adapt current project behavior into the new agent runtime.

##### `Capabilities/Plugins/`

Business capabilities exposed to the agent, such as:

- get device status
- list offline devices
- get diagnostic logs
- future report export
- future device detail retrieval

##### `Capabilities/Persistence/`

Conversation and message persistence implementations for the current project's database/storage model.

This layer contains project behavior that can later be swapped when transplanting the agent system into another application.

---

## 5. Reusable Core vs. Project-Specific Code

### 5.1 Future-Extractable Core

These parts should be written as if they may later move into another library:

- `Abstractions`
- `Contracts`
- `Conversation`
- `Multimodal`
- most of `Runtime`
- prompt composition primitives

These should not directly depend on:

- `Device`
- `DiagnosticLog`
- `AiConversation` entity
- `SqlSugar`
- `GB28181Platform.Api`

### 5.2 Current-Project Adaptation Layer

These parts remain project-specific:

- capability plugins that query current business data
- prompt fragments that describe current platform behavior
- persistence adapters that store conversation/message data in the current database
- HTTP DTO mapping and upload handling specific to this project

The extraction strategy later is:

- keep the reusable engine
- replace `Capabilities`
- replace project-specific persistence and prompts

---

## 6. API Design

The current text-only model:

- `ChatRequest.Message`
- `ChatResponse.Reply`

is not sufficient for multimodal agent behavior. The new API should support a single chat endpoint with richer request and response models.

### 6.1 Request Shape

The new request should represent one user message containing one or more content items.

Conceptually:

- `conversationId`
- `deviceId` or optional business context
- `contentItems[]`
- optional client metadata

Each `contentItem` should support:

- `text`
- `image`
- `audio`

The first implementation must support:

- text only
- text + image
- text + audio file
- multiple items in one request

The request model should be designed so realtime voice can later be introduced as another input channel without redesigning the message model.

### 6.2 Response Shape

The response should no longer be only a single assistant string. It should contain:

- `conversationId`
- `messageId`
- assistant content items
- model info
- tool call traces
- citations
- usage metadata

This lets the UI evolve later without redesigning the service contract again.

### 6.3 Controller Strategy

The route can conceptually stay as one main chat endpoint under the existing AI controller, but the DTOs and returned structure will be upgraded.

The controller should:

- extract authenticated user context
- translate HTTP request to application request contract
- avoid logging raw user content in production-sensitive form
- return the richer multimodal response structure

---

## 7. Conversation Model

### 7.1 Current Problem

Current `AiConversation` records are mainly append-only rows written after a reply. They do not serve as the authoritative conversation context source before model execution.

### 7.2 Target Model

The new design treats conversation as a first-class concept:

- `Conversation`
- `ConversationMessage`
- `ConversationContentItem`

Each conversation belongs to a user.

Each conversation contains many ordered messages.

Each message contains one or more content items and optional execution metadata.

### 7.3 Multi-User Concurrency

The design must support:

- multiple users
- multiple computers
- multiple browsers
- concurrent chat requests

The persistence layer must therefore support:

- ownership by `UserId`
- ordering by message creation time and/or sequence
- safe concurrent appends
- retrieving history by `UserId + ConversationId`

This architecture supports concurrent chatting across users and devices. Cross-tab live synchronization is optional and can later be added via SignalR/WebSocket without changing the storage or runtime model.

### 7.4 Device Context

`deviceId` should remain optional business context associated with a conversation message or conversation scope, not the primary owner of the conversation.

Conversation ownership is by user.

Device context is attached so the agent can answer device-scoped questions and invoke capabilities with the right target context.

---

## 8. Multimodal Input Model

### 8.1 Text

Text content is added directly to the normalized message content set and transformed into Semantic Kernel chat history entries.

### 8.2 Image

Images are normalized into a common content item representation and then converted into multimodal chat content supported by Semantic Kernel.

The current separate handwritten vision path is removed as the primary mechanism. Vision becomes part of the unified multimodal runtime.

### 8.3 Audio File

The first-phase audio path uses:

1. audio file input
2. audio-to-text service
3. transcription text inserted into normalized message content
4. unified Semantic Kernel agent execution

This avoids prematurely coupling the first release to realtime voice transport and streaming concerns.

### 8.4 Future Realtime Voice

Realtime voice is not implemented in this phase, but the architecture reserves it as:

- another input transport
- same conversation model
- same normalized content pipeline
- same Semantic Kernel runtime

That means realtime is an additive channel later, not a redesign.

---

## 9. Prompt Strategy

Prompts must be separated from capabilities.

The prompt system should provide:

- one main system prompt
- optional scoped augmentations for multimodal behavior
- tool usage guidance
- answer style rules
- citation/reference behavior
- diagnostic explanation policy

The prompt policy must explicitly cover:

- how to answer with text, image, and audio-derived context
- when to use tools vs. direct reasoning
- how to respond when multimodal content is insufficient
- how to reference diagnostics and platform facts

Prompt assembly should be explicit and deterministic, not spread across controller, runtime, and plugins.

---

## 10. Semantic Kernel Runtime Design

### 10.1 Runtime Role

Semantic Kernel is the execution engine, not the business application layer.

The runtime layer should be responsible for:

- building and configuring the `Kernel`
- choosing model services
- loading plugins
- managing tool invocation behavior
- executing the chat-completion-based agent
- returning structured execution results

### 10.2 Chat Completion and Tool Calling

The current handwritten tool-calling loop is removed.

Semantic Kernel handles:

- plugin exposure
- tool selection
- tool invocation
- multimodal chat orchestration

The first design should use one primary chat-completion-based agent path.

### 10.3 Vision Support

Image inputs should be passed through Semantic Kernel's multimodal chat abstractions rather than through custom handwritten JSON request building.

### 10.4 Audio Support

Audio file support should use an audio-to-text abstraction integrated into the runtime flow before final chat completion invocation.

### 10.5 Model Routing

The runtime must support routing between:

- primary text-capable model endpoint
- multimodal/vision-capable model endpoint
- audio-to-text endpoint if separate

This routing belongs in runtime strategy code, not controllers or plugins.

---

## 11. Capability Plugin Strategy

Current handwritten functions should be migrated into Semantic Kernel plugins.

Initial plugin set:

- device status
- offline devices
- diagnostic logs

Planned plugin growth:

- device detail retrieval
- report export
- richer diagnostic workflows

Plugin methods should:

- accept strongly typed arguments when possible
- stay focused on business capability
- not contain prompt logic
- not contain HTTP contract logic

`Capabilities` is the correct home for these current-project capabilities.

---

## 12. Logging and Safety

### 12.1 Current Issue

The current controller logs raw user message content directly, which is not suitable for production-sensitive user input.

### 12.2 Target Behavior

Logging should be changed so that:

- request tracing keeps correlation identifiers
- raw sensitive multimodal content is not logged by default
- model execution metadata is logged in structured form
- tool calls are traceable without exposing more content than necessary

This is especially important once image and audio inputs are introduced.

---

## 13. Testing Strategy

The migration must be verified in layers.

### 13.1 Contract and Multimodal Tests

Validate:

- content item normalization
- text/image/audio input mapping
- request/response serialization contracts

### 13.2 Runtime Tests

Validate:

- kernel configuration
- model routing
- plugin registration
- tool-calling behavior
- prompt injection
- usage/citation/tool trace capture

### 13.3 Capability Tests

Validate:

- current business capability behavior remains correct after plugin migration
- argument mapping is correct
- capability outputs remain agent-consumable

### 13.4 Conversation and Persistence Tests

Validate:

- conversation creation
- history replay into runtime
- multi-user isolation
- concurrent append behavior
- metadata persistence

### 13.5 API Integration Tests

Validate:

- text-only requests
- text + image requests
- text + audio-file requests
- upgraded response structure

---

## 14. Migration Strategy

Migration should be staged, not done as a single risky replacement.

### Stage 1: New Structure and Contracts

- add Semantic Kernel dependencies
- introduce new folder structure
- define abstractions, contracts, conversation, multimodal models

### Stage 2: Runtime Skeleton

- create runtime interfaces and Semantic Kernel wiring
- add prompt provider structure
- add model routing strategy

### Stage 3: Capability Migration

- migrate handwritten functions into Semantic Kernel plugins
- add application service that bridges controller and runtime

### Stage 4: Conversation and Persistence

- implement real conversation store behavior
- read history before model execution
- persist structured message results after execution

### Stage 5: API Upgrade

- replace old text-only DTOs with multimodal request/response contracts
- update controller behavior

### Stage 6: Image and Audio File Integration

- unify image path into runtime
- add audio file transcription path

### Stage 7: Old Runtime Removal

- remove `IQwenClient`
- remove `QwenClient`
- remove `FunctionRegistry`
- remove `IAgentFunction`
- remove handwritten tool-calling loop

---

## 15. Legacy Code Disposition

### To Remove

- `IQwenClient`
- `QwenClient`
- `FunctionRegistry`
- `IAgentFunction`
- old handwritten tool-calling logic
- old text-only runtime assumptions

### To Temporarily Keep During Migration

- route/controller location
- high-level `IAiAgentService` naming if useful during transition
- current business entities until conversation storage is evolved

### To Keep Conceptually But Redesign

- system prompts
- conversation persistence
- AI chat controller entry

---

## 16. Risks and Mitigations

### Risk: API Surface Expansion

Upgrading the request/response contracts affects front-end and test code.

Mitigation:

- define contracts early
- update API and UI together
- verify serialization explicitly

### Risk: Conversation Model Migration

Moving from append-only records to true conversation state can introduce data and compatibility issues.

Mitigation:

- keep migration steps explicit
- write persistence tests before replacing old flow

### Risk: Multimodal Routing Complexity

Text, image, and audio may require different endpoints/models.

Mitigation:

- centralize routing in runtime strategy
- do not let routing logic leak to controllers or plugins

### Risk: Over-Coupling `Capabilities`

`Capabilities` may become a dumping ground.

Mitigation:

- keep subfolders explicit: `Application`, `Plugins`, `Persistence`
- forbid prompt logic and runtime orchestration logic there

### Risk: Realtime Voice Scope Creep

Adding realtime voice in the same migration would over-expand scope.

Mitigation:

- reserve architecture for realtime voice
- defer implementation

---

## 17. Final Design Decisions

- Use Semantic Kernel as the unified runtime.
- Keep implementation inside the existing `GB28181Platform.AiAgent` project initially.
- Design internals for future extraction.
- Use `Capabilities` as the project-specific integration layer name.
- Keep prompts separate from capabilities.
- Upgrade API contracts to multimodal request/response models.
- Upgrade conversation storage to real multi-turn history.
- Support text, image, and audio-file input in the first phase.
- Support multiple users and multi-device concurrent chat.
- Reserve realtime voice for a later phase.

---

## 18. Expected Outcome

After migration, the project will have:

- one unified Semantic Kernel based multimodal agent architecture
- one upgraded chat API capable of richer request/response structures
- real multi-turn conversation history
- multiple-user concurrent conversation support
- cleaner separation between reusable runtime and project-specific capabilities
- a codebase that can later be extracted into reusable agent libraries with much lower effort

