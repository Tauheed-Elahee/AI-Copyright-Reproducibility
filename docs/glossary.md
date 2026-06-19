---
layout: default
title: Glossary
nav_order: 10
---

# Glossary

Key terms used throughout the documentation.

---

## content_sha256 {#content-sha256}

SHA-256 hash of the assistant message content exactly as received from the API (UTF-8 bytes). Used as the primary identity key for a response. Two responses with the same `content_sha256` are byte-for-byte identical. See [Methodology](/methodology/).

## coverage {#coverage}

The number of unique ground truth section headings mentioned in a response. A section matched by multiple list items still counts as 1. Coverage is the primary recall metric for [list tasks](#list-task). See [Scoring](/output/scoring/).

## DefaultAzureCredential {#defaultazurecredential}

Azure SDK authentication mechanism that uses the token from `az login`. Used by `AzureModeApi` and `AzureAgentApi` deployment modes — no API key in `secrets.json` is needed for Azure deployments. See [Configuration](/project-layout/configuration/).

## deployment arm {#deployment-arm}

One LLM endpoint configured as an entry in `config/deployments.json`. Each arm is tested independently on the same inputs, allowing side-by-side comparison.

## exact match {#exact-match}

A list item extracted from a response that, after normalization and alias resolution, matches a ground truth section heading. See [Scoring](/output/scoring/).

## hallucination {#hallucination}

A list item that does not match any ground truth section heading. Computed as `list_count − exact_matches`. See [Scoring](/output/scoring/).

## identity group {#identity-group}

A set of responses that share the same [semantic_sha256](#semantic-sha256). A group containing responses from multiple independent [deployment arms](#deployment-arm) is evidence that those deployments are reproducing memorised content. See [Methodology](/methodology/) and [Interpreting results](/output/interpreting-results/).

## list task {#list-task}

A query type (marked `"list_task"` in `input/queries.json`) that asks the model to enumerate items. Responses are scored for [exact matches](#exact-match), [coverage](#coverage), [hallucinations](#hallucination), and `li1_first`. See [Queries](/input/queries/).

## min_moves {#min-moves}

The minimum number of adjacent swaps needed to bring a set of matched items into ground-truth order. Equivalent to the Kendall tau distance between the observed and expected sequences. Populated only for [order tasks](#order-task). See [Scoring](/output/scoring/).

## order task {#order-task}

A query type (marked `"order_task"` in `input/queries.json`) that asks the model to list items in a specific sequence. In addition to [list task](#list-task) scoring, responses receive [position_score](#position-score), [min_moves](#min-moves), and `order_pct`. See [Queries](/input/queries/).

## perfect run {#perfect-run}

A successful run (`status = 200`) where scoring is complete and correct for its task type: full [coverage](#coverage) with zero [hallucinations](#hallucination) (list task), [min_moves](#min-moves) `= 0` (order task), or `title_hit = 1` (title recall). See [Summary CSVs](/output/summary-csvs/).

## perf% {#perf-pct}

The percentage of successful runs that are [perfect](#perfect-run). Calculated only over `status = 200` rows. A high `perf%` across multiple [deployment arms](#deployment-arm) is a strong reproducibility signal. See [Interpreting results](/output/interpreting-results/).

## position_score {#position-score}

The count of matched items that appear at their correct ordinal position relative to the ground truth section list. Populated only for [order tasks](#order-task). See [Scoring](/output/scoring/).

## semantic_sha256 {#semantic-sha256}

SHA-256 of a semantically simplified version of the response — whitespace normalised and punctuation stripped. Groups near-duplicate responses that differ only in formatting. Used to form [identity groups](#identity-group). See [Methodology](/methodology/).

## system_fingerprint {#system-fingerprint}

An API-returned field indicating which backend version served the request. A change in `system_fingerprint` between runs (with configuration unchanged) flags a silent backend update. Recorded on every manifest row. See [Methodology](/methodology/).

## title recall {#title-recall}

The default query type (no `"list_task"` or `"order_task"` in the `types` array). Scored for `title_hit` (whether the response mentioned the text's title) and `textbook_hit` (whether it cited the source textbook). See [Queries](/input/queries/).
