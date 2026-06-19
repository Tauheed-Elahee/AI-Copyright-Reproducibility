# AI-Copyright Reproducibility Harness

A small console app that drives multiple LLM deployments (Azure AI Foundry and native
OpenAI-compatible APIs), runs a set of prompts N times across each, and records signed,
hashed, reproducible evidence for every run. Built to support the Clinical-AI Reproducibility Annex.

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

## Quick start

```bash
aicr create my-study.project/
# fill in config/endpoints.json and config/secrets.json
aicr run my-study.project/
# output lands in my-study.project/output/<timestamp>/
```

For Azure deployments, run `az login` first with an identity that has the
**Cognitive Services User** role on the target Azure AI Foundry resource.

## CLI

| Command | Description |
|---|---|
| `aicr run <dir>...` | Run an experiment from one or more project directories |
| `aicr create <dir>...` | Create a new project directory with template config files |
| `aicr generate summary [run-dir]` | Regenerate `manifest.csv` and summary CSVs from an existing `manifest.json` |
| `aicr lock <dir>...` | Lock a project to prevent further runs |
| `aicr version` | Print the harness version |
| `aicr [project-dir]` | Shorthand for `aicr run` — uses the current directory if omitted |

## Documentation

Full documentation: **[ai-copyright-reproducibility.tauheed-elahee.com](https://ai-copyright-reproducibility.tauheed-elahee.com)**

Covers project layout, configuration, input files, output columns, CLI reference, scripts, logging, and query types.

## Results viewer

A Blazor WASM results viewer is hosted at
[ai-copyright-reproducibility.tauheed-elahee.com/viewer/](https://ai-copyright-reproducibility.tauheed-elahee.com/viewer/).
It loads the committed `medical-texts.project` run by default. See [`/results-viewer/`](https://ai-copyright-reproducibility.tauheed-elahee.com/results-viewer/) for documentation.

To view a local run, use the **Load local manifest.json** button in the bottom bar and select any `manifest.json` from your `output/<timestamp>/` directory.

## Methodological notes

- **Why hash content, not the envelope.** The response `id` and `created` fields change on every
  call, so hashing the whole JSON would never match even for identical outputs. Hashing the
  assistant message content isolates output identity. The full envelope is still saved per run.
- **Line endings.** Content is hashed in memory as received (UTF-8), eliminating copy-paste /
  CRLF ambiguity. Strictly stronger evidence than hashing manually-saved text.
- **`system_fingerprint`** is the drift detector for the rolling-version endpoint. A change across
  runs (with config unchanged) flags a silent backend update.
