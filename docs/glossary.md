---
layout: default
title: Glossary
nav_order: 10
---

# Glossary

Key terms used throughout the documentation.

---

## agent deployment {#agent-deployment}

An Azure AI Foundry deployment surface in which inference parameters (temperature, top-p, etc.) are embedded in the agent definition at creation time and cannot be overridden per request by the caller. Contrasted with [model deployment](#model-deployment).

## content_sha256 {#content-sha256}

SHA-256 hash of the assistant message content exactly as received from the API (UTF-8 bytes). Used as the primary identity key for a response. Two responses with the same `content_sha256` are byte-for-byte identical. See [Methodology](/methodology/).

## coverage {#coverage}

The number of unique ground truth section headings mentioned in a response. A section matched by multiple list items still counts as 1. Coverage is the primary recall metric for [list tasks](#list-task). The reported figure is the mean across all successful runs for a given deployment–text–query combination. See [Scoring](/output/scoring/).

## DefaultAzureCredential {#defaultazurecredential}

Azure SDK authentication mechanism that uses the token from `az login`. Used by `AzureModeApi` and `AzureAgentApi` deployment modes — no API key in `secrets.json` is needed for Azure deployments. See [Configuration](/project-layout/configuration/).

## deployment {#deployment}

A specific combination of model, API surface, and parameter configuration. Each deployment is tested independently on the same inputs. See [deployment arm](#deployment-arm).

## deployment arm {#deployment-arm}

One LLM endpoint configured as an entry in `config/deployments.json`. Each arm is tested independently on the same inputs, allowing side-by-side comparison.

## exact match {#exact-match}

A list item extracted from a response that, after normalization and alias resolution, matches a ground truth section heading. See [Scoring](/output/scoring/).

## generative recall {#generative-recall}

The task of reproducing a document's structure (e.g. its table of contents) from a title prompt alone, without access to retrieval tools, external documents, or memory. Output is driven solely by training weights.

## hallucination {#hallucination}

A list item that does not match any ground truth section heading. Computed as `list_count − exact_matches`. See [Scoring](/output/scoring/).

## HTTP success rate {#http-success-rate}

The fraction of API requests that returned a valid model response. Requests that failed with a network or server error were retried up to three times; runs still failing after retries are excluded from metric calculations. Reported as `n_ok%`.

## identity group {#identity-group}

A set of responses that share the same [semantic_sha256](#semantic-sha256). A group containing responses from multiple independent [deployment arms](#deployment-arm) is evidence that those deployments are reproducing memorised content. See [Methodology](/methodology/) and [Interpreting results](/output/interpreting-results/).

## inference parameters {#inference-parameters}

Model generation settings supplied at request time, including temperature, top-p, seed, and maximum token count. These parameters control output sampling behaviour and, when set to near-deterministic values, reduce run-to-run variation.

## list task {#list-task}

A query type (marked `"list_task"` in `input/queries.json`) that asks the model to enumerate items. Responses are scored for [exact matches](#exact-match), [coverage](#coverage), [hallucinations](#hallucination), and `li1_first`. See [Queries](/input/queries/).

## MCCQE {#mccqe}

Medical Council of Canada Qualifying Examination. Toronto Notes 2022 is a preparatory reference for this licensing examination.

## min_moves {#min-moves}

The minimum number of adjacent swaps needed to bring a set of matched items into ground-truth order. Equivalent to the Kendall tau distance between the observed and expected sequences. Populated only for [order tasks](#order-task). See [Scoring](/output/scoring/).

## model deployment {#model-deployment}

An Azure AI Foundry deployment surface in which inference parameters can be specified per request via the API. Contrasted with [agent deployment](#agent-deployment).

## native deployment {#native-deployment}

A deployment accessed through the DeepSeek-hosted API (`api.deepseek.com`) rather than through Microsoft Azure. The two native deployments in this study differ only in temperature: `deepseek-native` uses temperature 0; `deepseek-native-t1` uses temperature 1.

## open-weight model {#open-weight-model}

A language model whose trained weight parameters are publicly released, enabling independent redeployment on any compatible infrastructure. Contrasted with proprietary hosted models, whose weights are not disclosed and cannot be independently re-run after vendor deprecation.

## order accuracy {#order-accuracy}

The fraction of ordered heading pairs (i, j) — where heading i should precede heading j — in which i actually appears before j in the model's output. A value of 100% is equivalent to a perfect ordering. This pairwise metric is robust to single-item displacements: one heading moved out of place affects only the pairs involving that heading, not all subsequent positions. See [order task](#order-task).

## order task {#order-task}

A query type (marked `"order_task"` in `input/queries.json`) that asks the model to list items in a specific sequence. In addition to [list task](#list-task) scoring, responses receive [position_score](#position-score), [min_moves](#min-moves), and `order_pct`. See [Queries](/input/queries/).

## perfect ordering {#perfect-ordering}

A sequence reconstruction run in which all ordered heading pairs appear in the correct relative order ([order accuracy](#order-accuracy) = 100%). See [perfect run](#perfect-run).

## perfect run {#perfect-run}

A successful run (`status = 200`) where scoring is complete and correct for its task type: full [coverage](#coverage) with zero [hallucinations](#hallucination) (list task), [min_moves](#min-moves) `= 0` (order task), or `title_hit = 1` (title recall). See [Summary CSVs](/output/summary-csvs/).

## perf% {#perf-pct}

The percentage of successful runs that are [perfect](#perfect-run). Calculated only over `status = 200` rows. A high `perf%` across multiple [deployment arms](#deployment-arm) is a strong reproducibility signal. See [Interpreting results](/output/interpreting-results/).

## position_score {#position-score}

The count of matched items that appear at their correct ordinal position relative to the ground truth section list. Populated only for [order tasks](#order-task). See [Scoring](/output/scoring/).

## SaaS (Software as a Service) {#saas}

A software delivery model in which the application and its underlying infrastructure are operated by a third-party provider and accessed by the end user over a network. AI scribe products in clinical settings are typically delivered as SaaS.

## semantic_sha256 {#semantic-sha256}

SHA-256 of a semantically simplified version of the response — whitespace normalised and punctuation stripped. Groups near-duplicate responses that differ only in formatting. Used to form [identity groups](#identity-group). See [Methodology](/methodology/).

## source identification {#source-identification}

The task of naming the source work (title and/or textbook) when shown its table of contents or section headings with the title withheld. Run with headings in their published order (*ordered*) and with the order randomized (*shuffled*).

## system_fingerprint {#system-fingerprint}

An API-returned field indicating which backend version served the request. A change in `system_fingerprint` between runs (with configuration unchanged) flags a silent backend update. Recorded on every manifest row. See [Methodology](/methodology/).

## temperature {#temperature}

An inference parameter that scales the logit distribution before token sampling. Lower values (approaching 0) concentrate probability on the highest-likelihood tokens, producing more deterministic output. All DeepSeek deployments except `deepseek-native-t1` were run at temperature 0.

## title recall {#title-recall}

The default query type (no `"list_task"` or `"order_task"` in the `types` array). Scored for `title_hit` (whether the response mentioned the text's title) and `textbook_hit` (whether it cited the source textbook). See [Queries](/input/queries/).

## top-p (nucleus sampling) {#top-p}

An inference parameter that restricts token sampling to the smallest set of tokens whose cumulative probability mass reaches p. Set to 0.01 for all Azure DeepSeek and GPT-OSS deployments to minimise sampling variability.

## training-data memorization {#training-data-memorization}

The phenomenon in which a language model can reproduce, verbatim or near-verbatim, content that appeared in its training corpus. Empirically detectable when the model's inference parameters are accessible and the recalled content can be compared against a known reference.
