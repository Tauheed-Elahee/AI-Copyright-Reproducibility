---
layout: default
title: Interpretation
parent: Report
nav_order: 4
---

# Interpretation

## What the demonstration shows

The model has memorized the structure and headings of the Toronto Notes 2022 table of contents and reproduces them near-verbatim on demand, and it can name the source work from its headings alone, even with the title redacted. Coverage statistics of 87–93% are stable across all four DeepSeek deployments and consistent across 200 repeated runs per cell.

Notably, no individual run reproduced all headings: the perfect recall rate is 0% for all deployments and texts, meaning the model reliably recalls most but not every heading in each run. The variations observed — occasional heading omissions or rewording — are characteristic of recall from model weights rather than retrieval from a document: a system reading the text would not selectively omit sections or normalize spellings.

The cross-text comparison (see [Comparison](comparison)) makes this explicit: DeepSeek's near-verbatim recall of Toronto Notes does not extend to Bates' Guide, confirming that the memorization is specific to this work rather than a generic capability to reproduce any medical reference structure.

## Repeatability

The 200-run automated protocol provides robust evidence of within-window output stability. All deployments except `deepseek-agent` completed every request successfully, and coverage statistics are tight across runs under deterministic or near-deterministic decoding. The residual within-deployment variance is itself a finding: it motivates hashing and archiving outputs in any setting where AI-generated text may later need to be verified.

## The reproducibility claim, stated precisely

Outputs were repeatable, with the qualifications above, under production SaaS conditions. Long-term reproducibility is guaranteed by the public availability of the model weights rather than by any single provider: because the weights are published, the identical model can be independently redeployed on any suitable infrastructure at any future time and re-run under the same settings.

By contrast, when a proprietary hosted model is deprecated by its vendor, it can no longer be re-run by anyone, at any price.

## Why parameter access matters

This demonstration is only possible because the endpoint exposes its inference parameters and the model's weights are public. The same transparency that makes memorization of copyrighted material detectable is what makes the model auditable: detection, measurement, and remediation all depend on the model being inspectable and independently re-runnable.
