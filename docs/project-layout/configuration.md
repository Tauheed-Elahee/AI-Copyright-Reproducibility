---
layout: default
title: Configuration files
parent: Project layout
nav_order: 2
---

# Configuration files

## project.json

Project manifest. Controls metadata and filesystem layout.

Key fields:

| Field | Purpose |
|-------|---------|
| `name`, `author`, `date`, `location` | Study metadata |
| `version.created` | `aicr` version that created the project |
| `version.compatible` | Minimum `aicr` version required to run |
| `version.last_run` | Version that last ran the project (updated automatically) |
| `edition.read_only` | Set to `true` by `aicr lock` to prevent further runs |
| `project.fs` | Filesystem layout — override default directory/file locations |

## experiment.json

Controls how the experiment executes.

| Field | Purpose |
|-------|---------|
| `iterations.set` | Number of independent sets |
| `iterations.rep` | Repetitions per (set × deployment × prompt) |
| `timing.*` | Pause delays (ms) at set, prompt, rep, and deployment levels |
| `seed.run` | Shuffle seed for run order |
| `seed.content` | Shuffle seed for content order |
| `parallel.max_concurrency` | Maximum concurrent requests |
| `parallel.deployment` / `rep` / `prompt` | Per-level parallelism flags |
| `retry.max_attempts` | Retry count for failed requests |
| `retry.initial_delay_s`, `retry.max_delay_s` | Exponential backoff bounds |
| `log_level` | Console verbosity: `verbose`, `info` (default), `warning`, `error` |

Both log files always receive all levels regardless of `log_level`.

## deployments.json

Array of [deployment arms](/glossary/#deployment-arm) — the LLM endpoints to test in parallel.

Each entry specifies:

| Field | Purpose |
|-------|---------|
| `label` | Identifier used in output files (e.g. `gpt-4o-mini`, `claude-sonnet`) |
| `mode` | Executor type: `AzureModeApi`, `AzureAgentApiExecutor`, or `StandardOpenAI` |
| `connection` | Endpoint reference and auth fields |
| `parameters` | Fully parameterised request body (model, temperature, max_tokens, etc.) |
| `unsupported_parameters` | Parameters that are ignored or rejected by this endpoint |

## endpoints.json and secrets.json

These two files are always gitignored. Copy from the committed templates and fill in real values before running.

**endpoints.json** — endpoint URLs, auth structure, and field contracts. Shared with anyone who needs to run the harness.

**secrets.json** — API key values only. Goes through a secrets manager, never shared in plain text.

Azure deployments using `DefaultAzureCredential` do not need `secrets.json` — authentication is handled by `az login`.

**endpoints.json** example:

```json
{
  "endpoints": {
    "my_endpoint": {
      "url": "https://example.com/api",
      "auth": { "type": "api_key", "key": "my_key", "header": "Authorization", "scheme": "Bearer" },
      "fields": []
    }
  }
}
```

**secrets.json** example (the key name must match the `key` value in `endpoints.json`):

```json
{
  "keys": {
    "my_key": "sk-..."
  }
}
```

## Deployment modes

The `mode` field in each `deployments.json` entry controls which executor is used.

| Mode | Use when |
|------|----------|
| `StandardOpenAI` | Any OpenAI-compatible API — OpenAI, Anthropic, Mistral, local servers |
| `AzureModeApi` | Azure OpenAI via Azure AI Foundry (ChatCompletions API) |
| `AzureAgentApi` | Azure Agent API — requires an Agent deployment, not a ChatCompletions deployment |

**`StandardOpenAI`** — authenticates via API key from `secrets.json`. The model is set through `parameters["model"]`. The endpoint URL comes from `endpoints.json`. Use this for any OpenAI-compatible provider.

**`AzureModeApi`** — authenticates via `DefaultAzureCredential` (run `az login` first). Requires a `deployment` field in the connection config pointing to the Azure deployment name. Internally wraps the same request format as `StandardOpenAI`.

**`AzureAgentApi`** — authenticates via `DefaultAzureCredential`. Uses the Azure Agent API request/response format rather than ChatCompletions. Response content is extracted from `output[].content[].text` rather than `choices[].message.content`.

Both Azure modes (`AzureModeApi` and `AzureAgentApi`) will fail at startup with a credential error if `az login` has not been run or the credential has expired ([DefaultAzureCredential](/glossary/#defaultazurecredential)). See [Troubleshooting](/running/troubleshooting/) for auth error resolution steps.
