# §24 Chunk 5 — Add Target Language Post-Publish

**Date:** 2026-06-18
**Branch:** transval

## Problem

Target language codes could only be set at talk creation time. Once a talk was published, there was no way to add a new translation target without recreating the talk. Admins who wanted to add a new language to an already-published talk had no supported path.

## Solution

New backend endpoint `POST /api/toolbox-talks/{id}/target-languages` and a new `AddTargetLanguagePicker` UI component on the Translations tab of the talk detail page.

### Key architectural decision: Initial workflow state is implicit

The spec originally suggested writing a `TranslationWorkflowEvent` with type `Initial` when a language is added. Pre-flight reading of `TranslationWorkflowService.GetState()` revealed that `Initial = 0` is implicit — the service returns `Initial` when there are **no** `WorkflowEvent` rows for a `(talkId, languageCode)` pair. There is no `Initial` constant in `WorkflowEventTypes`. The handler therefore only updates `TargetLanguageCodes` — no event row is needed.

## Files Changed

### Backend

| File | Change |
|---|---|
| `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/AddTargetLanguage/AddTargetLanguageCommand.cs` | New command record |
| `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/AddTargetLanguage/AddTargetLanguageCommandHandler.cs` | New handler: validates code, checks duplicates, appends to JSON list, returns DTO |
| `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` | New `POST {id}/target-languages` action + `AddTargetLanguageRequest` record |

### Tests

| File | Change |
|---|---|
| `tests/QuantumBuild.Tests.Integration/ToolboxTalks/AddTargetLanguageCommandHandlerTests.cs` | 6 new integration tests |

### Frontend

| File | Change |
|---|---|
| `web/src/lib/api/toolbox-talks/toolbox-talks.ts` | `addTargetLanguage` API function |
| `web/src/lib/api/toolbox-talks/use-toolbox-talks.ts` | `useAddTargetLanguage` hook (invalidates 3 cache keys) |
| `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx` | New picker component |
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Translations tab: import + render picker, update empty-state text |

### Other

| File | Change |
|---|---|
| `BACKLOG.md` | Chunk 5 marked shipped; §24 summary updated to "all 6 chunks closed" |

## Handler Logic

```
1. Validate language code non-empty
2. Load talk by (id, tenantId) — 404 if not found
3. Load ILanguageCodeService.GetAllLanguagesAsync() — validate code against values dict
   (skip validation if lookup returns empty — defensive fallback)
4. Deserialize TargetLanguageCodes JSON (null → empty list)
5. Duplicate check — 400 if code already present
6. Append code (lowercased), re-serialize, save
7. Return ToolboxTalkDto via MapToDto
```

No WorkflowEvent row is written — Initial state is implicit in the workflow service.

## Cache Invalidation (Frontend)

`useAddTargetLanguage.onSuccess` invalidates:
- `['toolbox-talks', talkId]` — ToolboxTalkDetail sees updated `targetLanguageCodes`
- `['toolbox-talks', talkId, 'workflow-state']` — TranslateStep renders new language at Initial state
- `['learnings', talkId]` — TranslateStep's `useTalk` hook uses this separate cache key

## Controller Response Pattern

Returns `Ok(result.Data)` (DTO directly, not `Result<T>` envelope), consistent with the `UpdateSections` pattern per CLAUDE.md note 18.

## Test Results

| Suite | Before | After |
|---|---|---|
| Integration tests | 430 | **436 (6 new)** |
| Unit tests | 192 | 192 (unchanged) |
| Vitest (frontend) | 15 | 15 (unchanged) |
| Playwright E2E | 1 | 1 (unchanged) |
| TypeScript | clean | clean |
| dotnet build | 0 errors | 0 errors |
