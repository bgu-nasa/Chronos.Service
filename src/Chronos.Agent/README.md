# Chronos.Agent — Conversational Scheduling Agent

A .NET 8.0 class library that provides a conversational AI agent for submitting scheduling constraints and preferences into the Chronos system — without users touching the REST API directly.

Users chat naturally ("I can't work Fridays and prefer mornings"), the agent extracts structured constraint/preference records, presents them for approval, and commits them to the Chronos data layer.

---

## Architecture Overview

```
User sends message
    │
    ▼
┌──────────────────────┐
│   AgentOrchestrator  │  ← Main entry point
│                      │
│  1. Manage session   │
│  2. Drive FSM        │
│  3. Call LLM         │
│  4. Extract draft    │
│  5. Submit on approve│
└──────┬───────────────┘
       │
       ├──► ILlmAdapter (polymorphic)
       │     ├── OllamaLlmAdapter   → University API (132.73.84.84)
       │     └── PuterLlmAdapter    → Puter free AI API
       │
       ├──► ConstraintExtractor
       │     └── LLM JSON extraction → ConstraintDraft
       │
       └──► IAgentSubmitter
             └── ServiceBackedSubmitter
                   ├── IAgentConstraintService  ─┐
                   └── IAgentPreferenceService   │  At bootstrap, these are
                         │                       │  wired to existing MainApi
                         ▼                       │  services which handle:
                   UserConstraintService ◄───────┘  • DB writes (EF Core)
                   UserPreferenceService            • RabbitMQ publishing
```

---

## Conversation Flow (Finite State Machine)

```
┌───────────┐    user talks    ┌───────────┐   extraction   ┌──────────┐
│ Discovery │ ──────────────► │  Drafting  │ ────────────► │  Submit  │
└───────────┘                  └───────────┘                └────┬─────┘
                                                                 │
                                                    ┌────────────┤
                                                    │            │
                                               [Revise]    [Approve]
                                                    │            │
                                                    ▼            ▼
                                              ┌──────────┐ ┌──────────┐
                                              │ Revision │ │ Approved │
                                              └────┬─────┘ └──────────┘
                                                   │              (terminal)
                                                   │ re-submit
                                                   └──────► Submit
```

| State       | Description                                                |
|-------------|------------------------------------------------------------|
| Discovery   | Free conversation — user describes their scheduling needs  |
| Drafting    | Agent structures conversation into constraint/preference records (transient) |
| Submit      | Agent presents structured proposal for user review         |
| Revision    | User requested changes — back to conversation mode         |
| Approved    | User approved — records committed to Chronos data layer    |

---

## Project Structure

```
Chronos.Agent/
├── AgentOrchestrator.cs              # Main entry point — coordinates everything
│
├── Configuration/
│   └── LlmProviderOptions.cs         # OllamaOptions + PuterOptions config classes
│
├── Conversation/
│   ├── AgentState.cs                  # FSM state enum
│   ├── AgentStateMachine.cs           # FSM logic + allowed actions per state
│   ├── ChatMessage.cs                 # Role/Content message record
│   ├── ConversationSession.cs         # Session aggregate (state + messages + draft)
│   ├── IConversationStore.cs          # Session persistence interface
│   └── InMemoryConversationStore.cs   # Thread-safe in-memory store (POC)
│
├── Domain/
│   ├── ConstraintDraft.cs             # Mutable draft during conversation
│   └── ConstraintProposal.cs          # Immutable approved set for submission
│
├── Extraction/
│   ├── ConstraintExtractor.cs         # LLM extraction → ConstraintDraft
│   ├── ILlmAdapter.cs                 # Polymorphic LLM interface
│   ├── KnownConstraintKeys.cs         # Valid key catalog + validation
│   ├── OllamaLlmAdapter.cs            # University Ollama API adapter
│   ├── PuterLlmAdapter.cs             # Puter free AI API adapter
│   └── PromptTemplates.cs             # System prompts (conversation + extraction)
│
└── Submission/
    ├── IAgentSubmitter.cs             # Submission interface
    ├── IAgentServices.cs              # Thin interfaces for existing Chronos services
    └── ServiceBackedSubmitter.cs      # Delegates to Chronos service layer
```

---

## File-by-File Reference

### `AgentOrchestrator.cs`
**The main entry point.** Coordinates the full conversation lifecycle:
- `StartSessionAsync()` — creates a new session with system prompt
- `SendMessageAsync()` — records user message, calls LLM for reply
- `RequestSubmitAsync()` — triggers extraction, transitions to Submit
- `ApproveAsync()` — converts draft to proposal, submits to Chronos
- `ReviseAsync()` — returns to Revision state for edits

Returns `AgentResponse` with current state, assistant message, draft, and allowed actions.

### Configuration/

#### `LlmProviderOptions.cs`
Configuration classes for the two LLM providers:
- **`OllamaOptions`** — `BaseUrl`, `Model` (defaults: `https://132.73.84.84`, `llama4`)
- **`PuterOptions`** — `BaseUrl`, `ApiToken`, `Model` (defaults: `https://api.puter.com`, `gpt-4o-mini`)

