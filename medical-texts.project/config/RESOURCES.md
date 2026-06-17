# Resources for Development

---

## Microsoft Azure AI

### Links

- https://learn.microsoft.com/en-us/python/api/azure-ai-inference/azure.ai.inference.chatcompletionsclient?view=azure-python-preview
- https://learn.microsoft.com/en-us/azure/foundry/openai/latest#create-chat-completion
- https://learn.microsoft.com/en-us/azure/foundry/openai/latest#openaiverbosity
- https://learn.microsoft.com/en-us/azure/foundry/openai/latest#openaicreatechatcompletionrequestresponseformat
- https://learn.microsoft.com/en-us/azure/foundry/openai/latest#openaicreatechatcompletionrequestresponseformatresponseformattext
- https://learn.microsoft.com/en-us/azure/foundry/openai/latest#openaicreatechatcompletionrequestresponseformattype
- https://learn.microsoft.com/en-us/azure/foundry/openai/latest#create-chat-completion
- https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/runtime-components?tabs=csharp
- https://learn.microsoft.com/en-us/azure/foundry/agents/overview

### Azure AI Foundry — API Parameter Landscape

**Model under test:** DeepSeek-V4-Pro (Global Standard deployment)  
**Investigated:** 2026-06-13  
**Spec reference:** [Azure Foundry OpenAI v1 REST API](https://learn.microsoft.com/en-us/azure/foundry/openai/latest)

#### 1. Chat/Completions API (`/openai/v1/chat/completions`)

##### 1a. Parameters sent by the harness (all return 200)

24 parameters currently submitted per request. `OmitNullFields = true` strips null values before sending.

| Parameter | Value | Notes |
|---|---|---|
| `model` | `"DeepSeek-V4-Pro"` | Required |
| `messages` | user prompt array | Required |
| `temperature` | `0.0` | |
| `top_p` | `0.01` | |
| `max_completion_tokens` | `16384` | |
| `presence_penalty` | `0.0` | |
| `frequency_penalty` | `0.0` | |
| `stop` | `null` → omitted | |
| `seed` | `7294853106482917` | Accepted; silently ignored by DeepSeek |
| `reasoning_effort` | `"none"` | |
| `n` | `1` | |
| `logprobs` | `false` | |
| `top_logprobs` | `null` → omitted | |
| `logit_bias` | `null` → omitted | |
| `response_format` | `{"type": "text"}` | Maps to agent's `text.format.type` |
| `modalities` | `["text"]` | |
| `audio` | `null` → omitted | |
| `stream` | `false` | |
| `tools` | `null` → omitted | |
| `tool_choice` | `"none"` | |
| `parallel_tool_calls` | `false` | |
| `prediction` | `null` → omitted | |
| `store` | `false` | |
| `metadata` | `null` → omitted | |

##### 1b. In-spec parameters that return 400 on DeepSeek-V4-Pro

These fields are documented in the Azure Foundry chat/completions spec but DeepSeek-V4-Pro's deployment does not honour them. All return `400 unrecognized_request_argument`.

| Parameter | In Azure spec | Error code |
|---|---|---|
| `verbosity` | ✓ | `unrecognized_request_argument` |
| `prompt_cache_key` | ✓ | `unrecognized_request_argument` |
| `prompt_cache_retention` | ✓ | `unrecognized_request_argument` |
| `safety_identifier` | ✓ | `unrecognized_request_argument` |
| `user_security_context` | ✓ | `unrecognized_request_argument` |
| `background` | — (images API only) | `unrecognized_request_argument` |
| `thinking` | — (DeepSeek native only) | `unrecognized_request_argument` |

All use the same error code. The pattern: Azure Foundry platform features pass through to the model endpoint as-is, and DeepSeek-V4-Pro rejects anything it doesn't recognise.

##### 1c. In-spec parameters confirmed accepted (previously uncertain)

| Parameter | Test result |
|---|---|
| `user` | 200 — accepted (deprecated per spec but functional) |
| `service_tier` | 200 — accepted |
| `top_logprobs` (with `logprobs: true`) | 200 — accepted |
| `stream_options` | 200 — accepted |

##### 1d. Excluded parameters (not sent, not 400)

| Parameter | Reason excluded |
|---|---|
| `stream_options: null` | Confirmed to suppress `usage` in responses regardless of `OmitNullFields` |

---

#### 2. Responses API (`/protocols/openai/responses`) — Agent endpoint

##### 2a. Caller-settable parameters (all return 200)

18 confirmed parameters. Previously believed to be 11 — the earlier probe had a false-positive rejection due to `"error": null` appearing in all successful response bodies (matched by a `grep '"error"'` check).

| # | Parameter | Notes |
|---|---|---|
| 1 | `input` | Required. Array of `{role, content}` objects |
| 2 | `max_output_tokens` | Min value: 16 |
| 3 | `stream` | |
| 4 | `store` | |
| 5 | `tools` | |
| 6 | `tool_choice` | |
| 7 | `parallel_tool_calls` | |
| 8 | `truncation` | |
| 9 | `include` | |
| 10 | `metadata` | Must be object or omitted; null returns 400 |
| 11 | `previous_response_id` | Must be string if provided; null returns 400 |
| 12 | `user` | Echoed in response |
| 13 | `service_tier` | Echoed in response |
| 14 | `background` | bool; echoed in response |
| 15 | `top_logprobs` | int; echoed in response |
| 16 | `safety_identifier` | Accepted; not echoed |
| 17 | `prompt_cache_key` | Echoed in response |
| 18 | `prompt_cache_retention` | Accepted values: `null`, `"in_memory"`, `"24h"` |

##### 2b. Agent-owned parameters — rejected (400) when sent by caller

These live in the agent definition. The Responses API returns `400 Not allowed when agent is specified`.

| Parameter | Owned by |
|---|---|
| `temperature` | Agent definition |
| `top_p` | Agent definition |
| `reasoning` | Agent definition / Foundry default |
| `text` | Agent definition / Foundry default |
| `instructions` | Agent definition |

##### 2c. Not in Responses API surface (400)

| Parameter | Error |
|---|---|
| `seed` | `"Unknown parameter: 'seed'"` |
| `logprobs` | `"Unknown parameter: 'logprobs'"` |
| `verbosity` (top-level) | `"Unsupported parameter: 'verbosity'. In the Responses API, ..."` — lives in `text.verbosity` of the agent definition |

---

#### 3. Agent response echo — full parameter field map

Every Responses API response body echoes all effective parameters. System/envelope fields (`id`, `object`, `status`, `created_at`, `completed_at`, `output`, `agent`, `agent_reference`, `content_filters`, `incomplete_details`) are excluded below.

| Field | Echoed value | Source |
|---|---|---|
| `temperature` | `0` | Agent definition (explicit) |
| `top_p` | `0.0099999997764825` | Agent definition (explicit; float32→float64 artefact of 0.01) |
| `model` | `"DeepSeek-V4-Pro"` | Agent definition (explicit) |
| `tools` | `[]` | Agent definition (explicit) |
| `instructions` | `null` | Agent definition (empty → null) |
| `text` | `{"format": {"type": "text"}, "verbosity": "medium"}` | Foundry serving default |
| `reasoning` | `{"context": null}` | Foundry serving default (Azure-specific field) |
| `prompt_cache_retention` | `"in_memory"` | Foundry serving default |
| `service_tier` | `"auto"` | Foundry serving default |
| `tool_choice` | `"auto"` | Foundry serving default |
| `truncation` | `"disabled"` | Foundry serving default |
| `parallel_tool_calls` | `false` | Foundry serving default |
| `background` | `false` | Foundry serving default |
| `top_logprobs` | `0` | Foundry serving default |
| `metadata` | `{}` | Foundry serving default |
| `max_output_tokens` | *(caller-sent value)* | Caller |
| `usage` | input/output token counts | Response output — not a settable parameter |

**Note on `reasoning`:** The OpenAI Responses API spec defines `reasoning` as `{"effort": null, "summary": null}`. Azure Foundry echoes `{"context": null}` — the `context` subfield is an Azure-specific extension not in the OpenAI spec.

---

#### 4. `text` object — cross-API field mapping

The agent's `text: {"format": {"type": "text"}, "verbosity": "medium"}` maps to two separate fields on the chat/completions API:

| Agent `text` sub-field | Value | chat/completions equivalent | Harness current value |
|---|---|---|---|
| `text.format.type` | `"text"` | `response_format: {"type": "text"}` | ✓ Set |
| `text.verbosity` | `"medium"` | `verbosity: "medium"` (top-level) | ✗ Commented out — DeepSeek returns `unrecognized_request_argument` |

The `text` wrapper object does not exist as a field in the chat/completions API spec. It is the Foundry Agent Runtime's internal representation that folds `response_format` and `verbosity` into a single object for the agent definition schema.

---

#### 5. Verbosity and the Foundry Agent Runtime

`verbosity` is a documented top-level field in the Azure Foundry chat/completions spec (`OpenAI.Verbosity` enum: `low` / `medium` / `high`). It is also documented in the OpenAI Responses API spec as `text.verbosity` inside `ResponseTextParam`.

DeepSeek-V4-Pro returns `unrecognized_request_argument` for `verbosity` on the chat/completions endpoint.

The Foundry Agent Runtime reads `text.verbosity: "medium"` from the agent definition and applies it server-side. The mechanism is not publicly documented. **Observable consequence:** The agent arm averages 161 output tokens vs 186 for the model arm across 25 runs, despite identical `temperature`, `top_p`, and `reasoning_effort`. The token difference is attributable to the runtime's verbosity enforcement.

**Implication for controlled comparison:** The two arms (model API vs agent API) are not parameter-equivalent. Any output difference between arms may reflect runtime behaviour rather than model behaviour.

---

#### 6. Azure-specific extensions to the OpenAI spec

| Field | OpenAI spec | Azure Foundry |
|---|---|---|
| `reasoning.effort` | `{"effort": null, "summary": null}` | Absent when effort is "none"/default |
| `reasoning.summary` | `{"effort": null, "summary": null}` | Absent |
| `reasoning.context` | Not in spec | `{"context": null}` — Azure-only extension |
| `prompt_cache_key` | Not in spec | Azure chat/completions addition |
| `prompt_cache_retention` | Not in spec | Azure chat/completions addition |
| `safety_identifier` | Not in spec | Azure chat/completions addition |
| `user_security_context` | Not in spec | Azure chat/completions addition |

---

## DeepSeek

### Links

- https://api-docs.deepseek.com/
- https://api-docs.deepseek.com/api/deepseek-api
- https://api-docs.deepseek.com/api/create-chat-completion

### DeepSeek-V4-Pro Native API Parameters

Source: [api-docs.deepseek.com](https://api-docs.deepseek.com/api/create-chat-completion), verified June 2026.

#### Supported parameters

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `messages` | array | — | Required. Roles: system, user, assistant, tool. |
| `model` | string | — | Required. `deepseek-v4-pro` or `deepseek-v4-flash`. |
| `max_tokens` | integer | — | Input + output must not exceed context length. |
| `temperature` | number | 1 | Range 0–2. |
| `top_p` | number | 1 | Range 0–1. Nucleus sampling. |
| `stop` | string / array | — | Up to 16 stop sequences. |
| `stream` | boolean | — | SSE streaming. |
| `stream_options` | object | — | `{"include_usage": true}` to receive token stats at end of stream. |
| `response_format` | object | — | `{"type": "json_object"}`. Requires explicit JSON instruction in prompt. |
| `tools` | array | — | Max 128 function definitions. |
| `tool_choice` | string / object | — | `none` / `auto` / `required` / specific function. |
| `logprobs` | boolean | — | Return log-probabilities of output tokens. |
| `top_logprobs` | integer | — | 0–20. Most-likely tokens per position. |
| `user_id` | string | — | Max 512 chars, `a-zA-Z0-9\-\_`. Used for cache isolation and scheduling. |
| `thinking` | object | — | See below. DeepSeek-specific. |

#### Thinking mode (`thinking` + `reasoning_effort`)

Controls the model's internal chain-of-thought reasoning. V4-Pro is a general model with *optional* thinking, distinct from pure reasoning models (e.g. DeepSeek-R1) where thinking is always on.

`thinking` and `reasoning_effort` are **separate top-level fields** — `reasoning_effort` is not nested inside `thinking`:

```json
{
  "thinking": { "type": "enabled" },
  "reasoning_effort": "high"
}
```

| Field | Sub-field / Values | Default | Notes |
|---|---|---|---|
| `thinking.type` | `"enabled"` / `"disabled"` | `"enabled"` | Whether thinking is active for this request. |
| `reasoning_effort` | `"high"` / `"max"` | `"high"` | Top-level field. `"max"` is auto-set by agentic tools. Azure surface additionally exposes `"none"` / `"minimal"` / `"low"` / `"medium"` / `"xhigh"`. |

When thinking is enabled, the model's reasoning trace appears in the response content inside `<think>…</think>` tags before the final answer. Both the reasoning and the answer count toward token usage.

To suppress reasoning entirely, set both:
```json
{
  "thinking": { "type": "disabled" },
  "reasoning_effort": "none"
}
```
`thinking.type: "disabled"` is the native DeepSeek control; `reasoning_effort: "none"` is the Azure surface extension. Setting both is belt-and-suspenders.

#### Parameters NOT in the official DeepSeek spec

| Parameter | Origin | Effect when sent |
|-----------|--------|-----------------|
| `seed` | OpenAI API | Accepted without error; silently ignored. Confirmed by Study 2: identical hash distributions across unseeded, seed-42, and seed-7 arms. |
| `presence_penalty` | OpenAI API | Not listed in DeepSeek spec; acceptance and effect unconfirmed. |
| `frequency_penalty` | OpenAI API | Not listed in DeepSeek spec; acceptance and effect unconfirmed. |

The Study 1 playground runs used `presence_penalty` and `frequency_penalty` set to −2.0 (negative, to maximise recall). Whether those values had any effect on the output — or whether recall reflects temperature=0 and top_p=0.01 alone — cannot be determined from the harness results, since the endpoint neither confirms nor rejects these fields.

#### Parameters available on third-party wrappers only

Some providers (e.g. AI/ML API, Together AI) re-expose DeepSeek-V4-Pro with additional parameters from their own inference stacks (`top_k`, `min_p`, `top_a`, `repetition_penalty`, `web_search_options`). These are wrapper-layer features, not native DeepSeek API parameters.

#### Azure AI Foundry surface notes

- `system_fingerprint` is **not returned** in responses for third-party model deployments (DeepSeek, Mistral, etc.) on Azure AI Foundry. It is an OpenAI-proprietary field.
- The `azureml-model-session` response header (e.g. `d20260515214838-d5c76969`) encodes the deployment session creation date and serves as a session-level identifier, but is not an API version field.
- The reproducibility anchor for long-term re-running is the registry asset ID, not any HTTP header: `azureml://registries/azureml-deepseek/models/DeepSeek-V4-Pro/versions/2026-04-23`.

---

## OpenAI

### Links

- https://developers.openai.com/api/docs
- https://developers.openai.com/api/docs/quickstart?language=csharp
- https://developers.openai.com/api/docs/guides/text
- https://developers.openai.com/api/reference/resources/responses
- https://developers.openai.com/api/reference/resources/responses/methods/create
- https://developers.openai.com/api/docs/libraries?language=csharp
- https://developers.openai.com/api/docs/models/gpt-oss-120b
- https://developers.openai.com/api/reference/resources/chat
- https://developers.openai.com/api/docs/guides/reasoning
