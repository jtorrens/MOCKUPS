# Codex workflow

## Source of truth

- This repository is the source of truth.
- Accepted architecture decisions live in `docs/architecture/05_decisions_log.md`.
- Current project state and next work live in `PROJECT_STATUS.md`.
- Exchange tasks from ChatGPT to Codex live in `docs/exchange/` (currently organized under `docs/exchange/tasks/`).
- Codex responses, summaries, and handoffs may live in `docs/exchange/codex_responses/`.

## Task rules

- Keep each Codex task small and scoped.
- Follow accepted decisions and update documentation when explicitly requested.
- After each task, summarize changed files and update `PROJECT_STATUS.md`.
- Do not silently alter architecture. If implementation reveals a conflict, stop the conflicting work and create an Architecture Question for human resolution.

```text
Codex may implement, adapt and detect conflicts.
Codex must not silently change architecture.
If architecture conflicts appear, Codex should stop and create an Architecture Question.
```

An Architecture Question should identify the conflicting decision or requirement, explain the implementation impact, and list viable options without choosing a new architecture implicitly.

## Recommended response format

```md
## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```
