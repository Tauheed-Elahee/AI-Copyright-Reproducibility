# AI-Copyright Reproducibility Harness

A small console app that drives multiple LLM deployments (Azure AI Foundry and DeepSeek native
API), runs a set of prompts N times across each, and records signed, hashed, reproducible evidence
for every run. Built to support the Clinical-AI Reproducibility Annex.

## What it does

- Authenticates with Microsoft Entra ID (`DefaultAzureCredential`) for Azure deployments, or an
  API key for native DeepSeek.
- Sends fully parameterized request bodies assembled from `config/deployments.config.json`.
- Captures the **full raw JSON response** for every run to its own file.
- Parses each response for `model`, `system_fingerprint`, `id`, `created`, token usage, and
  `finish_reason`.
- Computes a **SHA-256 of the assistant message content** (not the whole envelope) as the identity
  key — produces identity groups automatically across runs.
- Scores list tasks: exact matches, coverage, hallucinations, ordering accuracy.
- Writes `run-config.json`, `manifest.csv`, `summary_counts.csv`, `summary_pct.csv`.

## Layout

```
/
├── config/                     run configuration and data
│   ├── config.json             experiment settings (iterations, timing, seeds)
│   ├── deployments.config.json deployment arms (endpoints, parameters)
│   ├── queries.config.json     prompt templates
│   ├── prompts.config.json     text × query bindings
│   ├── text.db.json            text library (ground truth sections, aliases)
│   └── secrets.template.json  copy → secrets.json, fill in API keys
├── src/                        C# source + project file
├── docs/                       GitHub Pages
├── output/                     run output (gitignored)
└── *.md                        documentation
```

## Prerequisites

1. .NET SDK 8.0+ (`dotnet --version`).
2. `az login` once, signed in as an identity that has the **Cognitive Services User** role on the
   target Azure AI Foundry resource. A 401/403 on the first call almost always means the role
   assignment is missing, not a code error.
3. Copy `config/secrets.template.json` → `secrets.json` at the repo root and fill in your keys.
4. Network restore access for NuGet (`Azure.Identity`, `OpenAI`).

## Configure

Edit `config/config.json` to set iteration counts, timing, and seeds.  
Edit `config/deployments.config.json` to add, remove, or adjust deployment arms.

## Run

Run from the **repo root**:

```bash
dotnet run --project src/
```

Output lands in `output/<timestamp>/`.

## Query types

Each entry in `config/queries.json` may include a `types` array. Two type strings are recognised by the harness:

| Type string | Effect |
|---|---|
| `"list_task"` | Response is parsed for bulleted list items (`- …`); exact matches, coverage, hallucinations, and first-item accuracy are scored. |
| `"order_task"` | Requires `"list_task"`. Additionally scores position accuracy (`position_score`), minimum relocations (`min_moves`), and order percentage (`order_pct`). |

Unknown strings in `types` are ignored. Queries with an empty `types` array are treated as title-recall tasks and scored only for `title_hit` and `textbook_hit`.

## Methodological notes

- **Why hash content, not the envelope.** The response `id` and `created` fields change on every
  call, so hashing the whole JSON would never match even for identical outputs. Hashing the
  assistant message content isolates output identity. The full envelope is still saved per run.
- **Line endings.** Content is hashed in memory as received (UTF-8), eliminating copy-paste /
  CRLF ambiguity. Strictly stronger evidence than hashing manually-saved text.
- **`system_fingerprint`** is the drift detector for the rolling-version endpoint. A change across
  runs (with config unchanged) flags a silent backend update.
