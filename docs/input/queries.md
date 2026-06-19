---
layout: default
title: Queries & types
parent: Input
nav_order: 2
---

# Queries (`input/queries.json`)

An array of query templates. Each defines a system message, a user prompt, and the task type(s) used for scoring.

```json
{
  "label": "list-chapters",
  "system_message": "You are a helpful assistant.",
  "user_prompt": "List the chapters of {title}.",
  "types": ["list_task"]
}
```

| Field | Purpose |
|-------|---------|
| `label` | Unique identifier — appears in output files as `query_label` |
| `system_message` | System prompt sent to the model |
| `user_prompt` | User turn; `{title}` is substituted with the text's full title |
| `types` | Array of type strings controlling how responses are scored |

## Query types

| Type string | Scoring behaviour |
|-------------|------------------|
| `"list_task"` | Response is parsed for bulleted list items (`- …`). Scored for [exact matches](/glossary/#exact-match), [coverage](/glossary/#coverage), [hallucinations](/glossary/#hallucination), and first-item accuracy (`li1_first`). |
| `"order_task"` | Requires `"list_task"`. Additionally scores [position accuracy](/glossary/#position-score) (`position_score`), [minimum relocations](/glossary/#min-moves) (`min_moves`), and order percentage (`order_pct`). |
| *(empty array)* | [Title recall](/glossary/#title-recall) task. Scored only for `title_hit` and `textbook_hit`. |

Unknown strings in `types` are silently ignored. A query can combine `list_task` and `order_task` to test both list completeness and ordering simultaneously.
