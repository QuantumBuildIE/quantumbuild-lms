# §27 Subgroup A — Verification Recon

**Date:** 2026-06-20
**Investigator:** Claude Code (read-only)
**Branch:** transval
**Source material:** `docs/translation-flow-investigation.md` (§8 risks R3, R5, R7, R10, R11); `BACKLOG.md §27 Subgroup A`

---

## 1. One-Line Summary

All five Subgroup A items are resolved without implementation work: A1 was closed by §24 Chunk 5 (shipped 2026-06-18); A2 is a real gap but scope belongs in Subgroup B (edit-page-only, not new-wizard specific); A3, A4, and A5 are verified-no-action (current code is correct).

---

## 2. Per-Item Findings

### A1 — Add-language post-publish has no immediate translation path

**Investigation hypothesis (R3):** When a user adds a target language via `AddTargetLanguagePicker`, `TargetLanguageCodes` is updated but no translation job runs. The user must wait up to 24h for `DailyTranslationScanJob` or manually revisit the wizard's TranslateStep — which is not reachable from a published talk.

**Current code evidence:**

`AddTargetLanguageCommandHandler.cs:31–93` — confirmed: the handler only updates `TargetLanguageCodes` and saves. No job enqueue, no workflow event.

`use-toolbox-talks.ts:295–309` — `useAddTargetLanguage` `onSuccess` invalidates:
- `[TOOLBOX_TALKS_KEY, talkId]` — refreshes talk DTO
- `[TOOLBOX_TALKS_KEY, talkId, 'workflow-state']` — refreshes workflow state panel
- `['learnings', talkId]` — refreshes the `TranslateStep` talk hook

`ToolboxTalkDetail.tsx:365–382` — the translations tab on the talk detail page (present since §24 Chunk 5, shipped 2026-06-18) embeds:
1. `AddTargetLanguagePicker` — for adding languages
2. `TranslateStep talkId={talkId}` — the full wizard translate step, rendered whenever `hasTargetLanguages` is true

After `AddTargetLanguagePicker` adds a language, cache invalidation causes `TranslateStep` to re-render with the new language in `Initial` state. The user sees a "Start" button immediately and can trigger `POST /toolbox-talks/{talkId}/translations/{languageCode}/start-translation` to begin translation without waiting for the daily scan.

The "no reachable TranslateStep" concern from the investigation was valid at time of writing but was addressed by §24 Chunk 5.

**Verdict: Verified — no action needed.** §24 Chunk 5 (shipped 2026-06-18) closed this gap. The immediate translation path exists on the talk detail page's translations tab.

---

### A2 — PDF slide translations never re-translated on source change

**Investigation hypothesis (R5):** `ToolboxTalkSlideTranslation` rows skip if the language already exists (`existingSlideTranslations.ContainsKey`, `GenerateContentTranslationsCommandHandler.cs:420`). `UpdateToolboxTalkCommandHandler` stale detection doesn't include slides. Re-uploading a PDF leaves stale slide translations with no UI signal.

**Current code evidence:**

`GenerateContentTranslationsCommandHandler.cs:417–448` — confirmed skip-if-exists at line 420:
```csharp
if (slide.Translations.Any(t => t.LanguageCode == languageCode))
{
    slidesTranslated++;
    continue;
}
```

`UpdateToolboxTalkCommandHandler.cs:154–162` — confirmed: `anyStaleningChange` is the OR of `scalarStaleningChange` (title/description), `sectionStaleningChange`, `questionStaleningChange`. Slides are not in this set. When stalening change fires (lines 168–183), it sets `NeedsRevalidation = true` on `ToolboxTalkTranslation` rows and emits `MarkedStale` workflow events — but does NOT touch `ToolboxTalkSlideTranslation` rows.

**Scope assessment — is this new-wizard specific?**

The new wizard's detail-page edit flow exposes `SectionEditPanel`, `QuizEditPanel`, `SettingsEditPanel`. There is no file upload panel on the detail page (`ToolboxTalkDetail.tsx` grep confirms no PDF/video upload affordance). PDF re-upload on a published talk is only possible through the legacy edit page (`/admin/toolbox-talks/talks/[id]/edit`).

In the new wizard flow for PDF talks:
- `ContentGenerationJob.AutoGenerateTranslationsAsync` creates slide translations at parse time (Step 2)
- `TranslationValidationJob` (Step 5) covers sections/quiz/title/description — it does NOT create `ToolboxTalkSlideTranslation` rows
- Post-publication, there is no new-wizard path that re-uploads the PDF or re-parses slides

