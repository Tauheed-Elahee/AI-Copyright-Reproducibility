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
├── example.project/        template — copy this to start a new study
├── src/                    C# source + project file
├── scripts/
│   ├── build/              build.sh / build.bat
│   ├── run/                run.sh   / run.bat
│   └── view/               status.sh / status.bat
├── .github/workflows/      release.yml — publishes binaries on version tag
├── docs/                   GitHub Pages
└── *.md                    documentation
```

## Download

Download the binary for your platform from [GitHub Releases](../../releases).  
Also download `example.project.tar.gz` to use as a starting template.

## Project directory

The harness operates on a **project directory** — a self-contained folder with this layout:

```
my-study.project/
├── config.json             location manifest
├── config/
│   ├── experiment.json     run settings
│   ├── deployments.json    deployment arms
│   └── secrets.json        API keys (never commit this — it is gitignored)
├── input/
│   ├── text.json           text library
│   ├── queries.json        query templates
│   └── prompts.json        prompt bindings
├── output/                 run output (created automatically)
└── log/                    run logs (created automatically)
```

Copy `example.project/` to start a new study, then edit the files inside it.

## Configure

Edit `config/experiment.json` to set iteration counts, timing, seeds, and parallelism.  
Edit `config/deployments.json` to add, remove, or adjust deployment arms.  
Edit files in `input/` to change the corpus, query templates, or prompt bindings.  
Edit `config.json` only to change directory locations.  
Copy `config/secrets.template.json` → `config/secrets.json` and fill in your API keys.

## Run

```bash
harness <project-dir>   # explicit path
harness                 # uses current directory if it contains config.json
```

Output lands in `<project-dir>/output/<timestamp>/`.

## Prerequisites (Azure)

1. `az login` once, signed in as an identity with the **Cognitive Services User** role on the
   target Azure AI Foundry resource. A 401/403 on the first call almost always means the role
   assignment is missing, not a code error.
2. Network access for initial Azure token fetch.

## Build from source

Requires .NET SDK 8.0+.

```bash
dotnet run --project src/ -- <project-dir>
```

Or build a self-contained binary for your platform:

```bash
dotnet publish src/ -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:AssemblyName=harness -o dist/
./dist/harness <project-dir>
```

## Scripts

All scripts must be run from the **repo root**. Each requires a `<project-dir>` argument and exits with an error if omitted.

### Build

Restores NuGet packages and compiles the project. Does not run.

```bash
bash scripts/build/build.sh
scripts\build\build.bat          # Windows
```

### Run

Restores, builds, and runs the harness against the given project directory.

```bash
bash scripts/run/run.sh <project-dir>
scripts\run\run.bat <project-dir>   # Windows
```

### Status monitor

Reads the latest log file from `<project-dir>/log/` and prints a live summary: last run line, progress bar, sleep status, identity groups, and error count. Set `HARNESS_LOG` to point at a specific log file instead.

```bash
bash scripts/view/status.sh <project-dir>
scripts\view\status.bat <project-dir>   # Windows

# Live refresh (Linux/macOS):
watch -n 2 -c bash scripts/view/status.sh <project-dir>
```

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
