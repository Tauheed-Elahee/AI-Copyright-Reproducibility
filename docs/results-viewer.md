---
layout: default
title: Results viewer
nav_order: 7
---

# Results viewer

A Blazor WASM results viewer is hosted at [ai-copyright-reproducibility.tauheed-elahee.com/viewer/](https://ai-copyright-reproducibility.tauheed-elahee.com/viewer/).

It loads the committed `medical-texts.project` run by default.

## What it shows

**Run summary** — timestamp, deployment count, total records, success and error counts.

**Summary tab** — two tables matching `summary_counts.csv` and `summary_pct.csv`, grouped by text × query × deployment. Scrollable horizontally for wide column sets.

**By Deployment tab** — per-deployment rollup: call count, success, perfect runs, errors, average duration, and average completion tokens. Separate counts and percentages tables.

**[Identity groups](/glossary/#identity-group)** — responses grouped by [content SHA-256](/glossary/#content-sha256), showing which deployments independently produced identical output across runs.

## Load a local manifest

Click **Load local manifest.json** in the bottom bar and select any `manifest.json` from your `output/<timestamp>/` directory. The viewer runs entirely in the browser — no data is uploaded.

## Update the default dataset

After a new `aicr run`, replace the viewer's default manifest with:

```bash
bash scripts/viewer/update-data.sh <project-dir>/output/<timestamp>/manifest.json
scripts\viewer\update-data.bat <project-dir>\output\<timestamp>\manifest.json   # Windows
```

Then commit `src/viewer/wwwroot/data/manifest.json`. The updated data deploys on the next tagged release.

## Run the viewer locally

```bash
dotnet run --project src/viewer/ --pathbase /viewer/
```
