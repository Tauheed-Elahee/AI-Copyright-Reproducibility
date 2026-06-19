---
layout: default
title: Project directory
parent: Project layout
nav_order: 1
---

# Project directory

```
my-study.project/
├── project.json                  project manifest — name, author, date, version, fs layout
├── config/
│   ├── experiment.json           run settings (iterations, timing, seeds, parallelism)
│   ├── deployments.json          deployment arms
│   ├── endpoints.template.json   endpoint structure template (commit this)
│   ├── endpoints.json            endpoint URLs and auth config (never commit — gitignored)
│   ├── secrets.template.json     API key name template (commit this)
│   └── secrets.json              API key values (never commit — gitignored)
├── input/
│   ├── text.json                 text library
│   ├── queries.json              query templates
│   └── prompts.json              prompt bindings (text × query)
├── output/                       created automatically on first run
│   └── <timestamp>/
│       ├── runs/                 raw per-request JSON responses
│       ├── manifest.json         full run record (source of truth)
│       ├── manifest.csv          flattened CSV version of manifest.json
│       ├── summary_counts.csv    aggregated counts by text × query × deployment
│       ├── summary_pct.csv       percentage-normalised summary
│       └── run-config.json       snapshot of config used for this run
└── log/                          created automatically on first run
```

## What to commit

| File | Commit? |
|------|---------|
| `project.json` | Yes |
| `config/experiment.json` | Yes |
| `config/deployments.json` | Yes |
| `config/endpoints.template.json` | Yes |
| `config/secrets.template.json` | Yes |
| `config/endpoints.json` | **No** — gitignored |
| `config/secrets.json` | **No** — gitignored |
| `input/*.json` | Yes |
| `output/` | Your choice — output files are large but reproducible |
| `log/` | Optional |

`endpoints.json` and `secrets.json` are separated so endpoint config can be shared with collaborators while key values travel through a secrets manager.
