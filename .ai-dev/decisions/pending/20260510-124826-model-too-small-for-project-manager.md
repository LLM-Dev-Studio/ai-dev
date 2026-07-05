---
from: pm-standard
date: 2026-05-10T12:48:26.6718120Z
priority: high
subject: Model too small for Project Manager
status: pending
blocks: pm-standard
---

Agent **Project Manager** could not run because `qwen/qwen3.6-27b` has a context window that is too small.

Model 'qwen/qwen3.6-27b' finished (finish_reason=stop) but produced no usable output. If this is a reasoning/thinking model (e.g. Qwen3), disable thinking mode in LM Studio or load a non-thinking variant of the model.

**Action required:** In LM Studio, reload `qwen/qwen3.6-27b` with a larger context (≥ 8192 tokens), or select a different model for this agent in its settings.