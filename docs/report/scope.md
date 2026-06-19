---
layout: default
title: Scope and Limitations
parent: Report
nav_order: 5
---

# Scope and Limitations

- This document makes no legal claim. Near-verbatim recall demonstrates that material from the cited work was present in the model's training data; it does not, by itself, establish whether that use was or was not authorized. Any characterization of lawfulness is outside the scope of this demonstration.

- The recalled content is a table of contents — organizational headings rather than the substantive point-form content that makes up the body of the work. This demonstration is presented as evidence of memorization of material from a specific copyrighted work, not as a claim about the copyright status of the recalled headings themselves.

- All results are based on 200 runs per deployment–text–query cell from the June 17, 2026 test session. Coverage and accuracy statistics represent mean performance across those runs. The same automated harness and run protocol were applied consistently across all three texts; results are not pooled across texts.

- Parameters were configured to maximize deterministic recall (see [Method](method)). The results characterize what the model can reproduce under recall-maximizing settings, not its behaviour under default settings.

- The endpoint is operated by Microsoft as a managed service. The inference stack is not under our control and may change over time; repeatability was verified during the stated test window. The long-term reproducibility argument rests on public weight availability (see [Interpretation](interpretation)), not on the stability of this particular endpoint.

- Bit-exact identity of outputs across different providers, hardware, or inference software versions is not claimed. The claim is strong repeatability under a fixed configuration and independent re-deployability of the identical weights.

- A comparable long-term verification is not possible for proprietary models, not because hosted APIs lack parameter controls (many expose temperature and seed settings), but because their weights are not available for independent redeployment or inspection once the vendor retires the model.

- Results pertain to the specific models, versions, content, and configurations stated above and should not be generalized to other models or texts without testing.
