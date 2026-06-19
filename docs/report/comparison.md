---
layout: default
title: Comparison
parent: Report
nav_order: 3
---

# Comparison: Canadian and American Medical References

The automated test protocol ran 200 repeated queries per deployment against two distinct medical references: *Toronto Notes 2022*, 38th Edition (a Canadian reference for the MCCQE licensing examination) and *Bates' Guide to Physical Examination and History Taking*, 13th Edition (a widely used American reference). This section compares recall quality across the two texts. All results are from the aggregated automated run (June 17, 2026; n = 200 per cell).

HTTP reliability differed sharply across deployments: only `deepseek-agent` had a substantial error rate (23.5–32.5% of requests); all other deployments, including `gpt-oss-agent`, completed every request successfully. Percentages for `deepseek-agent` are computed over successful responses only.

**Table — HTTP Reliability**

| Deployment | Toronto Notes 2022 (CA) | Bates' Guide 13e (US) |
|---|---|---|
| `deepseek-model` | 100% | 100% |
| `deepseek-agent` | ~70–76% (error rate 23.5–32.5%) | ~70–76% (error rate 23.5–32.5%) |
| `deepseek-native` | 100% | 100% |
| `deepseek-native-t1` | 100% | 100% |
| `gpt-oss-model` | 100% | 100% |
| `gpt-oss-agent` | 100% | 100% |

[![Fig 1 — HTTP reliability by deployment]({{ site.baseurl }}/assets/report/fig1_reliability.png)](/viewer/charts?example=v0.9.0)

*Mean HTTP success rate per deployment across all text–query combinations.*

---

## Generative recall (title to table of contents)

Each deployment was prompted with the full title of each work and asked to return its table of contents as a bulleted list. *Coverage* is the mean fraction of the reference headings present in the output across all successful runs; *perfect recall* (rcl\*) is the fraction of runs in which the output contained every reference heading.

**Table — Recall Coverage**

| Deployment | Toronto Notes 2022 — Coverage | Toronto Notes 2022 — Perfect | Bates' 13e — Coverage | Bates' 13e — Perfect |
|---|---|---|---|---|
| `deepseek-model` | 91.5% | 0% | 0.1% | 0% |
| `deepseek-agent` | 87.0% | 0% | 7.7% | 0% |
| `deepseek-native` | 93.0% | 0% | 0.3% | 0% |
| `deepseek-native-t1` | 89.5% | 0% | 0.2% | 0% |
| `gpt-oss-model` | 12.5% | 0% | 0.5% | 0% |
| `gpt-oss-agent` | 10.5% | 0% | 0.4% | 0% |

[![Fig 2 — Recall coverage by deployment]({{ site.baseurl }}/assets/report/fig2_recall_coverage.png)](/viewer/charts?example=v0.9.0)

*Mean coverage per deployment for each text.*

DeepSeek recovers the Toronto Notes table of contents at high coverage (87–93%) across all four DeepSeek deployments. Coverage of the Bates' Guide table of contents is near zero for the same deployments (0.1–7.7%). The asymmetry is specific to DeepSeek: both GPT-OSS deployments fail generative recall for either text. Despite high mean coverage, no run across any deployment or text achieved complete recall (perfect recall rate = 0% throughout); the model reliably returns most headings per run but consistently omits at least one.

---

## Source identification (table of contents to title)

Deployments were shown the full table of contents of each work — with the title withheld — and asked to name the source. The test was run twice: once with the TOC headings in their published order, and once with the order randomized. Queries for the Canadian text were framed as "*[a Canadian medical reference or textbook]*"; queries for the American text as "*[an American medical reference or textbook]*". The figure reported is the fraction of successful runs on which the model named the correct title.

**Table — Source Identification Rate**

| Deployment | TN 2022 — Ordered TOC | TN 2022 — Shuffled TOC | Bates' 13e — Ordered TOC | Bates' 13e — Shuffled TOC |
|---|---|---|---|---|
| `deepseek-model` | 100% | 100% | 100% | 100% |
| `deepseek-agent` | 100% | 100% | 100% | 100% |
| `deepseek-native` | 100% | 100% | 100% | 100% |
| `deepseek-native-t1` | 100% | 100% | 100% | 98% |
| `gpt-oss-model` | 0% | 0% | 39.5% | 9.5% |
| `gpt-oss-agent` | 0.5% | 1% | 46% | 39% |

