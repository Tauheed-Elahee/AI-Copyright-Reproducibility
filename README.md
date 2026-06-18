# AI-Copyright Reproducibility Harness

A small console app that drives multiple LLM deployments (Azure AI Foundry and DeepSeek native
API), runs a set of prompts N times across each, and records signed, hashed, reproducible evidence
for every run. Built to support the Clinical-AI Reproducibility Annex.

## What it does

- Authenticates with Microsoft Entra ID (`DefaultAzureCredential`) for Azure deployments, or an
  API key for native DeepSeek.
- Sends fully parameterized request bodies assembled from `config/deployments.json`.
- Captures the **full raw JSON response** for every run to its own file under `output/<timestamp>/runs/`.
- Parses each response for `model`, `system_fingerprint`, `id`, `created`, token usage, and
  `finish_reason`.
- Computes a **SHA-256 of the assistant message content** (not the whole envelope) as the identity
  key — produces identity groups automatically across runs.
- Scores list tasks: exact matches, coverage, hallucinations, ordering accuracy.
- Writes `run-config.json`, `manifest.json`, `manifest.csv`, `summary_counts.csv`, `summary_pct.csv`.

## Layout

```
/
├── example.project/        template — copy this to start a new study
├── src/
│   ├── cli/                CLI entry point (Program.cs)
│   ├── core/               shared library — logic, config, executors, utils
│   │   ├── Config/         config + runtime types
│   │   ├── Executors/
│   │   │   ├── Azure/      AzureModeApi, AzureAgentApiExecutor
│   │   │   └── Standard/   StandardOpenAIExecutor
│   │   └── Utils/          HarnessUtils, ScoringUtils, HttpPolicies, Logger
│   └── viewer/             Blazor WASM results viewer (deployed to /viewer/ on GitHub Pages)
├── tests/                  xUnit test project
├── scripts/
│   ├── build/              build.sh / build.bat
│   ├── run/                run.sh   / run.bat
│   ├── test/               test.sh  / test.bat
│   ├── view/               status.sh / status.bat
│   └── viewer/             update-data.sh / update-data.bat
├── .github/workflows/      pages.yml — docs + viewer; release.yml — binaries on version tag
├── docs/                   GitHub Pages (Just the Docs theme)
└── *.md                    documentation
```

## Download

Download the binary for your platform from [GitHub Releases](../../releases).  
Also download `example.project.tar.gz` to use as a starting template.

## Project directory

The harness operates on a **project directory** — a self-contained folder with this layout:

```
my-study.project/
├── project.json            project manifest — name, author, date, version, fs layout
├── config/
│   ├── experiment.json          run settings
│   ├── deployments.json         deployment arms
│   ├── endpoints.template.json  endpoint structure template (commit this)
│   ├── endpoints.json           endpoint URLs and auth config (never commit — gitignored)
│   ├── secrets.template.json    API key names template (commit this)
│   └── secrets.json             API key values (never commit — gitignored)
├── input/
│   ├── text.json           text library
│   ├── queries.json        query templates
│   └── prompts.json        prompt bindings
├── output/                 run output (created automatically)
│   └── <timestamp>/
│       ├── runs/           raw per-request JSON responses
│       ├── manifest.json   full run record
│       ├── manifest.csv
│       ├── summary_counts.csv
│       ├── summary_pct.csv
│       └── run-config.json
└── log/                    run logs (created automatically)
```

## Configure

Edit `config/experiment.json` to set iteration counts, timing, seeds, and parallelism. Set `"log_level"` to one of `verbose`, `info` (default), `warning`, or `error` to control console verbosity.  
Edit `config/deployments.json` to add, remove, or adjust deployment arms.  
Edit files in `input/` to change the corpus, query templates, or prompt bindings.  
Edit `project.json` to update project metadata or change directory locations (`project.fs`).

Two gitignored files must be provided before running — copy from the committed templates and fill in real values:

**`config/endpoints.json`** — endpoint URLs, auth structure, and field contracts. Copy from `endpoints.template.json`:

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

**`config/secrets.json`** — API key values only. Copy from `secrets.template.json`:

```json
{
  "keys": {
    "my_key": "sk-..."
  }
}
```

`endpoints.json` and `secrets.json` travel through different channels: endpoint config is shared with anyone who needs to run the harness; key values go through a secrets manager. Azure-only deployments (using `DefaultAzureCredential`) need no `secrets.json` — there are no key values to supply.

## CLI

```
harness <command> [options]
```

| Command | Description |
|---|---|
| `harness run <dir>...` | Run an experiment from one or more project directories |
| `harness create <dir>...` | Create a new project directory with template config files |
| `harness generate summary [run-dir]` | Regenerate `manifest.csv` and summary CSVs from an existing `manifest.json` |
| `harness [project-dir]` | Shorthand for `harness run` — uses the current directory if omitted |
| `harness --help` / `-h` | Show global help |
| `harness help generate` | Show help for the `generate` subcommand |

### Run

```bash
harness run my-study.project/        # explicit path
harness run proj-a/ proj-b/ proj-c/  # multiple projects in sequence
harness                               # uses current directory if it contains project.json
```

