---
layout: default
title: Method
parent: Report
nav_order: 1
---

# Method

The automated test harness ran six deployments across three API surfaces on June 17, 2026. No instructions, tools, retrieval, or memory were attached to any deployment.

## Deployments

**Table 1 — Test Deployments**

| Label | Model | API surface |
|---|---|---|
| `deepseek-model` | DeepSeek-V4-Pro | Azure Chat/Completions |
| `deepseek-agent` | DeepSeek-V4-Pro | Azure Agent / Responses API |
| `deepseek-native` | DeepSeek-V4-Pro | Native DeepSeek API (temp. 0) |
| `deepseek-native-t1` | DeepSeek-V4-Pro | Native DeepSeek API (temp. 1, logprob arm) |
| `gpt-oss-model` | GPT-OSS-120b | Azure Chat/Completions |
| `gpt-oss-agent` | GPT-OSS-120b | Azure Agent / Responses API |

## Run protocol

Run protocol: 20 sets × 10 repetitions = 200 runs per (deployment × text × query type). Deployments, repetitions, and prompts were parallelised up to a concurrency limit of 6. Sets were separated by a 60 s pause and repetitions by a 5 s pause; each request was retried up to three times on failure (1 s to 60 s backoff). Shuffle seeds: content 42, prompt order 99, deployment order 20.

## Query types

The harness ran eleven query types across three texts. Query types fall into four task groups: generative recall (given a title, produce the document structure); source identification from an ordered input (given a TOC or section list, name the source); source identification from a shuffled input (same task with ordering cues removed); and sequence reconstruction (given shuffled headings, restore the correct order). Results are reported in [Comparison](comparison).

**Table 2 — Query Types**

| Label | Input | Task | Metric | Text |
|---|---|---|---|---|
| *Generative recall* | | | | |
| `title_to_toc` | Full book title | Return TOC as bulleted list | Coverage | Toronto Notes, Bates 13e |
| `title_to_sections` | Chapter title | Return section headings as list | Coverage | Bates Ch.11 |
| *Source identification — ordered input* | | | | |
| `toc_to_title` | Full TOC (title withheld) | Name the work | Title hit% | Toronto Notes |
| `toc_to_title_us` | Full TOC (title withheld) | Name the work | Title hit% | Bates 13e |
| `sections_to_title_us` | All section headings (title withheld) | Name chapter and textbook | Title, textbook hit% | Bates Ch.11 |
| *Source identification — shuffled input* | | | | |
| `shuffled_toc_to_title` | Shuffled TOC (title withheld) | Name the work | Title hit% | Toronto Notes |
| `shuffled_toc_to_title_us` | Shuffled TOC (title withheld) | Name the work | Title hit% | Bates 13e |
| `shuffled_sections_to_title_us` | Shuffled section headings (title withheld) | Name chapter and textbook | Title, textbook hit% | Bates Ch.11 |
| *Sequence reconstruction* | | | | |
| `shuffled_toc_to_order` | Shuffled TOC | Restore correct order | Perfect, order acc. | Toronto Notes |
| `shuffled_toc_to_order_us` | Shuffled TOC | Restore correct order | Perfect, order acc. | Bates 13e |
| `shuffled_sections_to_order_us` | Shuffled section headings | Restore correct order | Perfect, order acc. | Bates Ch.11 |

*Toronto Notes = Toronto Notes 2022, 38th Edition; Bates 13e = Bates' Guide to Physical Examination and History Taking, 13th Edition; Bates Ch.11 = Chapter 11 of Bates' Guide 13e.*

## Deployment parameters

Parameters were configured to maximize deterministic recall. Temperature 0 and top-p 0.01 minimize sampling variability. The key distinction across surfaces is that the Agent deployment embeds inference parameters in the agent definition at creation time; they cannot be overridden per request by the caller. The Model deployment accepts parameters per request via the API.

**Table 3 — Azure DeepSeek parameters (key sampling settings)**

| Parameter | `deepseek-model` | `deepseek-agent` | Settable by |
|---|---|---|---|
| Temperature | `0` | `0` (agent-definition) | Operator (both surfaces) |
| Top-p | `0.01` | `0.01` (agent-definition) | Operator (both surfaces) |
| Seed | `7_294_853_106_482_917` | — | Model surface only (silently ignored by DeepSeek) |
| Max tokens | `16_384` | `16_384` | Operator (both surfaces) |
| Reasoning effort | none | none | Operator (both surfaces) |
| Presence penalty | `0` | `0` | Operator (both surfaces) |
| Frequency penalty | `0` | `0` | Operator (both surfaces) |
| Model & runtime (checkpoint, hardware, serving stack) | — | — | Microsoft |

On the Azure Agent deployment, temperature and top-p are owned by the agent definition and cannot be overridden by the caller per request. The goal was to test whether content exists in model weights under near-deterministic decoding, not how often it surfaces under default settings.

Rationale for the deployment choice: all AI scribe products we have surveyed in Ontario are delivered as cloud-based SaaS rather than on local hardware. Testing through a managed cloud endpoint therefore reflects the conditions under which such systems are actually operated in clinical settings.

**Table 4 — Azure GPT-OSS parameters (key sampling settings)**

| Parameter | `gpt-oss-model` | `gpt-oss-agent` | Settable by |
|---|---|---|---|
| Temperature | `0` | `0` (agent-definition) | Operator (both surfaces) |
| Top-p | `0.01` | `0.01` (agent-definition) | Operator (both surfaces) |
| Seed | `7_294_853_106_482_917` | — | Model surface only (silently ignored by GPT-OSS-120b) |
| Max tokens | `16_384` | `16_384` | Operator (both surfaces) |
| Reasoning effort | low | low | Operator (both surfaces) |
| Model & runtime | — | — | Microsoft |

`reasoning_effort` is set to `low` rather than `none` because GPT-OSS-120b is a reasoning model. Structure otherwise mirrors the Azure DeepSeek configuration.

**Table 5 — Native DeepSeek parameters (key sampling settings)**

| Parameter | `deepseek-native` | `deepseek-native-t1` | Settable by |
|---|---|---|---|
| Temperature | `0` | `1` | Operator (native API) |
| Top-p | `0.01` | `0.01` | Operator (native API) |
| Seed | N/A | N/A | Not in native DeepSeek API spec |
| Max tokens | `16_384` | `16_384` | Operator (native API) |
| Reasoning effort | low | low | Operator (native API) |
| Thinking mode | disabled | disabled | Operator (native API) |
| Model & runtime | — | — | DeepSeek |

The two native deployments differ only in temperature. `deepseek-native` uses temperature 0 (near-deterministic); `deepseek-native-t1` uses temperature 1 as a logprob arm to capture non-trivial token probability distributions.
