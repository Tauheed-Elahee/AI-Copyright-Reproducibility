---
layout: default
title: Methodology
nav_order: 8
---

# Methodology

## Why hash content, not the envelope

Every API response includes fields like `id` and `created` that change on every call, even for identical model outputs. Hashing the whole JSON response would never produce a match across runs. `aicr` instead hashes only the assistant message content, isolating output identity from response envelope noise. The full envelope is still saved per run for forensic use.

> **Example.** Two calls to the same model with the same prompt return:
> ```
> "id": "chatcmpl-Aab1",  "created": 1719000001,  content: "The answer is 42."
> "id": "chatcmpl-Aab2",  "created": 1719000087,  content: "The answer is 42."
> ```
> Hashing the full JSON would produce two different hashes. Hashing only the content
> produces the same `content_sha256` on both calls, correctly identifying them as identical outputs.

## Line ending normalisation

Content is hashed in memory as received (UTF-8), eliminating copy-paste and CRLF ambiguity. This is strictly stronger evidence than hashing manually-saved text, which may silently gain or lose line endings depending on the editor or OS.

> **Example.** The same response content saved on Windows via copy-paste might gain `\r\n` line endings, producing a different hash than the UTF-8 bytes the API returned. `aicr` hashes the API response bytes directly, so the hash is stable regardless of where the file is read.

## system_fingerprint as a drift detector

Azure AI Foundry and OpenAI return a [system_fingerprint](/glossary/#system-fingerprint) field on each response. When this value changes across runs with the configuration unchanged, it indicates a silent backend update — the model serving the endpoint has been swapped or patched. `aicr` records `system_fingerprint` in every manifest row so drift can be detected by comparing fingerprints across runs.

> **Example.** Run 1 (Monday): `system_fingerprint: "fp_a1b2c3"`. Run 2 (Friday, same config): `system_fingerprint: "fp_d4e5f6"`. The changed fingerprint flags a backend update between the two runs — any output differences cannot be attributed purely to model stochasticity.

## Semantic SHA-256

In addition to the exact content hash, `aicr` computes a [semantic_sha256](/glossary/#semantic-sha256) over a simplified version of the response (whitespace-normalised, punctuation-stripped). This groups near-duplicate responses that differ only in formatting, allowing detection of structurally identical outputs that would not match on the exact hash.

## Identity groups

After each run, the console prints "[identity groups](/glossary/#identity-group)" — responses grouped by [semantic_sha256](/glossary/#semantic-sha256). A group with members from multiple deployments shows those deployments independently producing the same output. Large groups (many runs converging on one hash) are strong evidence of reproducible memorisation.

> **Example.** A study runs 3 deployments × 5 repetitions (15 total calls) against one text. The identity groups output shows:
> ```
> Group a1b2c3 (13 members): gpt-4o-mini ×5, gpt-4o ×5, claude-sonnet ×3
> Group d4e5f6  (2 members): claude-sonnet ×2
> ```
> Thirteen of fifteen calls — across three independent deployments — produced semantically identical output. This is strong evidence that the specific content is memorised rather than generated.
