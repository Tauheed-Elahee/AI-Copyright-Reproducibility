---
layout: default
title: Report
nav_order: 11
has_children: true
---

# Report

This section presents the full technical report documenting an automated test of training-data memorization and output repeatability across six deployments of two open-weight large language models.

---

## Summary

This report documents an automated test of training-data memorization and output repeatability across six deployments of two open-weight large language models — DeepSeek-V4-Pro and GPT-OSS-120b — accessed through managed cloud inference endpoints of the kind used to deliver commercial SaaS products. The harness ran 200 repeated queries per deployment against three medical references on June 17, 2026, with no instructions, tools, retrieval, or memory attached to any deployment.

The central finding is that DeepSeek-V4-Pro has memorized the structure of *Toronto Notes 2022* (38th Edition), a copyrighted Canadian medical reference, across all four tested DeepSeek deployments. Given only the book's title, DeepSeek returns 87–93% of published table of contents headings on average; it identifies the work from its own recalled headings in 100% of runs even with the title withheld; and it restores shuffled headings to the correct sequence in 91–97.5% of runs. This combination — generative recall, source identification, and correct ordering — constitutes strong empirical evidence of memorization from training data.

The memorization is text-specific and asymmetric across models. DeepSeek achieves near-zero generative recall of *Bates' Guide to Physical Examination and History Taking* (13th Edition, US) but still identifies it from an ordered table of contents in 100% of runs. GPT-OSS shows the complementary pattern: near-zero identification of Toronto Notes but identification of Bates' Guide in 39.5–46% of runs with no generative recall of either text. The full cross-text comparison is in [Comparison](report/comparison).

All deployments except `deepseek-agent` completed every request successfully; `deepseek-agent` had a 23.5–32.5% HTTP error rate. Long-term reproducibility of the results rests on the public availability of DeepSeek-V4-Pro model weights, which allow any researcher to independently redeploy and re-run the identical test at any future time.

---

> **Interactive charts:** The [Results Viewer](/viewer/?example=v0.9.0) reproduces all figures from this report as interactive charts. Use the **Charts** and **Overview** tabs after loading the v0.9.0 example.

---

## Pages in this section

- [Method](report/method) — Test harness, deployments, run protocol, and query types
- [Results](report/results) — Top-line findings for Toronto Notes 2022
- [Comparison](report/comparison) — Full cross-text comparison across all deployments and query types
- [Interpretation](report/interpretation) — What the results demonstrate
- [Scope and Limitations](report/scope) — Boundaries and disclaimers
- [Copyright Note](report/copyright) — Note on the copyrighted material tested
