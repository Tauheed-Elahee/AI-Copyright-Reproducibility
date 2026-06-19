---
layout: default
title: CLI reference
parent: Running
nav_order: 1
---

# CLI reference

```
aicr <command> [options]
```

| Command | Description |
|---------|-------------|
| `aicr run <dir>...` | Run an experiment from one or more project directories |
| `aicr create <dir>...` | Create a new project directory with template config files |
| `aicr generate summary [run-dir]` | Regenerate `manifest.csv` and summary CSVs from an existing `manifest.json` |
| `aicr lock <dir>...` | Lock a project to prevent further runs |
| `aicr version` | Print the harness version |
| `aicr [project-dir]` | Shorthand for `aicr run` — uses current directory if omitted |
| `aicr --help` / `-h` | Show help |

## run

```bash
aicr run my-study.project/        # explicit path
aicr run proj-a/ proj-b/ proj-c/  # multiple projects in sequence
aicr                               # uses current directory if it contains project.json
```

Output lands in `<project-dir>/output/<timestamp>/`.

## create

Scaffolds a new project directory with template config files:

```bash
aicr create my-new-study.project/
```

## generate summary

Regenerates `manifest.csv`, `summary_counts.csv`, and `summary_pct.csv` from an existing `manifest.json` without re-running the experiment:

```bash
aicr generate summary my-study.project/output/20260617-120000/
aicr generate summary   # uses current directory
```

Useful when scoring logic is updated and you want to recalculate results from existing data.

## lock

Sets `edition.read_only = true` in `project.json`, preventing future runs:

```bash
aicr lock my-study.project/
```

## Azure prerequisites {#azure-prerequisites}

For deployments using `AzureModeApi` or `AzureAgentApiExecutor`:

1. Run `az login` once, signed in as an identity with the **Cognitive Services User** role on the target Azure AI Foundry resource.
2. Ensure network access for the initial Azure token fetch.

A 401 or 403 on the first call almost always means the role assignment is missing, not a code error.
