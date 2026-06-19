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

Array of deployment "arms" — the LLM endpoints to test in parallel.

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