Both bind from `appsettings.json` under `Agent:Ollama` and `Agent:Puter` sections.

### Conversation/

#### `AgentState.cs`
Enum defining the five FSM states: `Discovery`, `Drafting`, `Submit`, `Revision`, `Approved`.

#### `AgentStateMachine.cs`
Dedicated FSM class that wraps `ConversationSession` and enforces valid transitions:
- `ProcessUserMessage()` — only valid in Discovery/Revision
- `RequestSubmit(draft)` — Discovery→Drafting→Submit or Revision→Submit
- `Approve()` — Submit→Approved
- `RequestRevision()` — Submit→Revision
- `GetAllowedActions()` — returns valid `AgentAction` set for the current state
- `CanTransition()` — static transition validity check

#### `ChatMessage.cs`
Simple record: `ChatMessage(string Role, string Content)`. Roles: `"system"`, `"user"`, `"assistant"`.

#### `ConversationSession.cs`
The session aggregate with **guarded mutation**:
- Stores `Id`, `UserId`, `OrganizationId`, `SchedulingPeriodId`
- `State` (private set) — only changed via `TransitionTo()` with validation
- `Messages` — read-only list, modified via `AddUserMessage()`, `AddAssistantMessage()`, `AddSystemMessage()`
- `Draft` — set via `SetDraft()`
- Invalid transitions throw `InvalidOperationException`

#### `IConversationStore.cs` / `InMemoryConversationStore.cs`
Persistence abstraction. The in-memory implementation uses `ConcurrentDictionary` for thread safety. Can be swapped for Redis/DB later.

### Domain/

#### `ConstraintDraft.cs`
**Mutable** draft built during conversation:
- `HardConstraints` — list of `DraftConstraint(Key, Value)` → maps to `UserConstraint`
- `SoftPreferences` — list of `DraftPreference(Key, Value)` → maps to `UserPreference`
- `AddHardConstraint()`, `AddSoftPreference()`, `Clear()`
- Exposed as `IReadOnlyList<T>` to prevent external mutation

#### `ConstraintProposal.cs`
**Immutable** approved set created from an approved draft:
- Contains fully-formed `UserConstraint` and `UserPreference` domain objects
- `ApprovedAt` timestamp
- Created by `AgentOrchestrator.ConvertToProposal()` only after user approval

### Extraction/

#### `ILlmAdapter.cs`
**Polymorphic interface** — the core abstraction enabling multiple LLM providers:
```csharp
Task<LlmResponse> ChatAsync(
    IReadOnlyList<ChatMessage> messages,
    LlmOptions? options,
    CancellationToken cancellationToken);
```
- `LlmResponse(string Content)` — the LLM's reply
- `LlmOptions { Model?, JsonMode }` — optional overrides per request

#### `OllamaLlmAdapter.cs`
Adapter for the **university Ollama API** (`https://132.73.84.84/api/chat`):
- Sends `POST /api/chat` with `{ model, messages, stream: false }`
- Supports `JsonMode` via Ollama's `format: "json"` parameter
- Model can be overridden per-request via `LlmOptions.Model`
- Uses injected `HttpClient` (configure cert bypass at DI registration for self-signed cert)

#### `PuterLlmAdapter.cs`
Adapter for the **Puter free AI API** (`https://api.puter.com/drivers/call`):
- Sends `POST /drivers/call` with the `puter-chat-completion` driver interface
- Uses Bearer token authentication
- All Puter-specific DTOs are **isolated inside the adapter** (no leakage)
- Good for local development without university VPN

#### `ConstraintExtractor.cs`
Uses the LLM in **extraction mode** (JSON output) to parse conversation into structured data:
1. Prepends the extraction system prompt to conversation messages
2. Calls `ILlmAdapter.ChatAsync()` with `JsonMode = true`
3. Parses the JSON response into hard constraints + soft preferences
4. **Validates keys** against `KnownConstraintKeys` — invalid keys are silently filtered
5. Throws `ExtractionException` if the LLM returns malformed JSON

#### `KnownConstraintKeys.cs`
Static catalog of valid constraint/preference keys from the Chronos domain:

| Category | Keys |
|----------|------|
| Hard constraints | `unavailable_day`, `avoid_weekday` |
| Soft preferences | `preferred_weekday`, `preferred_weekdays`, `preferred_time_morning`, `preferred_time_afternoon`, `preferred_time_evening`, `preferred_timerange` |

Provides `IsValid(key)` for validation and `AllKeys` for prompt generation.

#### `PromptTemplates.cs`
Two system prompts, **never mixed in a single request**:

1. **Conversation prompt** — used during Discovery/Revision to understand user intent, ask clarifying questions, and distinguish hard constraints from soft preferences
2. **Extraction prompt** — used when entering Submit mode to produce strict JSON output matching the known key schema

### Submission/

