---
layout: default
title: Input
nav_order: 4
has_children: true
---

# Input

Three files in `input/` define what to ask and who to ask it to: a text library, query templates, and prompt bindings.

- **[Texts](input/texts)** — `text.json`: the text corpus, section headings used as ground truth, and aliases for fuzzy matching
- **[Queries](input/queries)** — `queries.json`: prompt templates, system messages, and query types (`list_task`, `order_task`, title recall)
- **[Prompts](input/prompts)** — `prompts.json`: bindings between texts and queries; the cartesian product determines total request count
