---
layout: default
title: Scripts
parent: Running
nav_order: 2
---

# Scripts

All scripts must be run from the **repo root**. Scripts that accept a `<project-dir>` argument exit with an error if it is omitted.

## Build

Restores NuGet packages and compiles the project.

```bash
bash scripts/build/build.sh
scripts\build\build.bat          # Windows
```

## Test

Restores and runs the xUnit test suite.

```bash
bash scripts/test/test.sh
scripts\test\test.bat            # Windows
```

## Run

Restores, builds, and runs `aicr` against the given project directory.

```bash
bash scripts/run/run.sh <project-dir>
scripts\run\run.bat <project-dir>   # Windows
```

## Status monitor

Reads the latest log file from `<project-dir>/log/` and prints a live summary: last run line, progress bar, sleep status, identity groups, and error count.

```bash
bash scripts/view/status.sh <project-dir>
scripts\view\status.bat <project-dir>   # Windows

# Live refresh (Linux/macOS):
watch -n 2 -c bash scripts/view/status.sh <project-dir>
```

Set `AICR_LOG` to point at a specific log file to override the automatic selection.

## Update viewer data

Replaces the viewer's default manifest with data from a new run:

```bash
bash scripts/viewer/update-data.sh <project-dir>/output/<timestamp>/manifest.json
scripts\viewer\update-data.bat <project-dir>\output\<timestamp>\manifest.json   # Windows
```

Then commit `src/viewer/wwwroot/data/manifest.json`. The updated data deploys on the next tagged release.