[![Fig 3 — Source identification rate]({{ site.baseurl }}/assets/report/fig3_source_id.png)](/viewer/overview?example=v0.9.0)

*Fraction of runs with correct source identification, by deployment, text, and input order.*

From the ordered TOC: DeepSeek identifies both works at 100% on every deployment. GPT-OSS shows the reverse pattern from generative recall: it almost never identifies Toronto Notes (0–0.5%) but identifies Bates' Guide in 39.5–46% of successful runs.

Shuffling the TOC (removing ordering cues) has no effect on DeepSeek for either text, except for a marginal drop in `deepseek-native-t1` on Bates' (100% → 98%). GPT-OSS is more sensitive: Toronto Notes rates remain near zero on both inputs, but Bates' rates fall substantially (`gpt-oss-model`: 39.5% → 9.5%; `gpt-oss-agent`: 46% → 39%), indicating that part of GPT-OSS's recognition of Bates' Guide depends on the ordering structure of the table of contents rather than content alone.

---

## Ordering (shuffled table of contents)

Deployments were shown the headings of each table of contents in randomized order and asked to restore them to the correct sequence. *Perfect* is the fraction of runs with a fully correct ordering; *order accuracy* is the fraction of ordered heading pairs appearing in the correct relative order.

**Table — Table-of-Contents Ordering Accuracy**

| Deployment | TN 2022 — Perfect | TN 2022 — Order acc. | Bates' 13e — Perfect | Bates' 13e — Order acc. |
|---|---|---|---|---|
| `deepseek-model` | 97.5% | 99.9% | 36.0% | 95.1% |
| `deepseek-agent` | 91.3% | 99.7% | 23.6% | 93.6% |
| `deepseek-native` | 91.0% | 98.6% | 23.5% | 93.5% |
| `deepseek-native-t1` | 92.5% | 99.5% | 23.0% | 93.5% |
| `gpt-oss-model` | 0% | 39.7% | 0% | 61.2% |
| `gpt-oss-agent` | 0% | 37.5% | 0% | 65.2% |

[![Fig 4a — Ordering: % perfect]({{ site.baseurl }}/assets/report/fig4a_ordering_perfect.png)](/viewer/charts?example=v0.9.0)

*Fraction of runs with fully correct ordering per deployment.*

[![Fig 4b — Ordering: pairwise accuracy]({{ site.baseurl }}/assets/report/fig4b_ordering_accuracy.png)](/viewer/charts?example=v0.9.0)

*Mean pairwise ordering accuracy per deployment.*

DeepSeek achieves near-perfect ordering of the Toronto Notes table of contents (91–97.5% of runs fully correct) but drops substantially on Bates' Guide (23–36%). GPT-OSS achieves zero perfect orderings for either text, though its mean order accuracy is higher for Bates' Guide (61–65%) than for Toronto Notes (38–40%), consistent with the identification results above.

---

## Chapter-level results (Bates' Guide Ch. 11)

The harness also ran four query types against a single chapter of Bates' Guide 13th Edition, testing behaviour at the section-heading level within a chapter rather than the full book.

### Generative section recall

Each deployment was given the chapter title and asked to return its section headings as a list. Coverage is the mean fraction of the reference section headings present in the output.

