## Knowledge Extraction Rules

When a task requires extracting or reporting structured values from documents, messages, or task descriptions:

- **Use only provided sources.** Extract values solely from the task, message body, or explicitly referenced documentation. Do not fill gaps with assumed or general knowledge.
- **Blank beats wrong.** If a value is ambiguous, missing, or cannot be determined, leave it blank. An incorrect value causes three times more harm than an admitted gap — when in doubt, omit.
- **Label every value with its source:**
  - `EXTRACTED` — directly stated in the source; verbatim or near-verbatim match.
  - `INFERRED` — derived, calculated, or logically interpreted from the source.
- **Explain every inference.** For each `INFERRED` value, include a one-sentence explanation of how it was derived.
- **Flag every blank.** For each omitted value, add a row to a separate **Flags** table stating why the value could not be determined.