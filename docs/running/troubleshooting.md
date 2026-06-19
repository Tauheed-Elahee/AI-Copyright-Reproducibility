---
layout: default
title: Troubleshooting
parent: Running
nav_order: 3
---

# Troubleshooting

## Azure authentication errors

**401 Unauthorized / 403 Forbidden** — your credential does not have access to the Azure AI Foundry resource.

1. Run `az login` to authenticate or refresh an expired token.
2. Confirm the logged-in identity has the **Cognitive Services User** role on the target Azure AI Foundry resource. Role assignment is in the Azure portal under the resource → Access control (IAM).
3. Check `az account show` to confirm you are using the correct subscription.

**Credential expired** — tokens from `az login` expire after several hours.

```bash
az account get-access-token   # check token expiry
az login                      # re-authenticate if expired
```

## Verbose logging with AICR_LOG

The `AICR_LOG` environment variable overrides the console log level set in `experiment.json`:

| Value | Behaviour |
|-------|-----------|
| `verbose` | All messages including per-request detail |
| `info` | Normal operational messages (default) |
| `warning` | Warnings and errors only |
| `error` | Errors only |

Both log files (`output/run.log` and the system log) always receive all levels regardless of this setting — only the console output is filtered.

```bash
# Linux / macOS
AICR_LOG=verbose aicr run my-study.project/

# Windows (PowerShell)
$env:AICR_LOG="verbose"; aicr run my-study.project/
```

## Common configuration mistakes

**Missing secrets.json**

`secrets.json` is gitignored and must be created manually on each machine.

```bash
cp config/secrets.template.json config/secrets.json
# fill in the key values
```

**Key name mismatch between endpoints.json and secrets.json**

The `key` value in `endpoints.json` must exactly match a key name in `secrets.json`:

```json
// endpoints.json — "key": "my_key"
"auth": { "type": "api_key", "key": "my_key", ... }

// secrets.json — must have "my_key"
{ "keys": { "my_key": "sk-..." } }
```

**Wrong mode casing in deployments.json**

Mode values are case-sensitive. The three valid values are `AzureModeApi`, `AzureAgentApi`, and `StandardOpenAI`. Any other string causes a startup error.

**Malformed JSON**

Use `jq` to validate config files before running:

```bash
jq . config/endpoints.json
jq . config/deployments.json
jq . input/queries.json
```

`jq` exits non-zero and prints the parse error location if the file is invalid.

## Rate limits and retries

**429 Too Many Requests** — the endpoint is rate-limiting your requests. `aicr` retries automatically with exponential backoff. The retry behaviour is controlled by fields in `experiment.json`:

| Field | Default | Effect |
|-------|---------|--------|
| `retry.max_attempts` | 3 | Maximum retries before the request is recorded as an error |
| `retry.initial_delay_s` | 2 | Initial backoff delay in seconds |
| `retry.max_delay_s` | 30 | Maximum backoff delay in seconds |

If you are hitting rate limits frequently, increase `retry.max_attempts` and `retry.max_delay_s`, or reduce `parallel.max_concurrency` to send fewer simultaneous requests.

## Network issues and concurrency tuning

**Intermittent timeouts or connection errors** — reduce `parallel.max_concurrency` in `experiment.json`. The default is conservative but some endpoints are sensitive to burst traffic.

**Slow runs** — if the endpoint can handle higher load, increasing `parallel.max_concurrency` reduces total wall-clock time. Start with small increments and monitor the error rate.

**Changing concurrency does not affect result validity** — each request is independent; parallelism only affects throughput, not scoring.

## Incomplete runs

If `aicr run` exits early (network error, interrupt, credential expiry), the output directory contains only the requests that completed. Re-running is safe — a new timestamped directory is created; the partial directory is not modified.

To regenerate `manifest.csv` and summary CSVs from a partially-completed `manifest.json`:

```bash
aicr generate summary <project-dir>/output/<timestamp>/
```
