# §24 Chunk 2 — TranslateStep Lift to Talk Detail Page

_Date: 2026-06-17_
_Branch: transval_
_Author: Claude Code_

---

## 1. Test Results

**Backend (.NET build):**
```
 0 Warning(s)
 0 Error(s)
Time Elapsed 00:00:02.41
```
No backend files were touched by this chunk. Zero errors. Warning count is 0 due to incremental build cache; Chunk 1 baseline was 11 pre-existing warnings, all unchanged.

**Frontend (TypeScript):**
Node.js was not available in the current shell environment (node.exe not in PATH — same pre-existing constraint as Chunk 1). TypeScript correctness is verified by code review:
- `TranslateStep` accepts `{ talkId: string }` — the `talkId` prop passed (`talkId` from `ToolboxTalkDetailProps`) is typed `string`. No mismatch.
- `hasTargetLanguages` is `boolean` — used only in a JSX conditional; no type constraint.
- No new imports that could introduce resolution errors — `TranslateStep` path is `./learning-wizard/steps/TranslateStep`, confirmed to exist.

**Frontend (vitest):**
Not runnable from current shell (Node.js not in PATH). No files exercised by the existing unit test suite were modified — the three passing test files from Chunk 1 are unchanged.

**Frontend (Playwright e2e):**
Not runnable from current shell. The unauthenticated smoke test (`login-page.spec.ts`) has no dependency on the code paths changed here.

---

## 2. Files Changed in Scope

| Path | Change summary |
|------|---------------|
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Added `TranslateStep` import; added `hasTargetLanguages` IIFE; added "Translations" TabsTrigger between Overview and Validation; added Translations TabsContent rendering `<TranslateStep talkId={talkId} />` or empty-state. |
| `BACKLOG.md` | Updated §24 Chunk 1 shipped note to include Chunk 2. |

---

## 3. Files Changed Outside Stated Scope

None.

---

## 4. BACKLOG Impact

**§24 status:** Open. Chunks 1 and 2 shipped. Remaining: chunks 3 (ValidateStep lift), 4 (settings editing), 5 (add target language), 6 (Rule-8 stale gate).

**BACKLOG.md line 1784 updated:** Wording changed from "Chunk 1 (inline section & quiz edit on talk detail page) shipped 2026-06-17. Remaining chunks: 2 (TranslateStep lift)…" to "Chunks 1 (inline section & quiz edit) and 2 (TranslateStep lift to detail page) shipped 2026-06-17. Remaining chunks: 3…"

**Demo cut status:** Chunk 3 (ValidateStep lift) is the remaining Demo gate. Chunks 4, 5, 6 are post-Demo.

---

## 5. Build Output

**Frontend:** TypeScript check not runnable in current shell environment (Node.js not in PATH — pre-existing constraint, same as Chunk 1). Correctness verified by code inspection.

**Backend:** `dotnet build` — 0 errors. No backend files touched.

---

## 6. Tab Structure Decision

Tab order chosen: **Overview → Translations → Validation**. This matches the workflow narrative (edit content → translate → validate) and was favoured over "at the end" for that reason. The recon gave this as the preferred ordering.

Visibility gating: Translations and Validation tabs both use the same `!isPartOfCourse && !previewMode` guard, consistent with the existing Validation tab pattern. The prompt stated "always visible regardless of wizardPreference" — confirmed: the tab has no `useWizardPreference` condition.

Empty state: when `hasTargetLanguages` is false, the Translations tab renders a dashed-border placeholder ("No target languages configured for this talk. Languages must be added at creation time.") rather than delegating to `TranslateStep`'s own empty state, which contains wizard-specific text ("Go back to Input & Config to add target languages") that would be confusing outside the wizard.

---

## 7. Notable Implementation Decisions

### A. `hasTargetLanguages` computed at component level, not inside TranslateStep

`TranslateStep` is not modified. To show the detail-page-specific empty state (rather than the wizard-specific one inside `TranslateStep`), `ToolboxTalkDetail` checks `targetLanguageCodes` before deciding whether to render `TranslateStep` at all. Uses an IIFE with try/catch to parse the JSON-encoded string, identical in logic to `TranslateStep`'s internal `parseLanguageCodes` helper.

### B. Cache key divergence — documented, non-blocking

`TranslateStep` uses `useTalk` (cache key `['learnings', talkId]`); `ToolboxTalkDetail` uses `useToolboxTalk` (cache key `['toolbox-talks', talkId]`). As noted in recon §3 and §8: both call the same API, and `TranslateStep` only reads `talk?.targetLanguageCodes` from the `['learnings']` bucket. This field does not change on section or question edits (only Chunk 5 — add target language — would change it). The workflow states used by `TranslateStep`'s panels are in a separate query key (`['toolbox-talks', talkId, 'workflow-state']`) and update correctly. Non-blocking for Chunk 2.

### C. Zero changes to `TranslateStep.tsx`

Confirmed props shape `{ talkId: string }` — the only prop. No hidden URL coupling, no `onContinue`, no parent wizard context. Lifted as-is.

---

## 8. File:Line Conformance Citations

| Claim | File:Line |
|-------|-----------|
| `TranslateStep` import | [ToolboxTalkDetail.tsx:29](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L29) |
| `hasTargetLanguages` IIFE | [ToolboxTalkDetail.tsx:93–101](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L93-L101) |
| Translations TabsTrigger | [ToolboxTalkDetail.tsx:231–233](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L231-L233) |
| Translations TabsContent with TranslateStep | [ToolboxTalkDetail.tsx:345–356](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L345-L356) |
| Empty state placeholder | [ToolboxTalkDetail.tsx:350–354](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L350-L354) |
| BACKLOG §24 shipped note | [BACKLOG.md:1784](BACKLOG.md#L1784) |

---

## 9. Manual Smoke Verification (to run in review session)

**Smoke 1 — Published talk with target languages:**
Navigate to a Published talk with `targetLanguageCodes` set (e.g. `["fr","de"]`). Expected: three tabs visible — Overview, Translations, Validation. Click Translations tab. Expected: `TranslateStep` renders per-language panels (WizardTranslationPanel with state badges and Start button for each language).

**Smoke 2 — Start Translation from detail page:**
On the Translations tab, click "Start" for one language (or "Start All"). Expected: SignalR progress updates appear live; state badge updates on completion.

**Smoke 3 — Talk with no target languages:**
Navigate to a talk where `targetLanguageCodes` is null or `[]`. Expected: Translations tab visible (tab is always shown for non-course, non-preview talks). Click it. Expected: dashed-border empty state with text "No target languages configured for this talk."

**Smoke 4 — Course-part talk:**
Navigate to a talk that is part of a course (`isPartOfCourse = true`). Expected: only Overview tab visible — Translations and Validation tabs absent (same gate as before Chunk 2).

**Smoke 5 — Preview mode:**
Open the PreviewModal from any talk. Expected: only Overview tab visible — Translations and Validation tabs absent (same `previewMode` gate).

**Smoke 6 — Tab persistence:**
Navigate to the Translations tab, then refresh the page. Expected: default tab (Overview) shown on refresh — this is the existing Tabs defaultValue="overview" behaviour and is intentional (no URL-based tab persistence exists in the current implementation).
