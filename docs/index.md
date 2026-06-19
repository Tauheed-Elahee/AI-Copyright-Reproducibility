---
layout: default
title: Home
nav_order: 1
---

# AI Reproducibility

`aicr` is a reproducibility harness for LLM deployment testing. It drives multiple model deployments in parallel, runs a set of prompts a configurable number of times across each, and records cryptographically signed, hashed evidence for every response.

Built to support the [Clinical-AI Reproducibility Annex](https://github.com/Tauheed-Elahee/AI-Copyright-Reproducibility).

## What it does

- Authenticates with Microsoft Entra ID (`DefaultAzureCredential`) for Azure deployments, or an API key for OpenAI-compatible APIs.
- Sends fully parameterised request bodies assembled from `config/deployments.json`.
- Captures the **full raw JSON response** for every run to its own file under `output/<timestamp>/runs/`.
- Computes a **SHA-256 of the assistant message content** as the identity key — produces identity groups automatically across runs.
- Scores list tasks (exact matches, coverage, hallucinations, ordering accuracy) and title-recall tasks.
- Writes `manifest.json`, `manifest.csv`, `summary_counts.csv`, `summary_pct.csv`.

## Get started

[Download the binary](getting-started/download) and follow the [Your first project](getting-started/first-project) guide.

## Browse results

The [Results viewer](results-viewer) is hosted at [ai-copyright-reproducibility.tauheed-elahee.com/viewer/](https://ai-copyright-reproducibility.tauheed-elahee.com/viewer/).
