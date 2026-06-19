---
layout: default
title: Methodology
nav_order: 8
---

# Methodology

## Why hash content, not the envelope

Every API response includes fields like `id` and `created` that change on every call, even for identical model outputs. Hashing the whole JSON response would never produce a match across runs. `aicr` instead hashes only the assistant message content, isolating output identity from response envelope noise. The full envelope is still saved per run for forensic use.

## Line ending normalisation

Content is hashed in memory as received (UTF-8), eliminating copy-paste and CRLF ambiguity. This is strictly stronger evidence than hashing manually-saved text, which may silently gain or lose line endings depending on the editor or OS.

## system_fingerprint as a drift detector

Azure AI Foundry and OpenAI return a `system_fingerprint` field on each response. When this value changes across runs with the configuration unchanged, it indicates a silent backend update — the model serving the endpoint has been swapped or patched. `aicr` records `system_fingerprint` in every manifest row so drift can be detected by comparing fingerprints across runs.

## Semantic SHA-256

In addition to the exact content hash, `aicr` computes a `semantic_sha256` over a simplified version of the response (whitespace-normalised, punctuation-stripped). This groups near-duplicate responses that differ only in formatting, allowing detection of structurally identical outputs that would not match on the exact hash.

## Identity groups

After each run, the console prints "identity groups" — responses grouped by `semantic_sha256`. A group with members from multiple deployments shows those deployments independently producing the same output. Large groups (many runs converging on one hash) are strong evidence of reproducible memorisation.