This means A2 only manifests when a user visits the legacy edit page for a new-wizard talk and re-uploads a PDF. That is an edit-page-only concern, not a new-wizard-end-to-end concern.

**Verdict: Fix needed but different shape.** The investigation's gap is real: `ToolboxTalkSlideTranslation` rows are never stale-marked and are skip-if-exists forever. However, the scope does not belong in Subgroup A (new-wizard pre-cutover). It should move to **Subgroup B** — it is an edit-page concern that affects all talks (legacy and new wizard), and is only reachable post-publish via the legacy edit page.

**Recommended fix shape (for Subgroup B implementation):**
- `UpdateToolboxTalkCommandHandler`: when a PDF re-upload is detected (track a `pdfStaleningChange` boolean triggered by `AttachmentUrl` change or `SlidesGenerated` becoming `false` then back to `true`), hard-delete all `ToolboxTalkSlideTranslation` rows for the talk. This resets the skip-if-exists guard, allowing the next `GenerateContentTranslationsCommand` invocation to re-translate.
- No migration needed — it's a delete, not a schema change.
- Estimated scope: 1 file changed (`UpdateToolboxTalkCommandHandler.cs`), ~10 lines. Half-day including test.

---

### A3 — Verify subtitle `EnglishSrtContent` always populated

**Investigation hypothesis (R7):** `SubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync` requires `job.EnglishSrtContent` (lines 607–615). Old jobs predating the field's introduction would silently fail (returns 0, logs warning, user sees no error).

**Current code evidence:**

`20260116203306_AddSubtitleProcessing.cs:28` — `EnglishSrtContent` column (`text, nullable: true`) was present in the **original** `AddSubtitleProcessing` migration (2026-01-16). There is no "before" state without this column — it was part of the table definition from day one.

`SubtitleProcessingOrchestrator.cs:221–226` — in `ProcessAsync`, the English SRT is generated and immediately assigned unconditionally:
```csharp
var englishSrt = _srtGeneratorService.GenerateSrt(
    transcriptWords, _settings.WordsPerSubtitle);

job.EnglishSrtContent = englishSrt;
```

This assignment runs before per-language translation begins. There is no code path where `ProcessAsync` completes successfully without setting `EnglishSrtContent`. The only scenario where it could be null is a job that failed before reaching this line — but a failed job has `Status != Completed`, and `TranslateMissingLanguagesAsync` only queries for `Status == Completed` jobs (line 595).

The check at lines 608–615 (`if (string.IsNullOrEmpty(job.EnglishSrtContent)) return 0`) is a defensive guard against broken data, not a scenario reachable in normal operation.

**Verdict: Verified — no action needed.** `EnglishSrtContent` has been in the schema since the feature launched and is unconditionally populated on every successful processing run. No legacy data gap exists.

---

### A4 — Translation cancellation token not propagated to HttpClient

**Investigation hypothesis (R10):** `ContentTranslationService.TranslateTextAsync` accepts a `CancellationToken` but does not pass it to `HttpClient.SendAsync()` at line 269.

**Current code evidence:**

`ContentTranslationService.cs:37–76` — `TranslateTextAsync` accepts `CancellationToken cancellationToken = default` and passes it to `CallClaudeApiAsync(prompt, cancellationToken)` at line 76.

`ContentTranslationService.cs:238–289` — `CallClaudeApiAsync` passes the token to both HTTP operations:
```csharp
// line 269
var response = await _httpClient.SendAsync(request, cancellationToken);
// line 270
var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
```

The same applies to `TranslateBatchAsync` (line 210) and `SendCustomPromptAsync` (line 143) — all route through `CallClaudeApiAsync` with the token.

The investigation was incorrect about the current codebase state. The token is correctly propagated end-to-end.

**Verdict: Verified — no action needed.** Current code correctly propagates the `CancellationToken` to `HttpClient.SendAsync`. The investigation hypothesis does not apply to the current codebase.

---

### A5 — Verify background-job overwrite guard for reviewer-accepted translations

**Investigation hypothesis (R11):** `MissingTranslationsJob`, `DailyTranslationScanJob`, `ContentGenerationJob` dispatch `GenerateContentTranslationsCommand` → `workflow.StartTranslation()`. The guard at `TranslationWorkflowService.StartTranslation:155–190` should block overwriting `Accepted`/`ReviewerAccepted` states. Verify enforcement.

**Current code evidence:**

