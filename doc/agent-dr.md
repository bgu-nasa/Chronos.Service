# Chronos Agent — Design Review

## Conversational Scheduling Agent (Class Library)

**From Chat → Structured Constraints/Preferences → Approval → Chronos Data Layer**

A separate class library (`Chronos.Agent`) that provides a conversational agent for submitting
scheduling constraints and preferences into the Chronos system — without users touching the API directly.

The library will be bootstrapped into its own deployable service later.

---

# Slide 1 — Title

## Chronos Conversational Agent

**Chat → Structured Constraints → Approval → Committed to Chronos**

Goal:
Let users submit their scheduling constraints and preferences through a conversational agent
instead of manually calling the Chronos REST API.

Project structure:

* `Chronos.Agent` — class library (this project)
* References `Chronos.Domain` for shared models
* Will be bootstrapped into a standalone service later

---

# Slide 2 — Problem Statement

Today in Chronos:

* Users submit constraints via API calls: `POST /api/schedule/constraints/userConstraint`
* Each constraint is a `Key`/`Value` pair per user per scheduling period
* Users must know the exact keys: `preferred_weekday`, `preferred_timerange`, `avoid_weekday`, etc.
* Users must know their `UserId`, `SchedulingPeriodId`, and the correct value format

This requires:

* Technical knowledge of the API
* Knowledge of valid constraint/preference keys
* Manual JSON construction
* No feedback loop — no way to know if constraints make sense together

---

# Slide 3 — Product Vision

A chat agent that behaves like a **smart scheduling assistant**.

Flow:

1. User chats naturally: _"I prefer mornings on Monday and Wednesday"_
2. Agent maps this to Chronos domain: `preferred_weekdays = Monday,Wednesday` + `preferred_time_morning = true`
3. Agent enters **Submit Mode** — shows the structured constraints
4. User approves or edits
5. Agent writes constraints/preferences directly to the Chronos data layer

Key concept:

> The agent never commits changes without explicit user approval.

---

# Slide 4 — What The Agent Actually Writes

The agent produces `UserConstraint` and `UserPreference` records — the same types Chronos already uses.

### Existing Chronos domain models the agent targets:

```csharp
// Hard constraints — must be satisfied
class UserConstraint : ObjectInformation
{
    Guid Id, OrganizationId, UserId, SchedulingPeriodId;
    string Key;   // e.g. "unavailable_day"
    string Value;  // e.g. "Friday"
}

// Soft preferences — weighted in ranking
class UserPreference : ObjectInformation
{
    Guid Id, OrganizationId, UserId, SchedulingPeriodId;
    string Key;   // e.g. "preferred_weekday"
    string Value;  // e.g. "Monday"
}
```

### Known preference keys (from PreferenceWeightedRanker):

| Key                        | Value format                          | Weight |
| -------------------------- | ------------------------------------- | ------ |
| `preferred_weekday`        | `Monday`                              | 3.0x   |
| `preferred_weekdays`       | `Monday,Wednesday,Friday`             | 3.0x   |
| `avoid_weekday`            | `Friday`                              | 0.3x   |
| `preferred_time_morning`   | `true`                                | 3.0x   |
| `preferred_time_afternoon` | `true`                                | 2.0x   |
| `preferred_time_evening`   | `true`                                | 2.0x   |
| `preferred_timerange`      | `Monday 09:00 - 11:00, Tuesday 13:00 - 15:00` | 4.0x   |

---

# Slide 5 — Core UX States

The agent uses a **finite state machine**.

States:

1. **Discovery** — Free conversation, clarifications
2. **Drafting** — Agent internally structures info into constraint/preference records
3. **Submit** — Agent presents structured proposal to user
4. **Revision** — User asks for changes, agent updates draft
5. **Approved** — Records committed to Chronos data layer

---

# Slide 6 — Example Conversation

User:
> I can't work on Fridays and I prefer morning shifts on Monday and Wednesday

Agent:
> Got it. You want to avoid Fridays entirely and prefer mornings on Monday and Wednesday. Is that for the current scheduling period?

User:
> Yes

Agent switches → **Submit Mode**