**Table — Generative Section Recall (Bates' Guide Ch. 11)**

| Deployment | Coverage |
|---|---|
| `deepseek-model` | 45.0% |
| `deepseek-agent` | 46.1% |
| `deepseek-native` | 46.5% |
| `deepseek-native-t1` | 46.1% |
| `gpt-oss-model` | 0.0% |
| `gpt-oss-agent` | 0.1% |

All four DeepSeek deployments achieved 45–46.5% coverage — substantially higher than the near-zero book-level recall of Bates' Guide, but still well below the Toronto Notes book-level recall (87–93%). GPT-OSS remains at near-zero (0–0.1%), consistent with its book-level result.

### Chapter and textbook identification

Deployments were shown the full list of section headings for the chapter — with the chapter title withheld — and asked to name the chapter and the textbook. The test was run with section headings in their published order and with the order randomized.

**Table — Chapter/Textbook Identification (Bates' Guide Ch. 11)**

| Deployment | Ordered sections — Textbook hit | Shuffled sections — Textbook hit |
|---|---|---|
| `deepseek-model` | 100.0% | 82.8% |
| `deepseek-agent` | 100.0% | 100.0% |
| `deepseek-native` | 100.0% | 91.5% |
| `deepseek-native-t1` | 100.0% | 89.0% |
| `gpt-oss-model` | 16.0% | 0.0% |
| `gpt-oss-agent` | 5.0% | 8.0% |

*No deployment identified the specific chapter in either condition (chapter identification rate: 0% for all deployments and both input orders). Figures shown are the fraction of runs on which the model named the correct textbook.*

DeepSeek correctly identified the textbook from ordered section headings in 100% of runs; shuffled input reduces this to 83–100% depending on deployment. GPT-OSS identified the textbook in only 5–16% of runs from ordered headings, dropping to 0–8% from shuffled headings.

### Section ordering

Deployments were shown the chapter's section headings in randomized order and asked to restore the correct sequence.

**Table — Section Ordering Accuracy (Bates' Guide Ch. 11)**

| Deployment | Perfect | Order acc. |
|---|---|---|
| `deepseek-model` | 0.5% | 76.2% |
| `deepseek-agent` | 1.5% | 75.7% |
| `deepseek-native` | 0.5% | 76.8% |
| `deepseek-native-t1` | 1.0% | 76.1% |
| `gpt-oss-model` | 44.0% | 92.3% |
| `gpt-oss-agent` | 60.5% | 93.6% |

The ordering results reverse the book-level pattern. GPT-OSS achieves 44–60.5% perfect orderings with 92–94% mean order accuracy, while all four DeepSeek deployments fall below 2% perfect with 75–77% mean order accuracy. This reversal is consistent with GPT-OSS having sufficient knowledge of Bates' Guide content to order sections within a chapter — a task that requires knowing the sequence of a specific chapter's content — even though it cannot reproduce that structure generatively or reliably identify it from a full book-level TOC.

### Overview heatmaps

The following figures show all query types and deployments together for each of the three texts. Each cell reports the primary metric for that query type: coverage (%) for recall tasks, title or textbook hit (%) for identification tasks, and perfect ordering (%) or pairwise order accuracy (%) for sequencing tasks.

[![Fig 5a — Overview: Toronto Notes 2022]({{ site.baseurl }}/assets/report/fig5a_overview_tn.png)](/viewer/overview?example=v0.9.0)

*Fig 5a — All query types × all deployments for Toronto Notes 2022.*

[![Fig 5b — Overview: Bates' Guide 13e]({{ site.baseurl }}/assets/report/fig5b_overview_bates.png)](/viewer/overview?example=v0.9.0)

*Fig 5b — All query types × all deployments for Bates' Guide 13e.*

[![Fig 5c — Overview: Bates' Guide Ch. 11]({{ site.baseurl }}/assets/report/fig5c_overview_ch11.png)](/viewer/overview?example=v0.9.0)

*Fig 5c — All query types × all deployments for Bates' Guide Ch. 11.*

---

## Interpretation

The results across all three task types point to an asymmetry in training data coverage rather than a localization or spelling artifact. DeepSeek-V4-Pro shows strong memorization of the Toronto Notes structure — it can reproduce the table of contents from the title, identify the work from its headings, and correctly re-order shuffled headings — while showing near-zero generative recall of Bates' Guide despite being able to identify it. This is consistent with Toronto Notes being a frequently distributed Canadian reference well represented in Canadian web content that formed part of the model's training corpus.

GPT-OSS-120b shows the complementary pattern on identification tasks: it almost never names Toronto Notes but identifies Bates' Guide in roughly 40% of runs. This is consistent with a training corpus in which the American reference was more prominent. Neither model achieves generative recall of Bates' Guide, suggesting that recognition of a work and verbatim reproduction of its structure are dissociable and can reflect different exposure thresholds in training data.

See [Interpretation](interpretation) for the full analysis.
