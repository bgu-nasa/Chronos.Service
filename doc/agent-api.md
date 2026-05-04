# Chronos Agent API Documentation

The Chronos Agent provides a conversational interface for submitting scheduling constraints and preferences.
Users chat naturally (e.g., "I can't work Fridays and prefer mornings"), and the agent extracts structured data,
presents it for approval, and commits it to the Chronos data layer.

---

## Base URL

```
/api/agent
```

All endpoints require **JWT authentication** (Bearer token) and the `x-org-id` header with the user's organization ID.

---

## Authentication

Every request must include:

| Header          | Description                                         |
|-----------------|-----------------------------------------------------|
| `Authorization` | `Bearer <JWT token>` — obtained from `/api/auth/login` |
| `x-org-id`      | Organization UUID — the user's active organization   |

The `userId` is extracted from the JWT claims automatically — clients never send it in request bodies.

---

## Conversation Flow

The agent uses a finite state machine (FSM):

```
Discovery → Drafting → Submit → Approved (terminal)
                          ↕
                       Revision
```

| State     | Description                                           | Allowed Actions            |
|-----------|-------------------------------------------------------|----------------------------|
| Discovery | Free conversation — user describes scheduling needs   | ContinueConversation, RequestSubmit |
| Drafting  | Transient — agent structures info (no client action)  | —                          |
| Submit    | Agent presents proposal for review                    | Approve, Revise            |
| Revision  | User requested changes — back to conversation         | ContinueConversation, RequestSubmit |
| Approved  | Committed to Chronos — session is done                | (none)                     |

---

## Endpoints

### 1. Start Session

Creates a new conversation session.

```
POST /api/agent/sessions
```

**Request Body:**

