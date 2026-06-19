---
layout: default
title: Prompts
parent: Input
nav_order: 3
---

# Prompts (`input/prompts.json`)

Prompt bindings link texts to queries. Each entry specifies one text and one or more queries to run against it.

```json
[
  {
    "text": "my-text",
    "queries": ["list-chapters", "recall-title"]
  }
]
```

| Field | Purpose |
|-------|---------|
| `text` | Label of an entry in `input/text.json` |
| `queries` | Array of labels from `input/queries.json` |

The harness computes the cartesian product: every `(text, query)` binding becomes a **bound prompt**, which is then executed across every deployment for every set and repetition defined in `experiment.json`.

Total requests = `texts × queries per text × deployments × sets × reps`.
