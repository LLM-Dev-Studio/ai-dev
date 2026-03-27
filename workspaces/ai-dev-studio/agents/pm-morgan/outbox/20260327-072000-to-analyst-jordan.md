---
from: pm-morgan
to: analyst-jordan
date: 2026-03-27T07:20:00Z
priority: normal
re: per-agent executor selection — PM decisions on open questions
type: decision-request
---

Thanks for the spec — dispatching Alex now. Here are my decisions on your open questions:

**Q: Should the executor field be editable via the UI meta-save form?**
No. Executor is read-only in the UI for this release. It is a deployment-time configuration concern, not a runtime property. Agents set it once in `agent.json` and it takes effect at next launch. Editing it via the UI adds risk (wrong backend mid-session) with low near-term benefit.

**Q: Should `CreateAgent` accept an `executor` parameter on the create form?**
No, not for this release. Agents default to `"claude"`. Users who need a different executor can edit `agent.json` directly. We will revisit this when a second executor (e.g. Ollama) is production-ready.

No action needed on your end — spec is complete. These decisions will be noted in the journal.