Output lands in `<project-dir>/output/<timestamp>/`.

### Create

Scaffolds a new project directory with template config files:

```bash
harness create my-new-study.project/
```

Then:
1. Fill in `config/endpoints.json` with your endpoint URLs (copy from `endpoints.template.json`).
2. Fill in `config/secrets.json` with your API keys (copy from `secrets.template.json`).
3. Edit `config/deployments.json`, `input/text.json`, `input/queries.json`.
4. `harness run my-new-study.project/`

### Generate summary

Regenerates `manifest.csv`, `summary_counts.csv`, and `summary_pct.csv` from an existing `manifest.json` without re-running the experiment:

```bash
harness generate summary my-study.project/output/20260617-120000/
harness generate summary   # uses current directory
```

## Prerequisites (Azure)

1. `az login` once, signed in as an identity with the **Cognitive Services User** role on the
   target Azure AI Foundry resource. A 401/403 on the first call almost always means the role
   assignment is missing, not a code error.
2. Network access for initial Azure token fetch.

## Build from source

Requires .NET SDK 8.0+.

```bash
dotnet run --project src/cli/ -- run <project-dir>
```

Or build a self-contained binary for your platform:

```bash
dotnet publish src/cli/ -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:AssemblyName=harness -o dist/
./dist/harness run <project-dir>
```

## Scripts

All scripts must be run from the **repo root**. Scripts that accept a `<project-dir>` argument exit with an error if it is omitted.

### Build

Restores NuGet packages and compiles the project. Does not run.

```bash
bash scripts/build/build.sh
scripts\build\build.bat          # Windows
```

### Test

Restores and runs the xUnit test suite.

```bash
bash scripts/test/test.sh
scripts\test\test.bat            # Windows
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

## Logging

Each run writes to two log files.

**Project log** — `<project-dir>/log/harness-TIMESTAMP.log`  
Created fresh for every run. Lines are tagged by level (`[WARN]`, `[ERROR]`, `[VERBOSE]`; info
lines are untagged). Set `"log_level"` in `experiment.json` to filter console output; both log
files always receive all levels regardless of this setting.

**System log** — always-on, accumulates across all runs and all projects:
- Linux / macOS: `~/.local/share/ai-copyright-reproducibility/logs/harness-TIMESTAMP.log`
- Windows: `%LOCALAPPDATA%\ai-copyright-reproducibility\logs\harness-TIMESTAMP.log`

Each line in the system log is prefixed with a UTC timestamp
(`2026-06-17 14:23:01 Loaded config…`) for cross-run correlation. The system log path is
derived from the OS user-data directory and is not affected by the `project.fs.log` setting in
`project.json`.

## Query types

Each entry in `config/queries.json` may include a `types` array. Two type strings are recognised by the harness:

| Type string | Effect |
|---|---|
| `"list_task"` | Response is parsed for bulleted list items (`- …`); exact matches, coverage, hallucinations, and first-item accuracy are scored. |
| `"order_task"` | Requires `"list_task"`. Additionally scores position accuracy (`position_score`), minimum relocations (`min_moves`), and order percentage (`order_pct`). |

Unknown strings in `types` are ignored. Queries with an empty `types` array are treated as title-recall tasks and scored only for `title_hit` and `textbook_hit`.

## Results viewer

A Blazor WASM results viewer is hosted at
[ai-copyright-reproducibility.tauheed-elahee.com/viewer/](https://ai-copyright-reproducibility.tauheed-elahee.com/viewer/).
It loads the committed `medical-texts.project` run by default and renders:

- **Run summary** — timestamp, deployment count, total records, success/error breakdown
- **Per-deployment summary** — calls, success count, error count, average duration, average completion tokens
- **Identity groups** — records grouped by content SHA-256, showing which deployments independently produced identical output

To view a local run, use the **Load local manifest.json** button in the bottom bar and select any `manifest.json` from your `output/<timestamp>/` directory.

The viewer is a static site built in CI from `src/viewer/` and deployed to `/viewer/` alongside the Jekyll documentation. To run it locally:

```bash
dotnet run --project src/viewer/ --pathbase /viewer/
```

### Updating the default dataset

After a new harness run, replace the viewer's default manifest with:

```bash
bash scripts/viewer/update-data.sh <project-dir>/output/<timestamp>/manifest.json
scripts\viewer\update-data.bat <project-dir>\output\<timestamp>\manifest.json   # Windows
```

Then commit `src/viewer/wwwroot/data/manifest.json`. The updated data deploys on the next tagged release.

## Methodological notes

- **Why hash content, not the envelope.** The response `id` and `created` fields change on every
  call, so hashing the whole JSON would never match even for identical outputs. Hashing the
  assistant message content isolates output identity. The full envelope is still saved per run.
- **Line endings.** Content is hashed in memory as received (UTF-8), eliminating copy-paste /
  CRLF ambiguity. Strictly stronger evidence than hashing manually-saved text.
- **`system_fingerprint`** is the drift detector for the rolling-version endpoint. A change across
  runs (with config unchanged) flags a silent backend update.