```
Constraints (hard):
  1. avoid_weekday = Friday

Preferences (soft):
  2. preferred_weekdays = Monday,Wednesday
  3. preferred_time_morning = true

Scheduling Period: Spring 2026
[Approve] [Make changes]
```

---

# Slide 7 — Why Submit Mode Matters

Without Submit Mode:

* LLM might misinterpret "I don't love Fridays" as a hard constraint vs soft preference
* No audit trail of what was committed
* Users lose trust

Submit Mode gives:

* Explicit contract — user sees exact keys/values
* Predictability — same input → same proposal
* Trust — human-in-the-loop
* Correctness — separates hard constraints from soft preferences

This is the **most critical design decision**.

---

# Slide 8 — Project Architecture

### Chronos.Agent (Class Library)

```
Chronos.Agent/
├── Conversation/
│   ├── ConversationSession.cs        # State + messages + draft
│   ├── AgentStateMachine.cs          # FSM: Discovery → Submit → Approved
│   └── IConversationStore.cs         # Persist sessions (in-memory for POC)
├── Extraction/
│   ├── ConstraintExtractor.cs        # LLM → structured constraints
│   ├── ILlmAdapter.cs               # Abstraction over LLM provider
│   └── PromptTemplates.cs           # System prompts for conversation + extraction
├── Domain/
│   ├── ConstraintDraft.cs            # Mutable draft before approval
│   └── ConstraintProposal.cs         # Immutable approved set
├── Submission/
│   ├── ChronosSubmitter.cs           # Writes to Chronos data layer
│   └── IChronosSubmitter.cs          # Interface for testability
└── AgentOrchestrator.cs              # Main entry point — coordinates everything
```

### References:

* `Chronos.Domain` — shares UserConstraint, UserPreference, SchedulingPeriod models
* `Chronos.Data` — direct repository access for writing constraints/preferences

The agent does **not** go through the HTTP API — it uses the data layer directly
(same repositories the API uses). This avoids auth complexity for the POC.

---

# Slide 9 — Conversation State Model

```csharp
class ConversationSession
{
    Guid Id;
    Guid UserId;
    Guid OrganizationId;
    Guid SchedulingPeriodId;
    AgentState State;
    List<ChatMessage> Messages;
    ConstraintDraft? Draft;
}

enum AgentState { Discovery, Drafting, Submit, Revision, Approved }

class ConstraintDraft
{
    List<DraftConstraint> HardConstraints;   // → UserConstraint
    List<DraftPreference> SoftPreferences;   // → UserPreference
}

record DraftConstraint(string Key, string Value);
record DraftPreference(string Key, string Value);
```

---

# Slide 10 — Key Concept: Draft vs Approved

### Draft (mutable)

Work in progress from chat. Can be revised.

### Approved (immutable)

Committed to Chronos repositories.

```csharp
class ConstraintDraft
{
    List<DraftConstraint> HardConstraints;
    List<DraftPreference> SoftPreferences;
}

class ConstraintProposal  // sealed, created from approved draft
{
    Guid UserId;
    Guid OrganizationId;
    Guid SchedulingPeriodId;
    IReadOnlyList<UserConstraint> Constraints;
    IReadOnlyList<UserPreference> Preferences;
    DateTime ApprovedAt;
}
```

Never write a Draft directly. Only write an approved `ConstraintProposal`.

---

# Slide 11 — LLM Prompt Strategy

We use **two prompt modes** (never mixed):

### Conversation Prompt

Goal: understand user intent, ask clarifying questions.
System prompt includes the list of valid constraint/preference keys so the LLM
understands the domain.

### Extraction Prompt

Goal: output strict JSON mapping user intent to constraint/preference key-value pairs.
System prompt includes the exact schema and valid keys.

This separation reduces hallucinations dramatically.

---

# Slide 12 — Extraction Prompt Contract

When entering Submit Mode, the extraction prompt produces:

```json
{
  "hardConstraints": [
    { "key": "avoid_weekday", "value": "Friday" }
  ],
  "softPreferences": [
    { "key": "preferred_weekdays", "value": "Monday,Wednesday" },
    { "key": "preferred_time_morning", "value": "true" }
  ]
}
```

The agent validates:
1. All keys are in the known key set
2. Value formats match expected patterns (weekday names, time formats, booleans)
3. No contradictions (e.g., `preferred_weekday = Friday` + `avoid_weekday = Friday`)