`TranslationWorkflowService.cs:175–178` — the guard fires unconditionally when state is `Accepted` or `ReviewerAccepted` and `confirmOverwrite=false`, regardless of `TriggeredBy`:
```csharp
if ((state is TranslationWorkflowState.Accepted or TranslationWorkflowState.ReviewerAccepted) && !confirmOverwrite)
    return Result.Fail(
        $"Cannot start translation: language is in {state} state. ...",
        FailureCode.WorkflowConfirmationRequired);
```

`MissingTranslationsJob.cs:274–281` — dispatches `GenerateContentTranslationsCommand` with `TriggeredBy = TriggeredByType.System` and no `ConfirmOverwrite` (defaults to `false`):
```csharp
var command = new GenerateContentTranslationsCommand
{
    ...
    TriggeredBy = TriggeredByType.System
};
```

`GenerateContentTranslationsCommandHandler.cs:107–132` — passes `request.ConfirmOverwrite` (false) and `request.TriggeredBy` (System) to `StartTranslation`. When the guard rejects:
- `workflowGuard.Success == false`
- Logs a warning: "StartTranslation guard rejected {Language} ({Code})..."
- Adds a `LanguageTranslationResult { Success=false, ErrorMessage="Confirmation required..." }` for that language
- `continue`s to the next language — **no exception thrown, no job failure**

`DailyTranslationScanJob` → dispatches `MissingTranslationsJob` → same code path as above.

`ContentGenerationJob.AutoGenerateTranslationsAsync` also dispatches `GenerateContentTranslationsCommand` (verified pattern same as `MissingTranslationsJob`).

All three background job paths correctly hit the guard and silently skip the protected language. The guard is `TriggeredBy`-agnostic — a system-triggered job cannot override it without explicitly passing `ConfirmOverwrite=true`, which none of the background jobs do.

**Verdict: Verified — correct enforcement. No action needed.** The guard correctly protects `Accepted`/`ReviewerAccepted` translations from all background job overwrite paths. Skip behavior is silent (warning log only) — no noise to users.

---

## 3. Closing Section

### Summary table

| Item | Verdict | Action |
|------|---------|--------|
| A1 | Verified — no action needed | §24 Chunk 5 (2026-06-18) closed the gap |
| A2 | Fix needed but different shape | Move to Subgroup B; edit-page-only concern |
| A3 | Verified — no action needed | `EnglishSrtContent` always populated |
| A4 | Verified — no action needed | Token correctly propagated in current code |
| A5 | Verified — correct enforcement | Guard blocks all background job paths |

### Recommended chunk plan

**No implementation chunk is required for Subgroup A.**

A1, A3, A4, A5 are all verified-no-action. A2 is a real gap but belongs in Subgroup B.

**Wizard cutover gate status:** All five Subgroup A items are resolved. No Subgroup A item blocks the cutover toggle. The §27 Subgroup A gate for the cutover is clear.

### A2 reclassification to Subgroup B

Move A2 to Subgroup B immediately. Recommended BACKLOG update:
- Remove A2 from the Subgroup A list (or mark it `→ moved to Subgroup B`)
- Add to Subgroup B as: "**B8. Stale slide translations after PDF re-upload** (recon reclassification from A2) — `ToolboxTalkSlideTranslation` rows are skip-if-exists and not cleared when a PDF is re-uploaded. Fix: in `UpdateToolboxTalkCommandHandler`, when PDF/slide source changes, hard-delete all `ToolboxTalkSlideTranslation` rows for the talk to reset the skip guard. Estimated: half-day. Ref: `GenerateContentTranslationsCommandHandler.cs:420`; `UpdateToolboxTalkCommandHandler.cs:156-162`."

### Total estimated effort for follow-on implementation work

- **A1:** Zero — closed.
- **A2:** Half-day (when scheduled as B8, not pre-cutover).
- **A3:** Zero — verified.
- **A4:** Zero — verified.
- **A5:** Zero — verified.

**Pre-cutover implementation total: zero.** Subgroup A is clear.

### New BACKLOG entry surfaced during recon

None. All adjacent items encountered (slide staleness, `TranslationWorkflowPanel` guard) are already tracked — A2 reclassifies to B8 rather than opening a new entry.

### Subgroup B items noted (out of scope, for completeness)

Encountered during code traversal but not investigated:
- B3 (`DailyTranslationScanJob` coarser check) — confirmed real from investigation; code corroborates.
- B4 (slideshow HTML skip-if-exists partial failure) — confirmed skip pattern at `GenerateContentTranslationsCommandHandler.cs:452–462`.
- B5 (missing soft-delete filters on slide/slideshow translation entities) — not investigated here; noted from BACKLOG.
