---
layout: default
title: Results
parent: Report
nav_order: 2
---

# Results

Results across all six deployments, three texts, and eleven query types are reported in [Comparison](comparison); this page summarizes the top-line findings for the primary demonstration case.

## HTTP reliability

All deployments except `deepseek-agent` completed every request successfully across all query types. `deepseek-agent` failed 23.5–32.5% of requests depending on query type; all statistics for that deployment are computed over successful responses only.

[![Fig 1 — HTTP reliability by deployment]({{ site.baseurl }}/assets/report/fig1_reliability.png)](/viewer/charts?example=v0.9.0)

*Mean HTTP success rate per deployment across all text–query combinations.*

## Toronto Notes 2022: generative recall

Each deployment was prompted with the full title of *Toronto Notes 2022* and asked to return its table of contents as a bulleted list. All four DeepSeek deployments returned 87–93% of the published headings on average across 200 runs; both GPT-OSS deployments returned fewer than 13%.

No run across any deployment returned all headings: the perfect recall rate was 0% for all deployments and texts. The DeepSeek result constitutes near-verbatim structural recall from training weights, with no retrieval tools available; the zero perfect-recall rate indicates that the model consistently omits one or more headings per run even when mean coverage is high.

[![Fig 2 — Recall coverage by deployment]({{ site.baseurl }}/assets/report/fig2_recall_coverage.png)](/viewer/charts?example=v0.9.0)

*Mean fraction of reference headings present in output (coverage) per deployment, for Toronto Notes 2022 and Bates' Guide 13e.*

## Toronto Notes 2022: source identification and ordering

When shown the Toronto Notes table of contents with the title withheld — framed as "*[a Canadian medical reference or textbook]*" — DeepSeek identified the work correctly in 100% of runs from both ordered and shuffled input. GPT-OSS identified it in 0–0.5% of runs.

When asked to restore shuffled headings to their correct sequence, DeepSeek achieved a fully correct ordering in 91–97.5% of runs with pairwise ordering accuracy above 94%. GPT-OSS achieved zero fully correct orderings, with pairwise ordering accuracy of 38–40%.

The near-perfect ordering result confirms that DeepSeek has internalized not only the headings but their sequence.

[Comparison](comparison) extends these results to *Bates' Guide to Physical Examination and History Taking*, 13th Edition, and to chapter-level queries against Bates' Guide Ch. 11, revealing a text-specific asymmetry in what each model family has memorized.