#### `IAgentSubmitter.cs`
Interface for committing approved proposals:
```csharp
Task SubmitAsync(ConstraintProposal proposal, CancellationToken cancellationToken);
```

#### `IAgentServices.cs`
**Thin adapter interfaces** that mirror the existing MainApi service methods:
- `IAgentConstraintService.CreateUserConstraintAsync(orgId, userId, periodId, key, value, weekNum?)`
- `IAgentPreferenceService.CreateUserPreferenceAsync(orgId, userId, periodId, key, value)`

These exist so the agent library doesn't depend on MainApi directly. At bootstrap, the MainApi registers its existing services as these interfaces.

#### `ServiceBackedSubmitter.cs`
Concrete submitter that **delegates to existing Chronos services**:
- Iterates through proposal constraints → calls `IAgentConstraintService.CreateUserConstraintAsync()`
- Iterates through proposal preferences → calls `IAgentPreferenceService.CreateUserPreferenceAsync()`

The existing services (`UserConstraintService`, `UserPreferenceService`) internally handle:
- **EF Core repository writes** (UserConstraint/UserPreference to PostgreSQL)
- **RabbitMQ publishing** (`HandleConstraintChangeRequest` with routing key `"request.online"`)
- **Validation** (organization exists, scheduling period valid)

This design means the agent **never duplicates DB writes or queue publishing logic**.

---

## Submission Flow (How Constraints Reach the Queue)

```
User approves in chat
    │
    ▼
AgentOrchestrator.ApproveAsync()
    │
    ├── FSM transitions to Approved
    ├── Converts ConstraintDraft → ConstraintProposal (immutable, with domain objects)
    │
    ▼
ServiceBackedSubmitter.SubmitAsync(proposal)
    │
    ├── For each hard constraint:
    │   └── IAgentConstraintService.CreateUserConstraintAsync()
    │       └── (wired to) UserConstraintService.CreateUserConstraintAsync()
    │           ├── repo.AddAsync()                          ← DB write
    │           └── messagePublisher.PublishAsync(            ← RabbitMQ
    │               HandleConstraintChangeRequest {
    │                   Scope: User,
    │                   Operation: Created,
    │                   Mode: Online
    │               }, "request.online")
    │
    └── For each soft preference:
        └── IAgentPreferenceService.CreateUserPreferenceAsync()
            └── (wired to) UserPreferenceService.CreateUserPreferenceAsync()
                └── repo.AddAsync()                          ← DB write
```

---

## Test Coverage

**82 tests** across 5 test files:

| Test File | Tests | What It Covers |
|-----------|-------|----------------|
| `ConversationTests.cs` | 24 | ChatMessage, ConstraintDraft, ConversationSession (state transitions, guarded mutation) |
| `AgentStateMachineTests.cs` | 17 | FSM transitions (valid/invalid), ProcessUserMessage, RequestSubmit, Approve, Revise, GetAllowedActions |
| `PromptTemplatesTests.cs` | 8 | Prompt content validation, KnownConstraintKeys catalog, key validation |
| `LlmAdapterTests.cs` | 10 | ILlmAdapter contract, Ollama wire format (JSON body, headers, model override, JSON mode, error handling), Puter wire format (driver call body, auth header, error handling) |
| `ConstraintExtractorTests.cs` | 6 | JSON parsing, multiple items, invalid key filtering, empty arrays, extraction prompt injection, malformed JSON handling |
| `SubmitterTests.cs` | 4 | IAgentSubmitter contract, ServiceBackedSubmitter delegation (constraints, preferences, empty proposal) |
| `AgentOrchestratorTests.cs` | 8 | Full flow: start session, send message, request submit, approve (verifies submitter called), revise, invalid session, invalid state |

All tests use **mocked HttpMessageHandler** for HTTP wire contract verification (outbound JSON shape, headers, response parsing) rather than just interface mocks.

---

## Bootstrap (TODO — Next Step)

To integrate into MainApi, register these services in DI:

```csharp
// In MainApi — adapter wrappers for existing services
services.AddScoped<IAgentConstraintService>(sp =>
    new UserConstraintServiceAdapter(sp.GetRequiredService<IUserConstraintService>()));
services.AddScoped<IAgentPreferenceService>(sp =>
    new UserPreferenceServiceAdapter(sp.GetRequiredService<IUserPreferenceService>()));

// Agent core
services.AddSingleton<IConversationStore, InMemoryConversationStore>();
services.AddScoped<ILlmAdapter, OllamaLlmAdapter>();  // or PuterLlmAdapter
services.AddScoped<ConstraintExtractor>();
services.AddScoped<IAgentSubmitter, ServiceBackedSubmitter>();
services.AddScoped<AgentOrchestrator>();
```

Configuration in `appsettings.json`:
```json
{
  "Agent": {
    "Ollama": {
      "BaseUrl": "https://132.73.84.84",
      "Model": "llama4"
    },
    "Puter": {
      "BaseUrl": "https://api.puter.com",
      "ApiToken": "your-token-here",
      "Model": "gpt-4o-mini"
    }
  }
}
```
