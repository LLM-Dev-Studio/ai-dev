# Design Blueprint: Local Functionality Core

**Date**: 2026-04-22
**Author**: GitHub Copilot
**Status**: Proposed

---

## 1. Decision

Yes: add a dedicated project as a local functionality core.

Rationale:
1. It gives a stable boundary for model-agnostic orchestration (progressive discovery, planning, compaction, memory).
2. It allows ADS behavior to diverge by model/executor type via strategy selection rather than branching inside feature services.
3. It keeps existing app, executor, and UI projects from owning orchestration policy details.

---

## 2. Proposed Project

- Project: `ai-dev.core.local`
- Purpose: host contracts and small pure orchestration primitives for local-first agent capabilities.
- Style: functional boundaries (`Result<T>`, immutable records, small interfaces, no mutable DTO workflow).

### 2.1 Responsibility Split

`ai-dev.core.local` should own:
1. Objective and planning contracts.
2. Progressive discovery contracts (search -> read slices -> synthesize).
3. Context compaction contracts and policies.
4. Runtime memory contracts (working, task, repo facts).
5. Model strategy resolution for capability divergence.

`ai-dev.core` should continue to own:
1. Domain entities and value types used by the application.
2. Existing feature services and domain event dispatch.
3. Filesystem/project mutation coordination.

Executors (`ai-dev.executor.*`) should own:
1. Model-specific invocation details.
2. Provider transport and retries.
3. Capability registration metadata.

---

## 3. Functional Architecture

```text
Objective -> Planner -> Next Action
                  |
                  v
            Discovery Engine ----> Tool Broker
                  |                     |
                  v                     v
            Compactor <--------- Transcript/Observations
                  |
                  v
             Memory Store
```

Loop contract:
1. Read objective and current runtime state.
2. Plan one bounded next action.
3. Execute tools for that action.
4. Compact transcript to fit context budget.
5. Persist facts and continue until success/blocked.

---

## 4. Capability Divergence by Model Type

Use a model strategy resolver so behavior can differ without leaking provider logic.

Example strategy dimensions:
1. Planning depth (small local model vs larger local model).
2. Discovery breadth and retries.
3. Compaction aggressiveness and citation strictness.
4. Tool parallelism limits.

This keeps ADS evolution explicit and testable.

---

## 5. Progressive Discovery Contract

Minimum phases:
1. Candidate discovery (paths/symbols).
2. Targeted slice reads.
3. Evidence synthesis with citations.
4. Confidence + next-step recommendation.

Hard rule: avoid full-file reads unless a confidence threshold cannot be reached by slices.

---

## 6. Auto-Compaction Contract

Compaction policy should keep:
1. Open decisions.
2. Last successful and failed attempts.
3. Stable repo facts and references.
4. Unresolved errors with direct evidence.

Compaction policy should drop:
1. Redundant command output.
2. Superseded hypotheses.
3. Repeated observations.

---

## 7. Rollout Plan

Phase 1:
1. Add `ai-dev.core.local` contracts.
2. Add planner/discovery/compactor test doubles in unit tests.
3. Add one orchestrator integration path behind a feature flag.

Phase 2:
1. Move existing local-agent runtime decisions behind contracts.
2. Add model strategy resolver and capability matrix.
3. Add deterministic regression tests for discovery + compaction.

Phase 3:
1. Introduce bounded sub-agent roles (planner, researcher, coder) driven by local models.
2. Add telemetry and quality metrics (success rate, tool-call budget, compaction ratio).
3. Tune policies per model profile.

---

## 8. Acceptance Criteria

1. A single objective can complete through planner->tools->compaction loop using local models only.
2. Context size remains under configured budget across iterative steps.
3. Model-type strategy changes behavior without changing feature-service code.
4. Failures are expressed as typed `DomainError` values and surfaced with evidence.
