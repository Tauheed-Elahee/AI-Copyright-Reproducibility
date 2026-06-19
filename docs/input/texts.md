---
layout: default
title: Texts
parent: Input
nav_order: 1
---

# Texts (`input/text.json`)

The text library is an array of entries. Each entry defines one piece of source material to be tested.

```json
{
  "label": "my-text",
  "content": {
    "title": {
      "full": "Full Title of the Work",
      "short": "Short Title",
      "extra_fields": { "year": "2020" }
    },
    "section_headings": [
      "Introduction",
      "Methods",
      "Results",
      "Discussion"
    ],
    "aliases": ["Alt Title", "Abbreviation"]
  }
}
```

| Field | Purpose |
|-------|---------|
| `label` | Unique identifier — appears in output files as `text_label` |
| `content.title.full` | Full title string, substituted into prompts via `{title}` |
| `content.title.short` | Short title for display |
| `content.title.extra_fields` | Arbitrary metadata (year, edition, etc.) |
| `content.section_headings` | List of expected section or chapter headings — used to score list task responses |
| `content.aliases` | Alternative names used for fuzzy title matching |

`section_headings` is the ground truth for list tasks: the harness measures how many headings the model reproduces, how many are hallucinated, and whether the first item is correct.