```json
{
  "schedulingPeriodId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response:** `201 Created`

```json
{
  "sessionId": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

**Notes:**
- `schedulingPeriodId` must be a valid scheduling period in the user's organization.
- Each session is bound to one user, one organization, and one scheduling period.

---

### 2. Send Message

Sends a user message and gets a conversational reply from the LLM.

```
POST /api/agent/sessions/{sessionId}/messages
```

**Request Body:**

```json
{
  "message": "I can't work on Fridays and I prefer morning shifts"
}
```

**Response:** `200 OK`

```json
{
  "sessionId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "state": "Discovery",
  "assistantMessage": "Got it! You want to avoid Fridays entirely and prefer morning shifts. Would you like to specify which days you prefer mornings, or is that for all days?",
  "draft": null,
  "allowedActions": ["ContinueConversation", "RequestSubmit"]
}
```

**Valid states:** Discovery, Revision

---

### 3. Request Submit

Triggers LLM extraction and transitions to Submit state. The agent parses the conversation into structured constraints/preferences.

```
POST /api/agent/sessions/{sessionId}/submit
```

**Request Body:** None

**Response:** `200 OK`

```json
{
  "sessionId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "state": "Submit",
  "assistantMessage": null,
  "draft": {
    "hardConstraints": [
      { "key": "avoid_weekday", "value": "Friday" }
    ],
    "softPreferences": [
      { "key": "preferred_time_morning", "value": "true" }
    ]
  },
  "allowedActions": ["Approve", "Revise"]
}
```

**Valid states:** Discovery, Revision

---

### 4. Approve

Approves the current draft and submits constraints/preferences to the Chronos data layer.

```
POST /api/agent/sessions/{sessionId}/approve
```

**Request Body:** None

**Response:** `200 OK`

```json
{
  "sessionId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "state": "Approved",
  "assistantMessage": null,
  "draft": { "hardConstraints": [...], "softPreferences": [...] },
  "allowedActions": []
}
```

**What happens on approval:**
1. FSM transitions to `Approved`
2. Draft is converted to immutable `ConstraintProposal`
3. Hard constraints → written as `UserConstraint` records (DB + RabbitMQ event)
4. Soft preferences → written as `UserPreference` records (DB)
5. Session becomes terminal (no further actions)

**Valid state:** Submit only

---

### 5. Revise

Returns to Revision state so the user can make changes.

```
POST /api/agent/sessions/{sessionId}/revise
```

**Request Body:** None

**Response:** `200 OK`

```json
{
  "sessionId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "state": "Revision",
  "assistantMessage": null,
  "draft": { "hardConstraints": [...], "softPreferences": [...] },
  "allowedActions": ["ContinueConversation", "RequestSubmit"]
}
```

**Valid state:** Submit only

---

## Error Responses

All errors follow this format:

```json
{
  "statusCode": 400,
  "message": "Cannot transition from Discovery to Approved.",
  "type": "BadRequest"
}
```

| Status | When                                                         |
|--------|--------------------------------------------------------------|
| 400    | Invalid state transition (e.g., approve before submit)       |
| 400    | Constraint extraction failed (malformed LLM response)        |
| 401    | Missing or invalid JWT token                                 |
| 401    | Session belongs to a different user/organization             |
| 404    | Session not found                                            |
| 500    | LLM provider unreachable or internal error                   |

---

## Known Constraint/Preference Keys

The agent only produces constraints using these validated keys:

### Hard Constraints

| Key               | Value Format | Effect                    |
|-------------------|--------------|---------------------------|
| `unavailable_day` | `Friday`     | User cannot be scheduled  |
| `avoid_weekday`   | `Friday`     | User must avoid this day  |

### Soft Preferences

| Key                        | Value Format                       | Weight |
|----------------------------|------------------------------------|--------|
| `preferred_weekday`        | `Monday`                           | 3.0x   |
| `preferred_weekdays`       | `Monday,Wednesday,Friday`          | 3.0x   |
| `avoid_weekday`            | `Friday`                           | 0.3x   |
| `preferred_time_morning`   | `true`                             | 3.0x   |
| `preferred_time_afternoon` | `true`                             | 2.0x   |
| `preferred_time_evening`   | `true`                             | 2.0x   |
| `preferred_timerange`      | `Monday 09:00 - 11:00, ...`       | 4.0x   |

Any key the LLM produces that isn't in this list is silently filtered out.

---

## Configuration

The agent's LLM provider is configured via environment variables or `appsettings.json`:

| Variable                 | Default                    | Description                    |
|--------------------------|----------------------------|--------------------------------|
| `Agent__LlmProvider`     | `ollama`                   | `"ollama"` or `"puter"`        |
| `Agent__Ollama__BaseUrl` | (empty)                    | University Ollama server URL   |
| `Agent__Ollama__Model`   | `phi3`                     | Model name for Ollama          |
| `Agent__Puter__BaseUrl`  | `https://api.puter.com`    | Puter API base URL             |
| `Agent__Puter__ApiToken` | (empty)                    | Bearer token for Puter         |
| `Agent__Puter__Model`    | `gpt-4o-mini`              | Model name for Puter           |

### LLM Provider Selection

- **Ollama** (default): University Ollama server. Set `Agent__Ollama__BaseUrl` to the server URL. Requires VPN access to the university network. Uses a self-signed certificate (bypassed automatically).
- **Puter**: Free AI API for local development without VPN. Requires an API token from [puter.com](https://puter.com).

---

## Important Notes

1. **Human-in-the-loop**: The agent never commits changes without explicit user approval (the `approve` endpoint).
2. **Session isolation**: Each session is bound to one user + org + scheduling period. Cross-user access is denied.
3. **Stateless sessions (POC)**: Sessions are stored in-memory and lost on service restart. This is acceptable for the POC.
4. **No contradiction detection**: The agent does not yet detect contradictory constraints (e.g., `preferred_weekday=Friday` + `avoid_weekday=Friday`).
5. **Downstream effects**: When constraints are approved, existing Chronos infrastructure handles:
   - DB persistence via EF Core
   - RabbitMQ event publishing for online scheduling
   - No additional triggering is needed from the client.

---

## Example: Full Conversation Flow

```bash
# 1. Start session
curl -X POST http://localhost:5000/api/agent/sessions \
  -H "Authorization: Bearer $TOKEN" \
  -H "x-org-id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{"schedulingPeriodId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}'

# Response: {"sessionId": "11111111-2222-3333-4444-555555555555"}

# 2. Send messages
curl -X POST http://localhost:5000/api/agent/sessions/11111111-2222-3333-4444-555555555555/messages \
  -H "Authorization: Bearer $TOKEN" \
  -H "x-org-id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{"message": "I cannot work on Fridays and I prefer morning shifts on Monday and Wednesday"}'

# 3. Request submit (extract constraints)
curl -X POST http://localhost:5000/api/agent/sessions/11111111-2222-3333-4444-555555555555/submit \
  -H "Authorization: Bearer $TOKEN" \
  -H "x-org-id: $ORG_ID"

# 4. Review the draft in the response, then approve
curl -X POST http://localhost:5000/api/agent/sessions/11111111-2222-3333-4444-555555555555/approve \
  -H "Authorization: Bearer $TOKEN" \
  -H "x-org-id: $ORG_ID"

# Done! Constraints are now in the database.
```