---

# Slide 13 — Submit Mode Rendering

The agent returns a structured response:

```json
{
  "mode": "submit",
  "schedulingPeriod": "Spring 2026",
  "hardConstraints": [
    "Avoid weekday: Friday"
  ],
  "softPreferences": [
    "Preferred weekdays: Monday, Wednesday",
    "Preferred time: Morning"
  ],
  "draft": { ... }
}
```

The frontend (or CLI) renders:
* Constraint summary (hard vs soft clearly separated)
* Approve button
* Make changes button

---

# Slide 14 — Approval Flow

When user approves:

1. Agent validates draft still consistent
2. Converts `ConstraintDraft` → `ConstraintProposal`
3. Writes `UserConstraint` records via `IUserConstraintRepository`
4. Writes `UserPreference` records via `IUserPreferenceRepository`
5. Marks session as `Approved`

```csharp
// ChronosSubmitter.cs
async Task SubmitAsync(ConstraintProposal proposal)
{
    foreach (var c in proposal.Constraints)
        await userConstraintRepo.CreateAsync(c);
    foreach (var p in proposal.Preferences)
        await userPreferenceRepo.CreateAsync(p);
}
```

---

# Slide 15 — Revision Flow

User clicks "Make changes":

* Session returns to **Revision** state
* Agent: _"What would you like to change?"_
* User: _"Actually, also avoid Tuesday"_
* Agent updates draft, returns to Submit Mode

The draft is modified in place. Previous versions are not tracked (MVP).

---

# Slide 16 — Integration with Chronos Engine

After constraints/preferences are written, the existing Chronos Engine uses them:

1. `PreferenceWeightedRanker` reads `UserPreference` records and weights slot candidates
2. `ConstraintEvaluator` + validators check hard constraints during matching
3. `MatchingOrchestrator` runs batch or online scheduling

The agent doesn't trigger scheduling — it just ensures the data is in the database
for the next scheduling run.

---

# Slide 17 — Why a Class Library?

Benefits of keeping this as a class library (not a service yet):

* **Testable** — can unit test all logic without HTTP/infrastructure
* **Composable** — can be hosted in the MainApi, a worker, or a standalone service
* **No premature infrastructure** — no Docker, no ports, no deployment pipeline yet
* **Shared domain** — uses the same `Chronos.Domain` models as the rest of the system

When ready to deploy:

```
Chronos.Agent.Service/        # Thin host project
├── Program.cs                # ASP.NET Minimal API or Worker Service
├── Dockerfile
└── References: Chronos.Agent
```

---

# Slide 18 — Ollama Migration Plan

Current POC:

* LLM via external API (OpenAI-compatible)

Future:

* Replace `ILlmAdapter` implementation with Ollama client

Because:

* All prompts live in the agent library
* `ILlmAdapter` abstracts the provider
* State machine is LLM-agnostic

Migration cost: **one new class**.

---

# Slide 19 — Risks & Mitigations

| Risk                          | Mitigation                                |
| ----------------------------- | ----------------------------------------- |
| LLM misinterprets constraint  | Submit Mode + human approval              |
| Invalid constraint key        | Validation against known key set          |
| Contradictory constraints     | Contradiction detection before submit     |
| Value format errors           | Regex validation per key type             |
| User submits for wrong period | Session bound to SchedulingPeriod upfront |
| LLM provider swap             | ILlmAdapter abstraction                   |

---

# Slide 20 — MVP Scope

MVP includes:

* Conversation → constraint extraction for known keys
* Submit mode with approval
* Direct write to Chronos repositories
* In-memory conversation store
* Single scheduling period per session

Not in MVP:

* Multi-period sessions
* Constraint history / undo
* Real-time conflict detection with other users
* Calendar integration
* Standalone service deployment

---

# Slide 21 — Next Steps

1. Create `Chronos.Agent` class library project
2. Implement `AgentStateMachine` with FSM
3. Implement `ILlmAdapter` with OpenAI-compatible client
4. Build extraction prompt with known Chronos constraint keys
5. Implement `ChronosSubmitter` using existing repositories
6. Add unit tests for state machine + extraction validation
7. Integrate into a simple CLI or chat UI for demo
