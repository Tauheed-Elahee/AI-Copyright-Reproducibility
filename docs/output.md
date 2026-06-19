---
layout: default
title: Output
nav_order: 6
has_children: true
---

# Output

Every run produces a timestamped directory under `<project-dir>/output/`. The files it contains serve different purposes — from raw forensic evidence to aggregated summary statistics.

- **[File overview](output/file-overview)** — directory structure, file types, and log file locations
- **[manifest.csv columns](output/manifest-columns)** — full column reference for the per-request output file
- **[Summary CSVs](output/summary-csvs)** — `summary_counts.csv` and `summary_pct.csv` structure and the "perfect run" definition
- **[Scoring](output/scoring)** — how list task metrics (`exact_matches`, `coverage`, `hallucinations`, `position_score`, `min_moves`) are computed
- **[Interpreting results](output/interpreting-results)** — how to read the numbers as research evidence: comparing deployments, identity groups, and drawing reproducibility conclusions
