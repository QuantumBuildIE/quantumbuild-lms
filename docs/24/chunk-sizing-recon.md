# §24 Edit Workflow — Chunk-Sizing Recon

_Date: 2026-06-17_
_Branch: transval_
_Author: Claude Code (read-only investigation — no code changed)_

---

## Minimum Cut for Demo

**Demo needs Chunks 1 + 2 + 3 in that sequence, estimated 5–7 days total.** Chunk 1 (inline section/quiz editing on the talk detail page) is the headline feature. Chunks 2 and 3 (TranslateStep and ValidateStep lifted to the detail page) complete the post-edit journey — re-run translations, then review them — and are what makes the Demo show a credible end-to-end create → edit → translate → validate story. No backend changes are needed for Chunks 2 and 3. Chunk 1 requires one frontend session only (the full `UpdateToolboxTalkCommand` backend endpoint already handles Published-talk edits with MarkStale). Chunks 4 (settings editing), 5 (add new language), and 6 (stale banner) are not required for Demo. The toggle flip (`UseNewWizard = true`) should not happen until at least Chunks 1 + 2 + 3 are merged to `main`.

## Full §24 Plan

**Six chunks, estimated 12–15 days total.** Chunk 1: inline section/quiz edit (3 d). Chunk 2: TranslateStep lift (1–2 d). Chunk 3: ValidateStep lift (2 d). Chunk 4: settings edit panel (2 d). Chunk 5: add target language to published talk — this is the only chunk requiring a new backend endpoint (3 d). Chunk 6: stale-state warning banner (1 d, closes Design Rule 8). All six must land before the toggle flip to 100% of tenants; Chunks 1–3 + 6 must land before any individual tenant is toggled. Chunk 5 can slip to post-Demo without blocking the toggle.

---

## 1. Verification of Locked State

### Design Rules 1–7 — Status

All eight rules are present in BACKLOG §24. Rules 1–5 and 7 are stated; Rules 6 and 8 carry a TBD marker. Substantively unchanged since 2026-06-15 (git history was not checked, but code artefacts are consistent with the rules as read). No structural incompatibility found with the wizard components — do not stop.

| Rule | Text | Status |
|------|------|--------|
| 1 | Edit UI lives on talk detail page, not in wizard | Locked |
| 2 | Section/question edits on Published talks cascade MarkStale to all translations | Locked — but see §4 |
| 3 | Settings edits (quiz, refresher, cert) also cascade MarkStale | Locked — but see §4 |
| 4 | Re-translate UI lives on talk detail page (reuse TranslateStep) | Locked |
| 5 | Post-publish validation uses same accept/edit/retry UI (reuse ValidateStep) | Locked |
| 6 | External review workflow: TBD | See §5 proposal |
| 7 | Adding new target language to published talk requires dedicated backend endpoint | Locked |
| 8 | Publishing with stale translations: TBD — blocked, warned, or allowed? | See §5 proposal |

### "Chunk 8" Mystery — Resolved

BACKLOG §24 says "Recommended minimum before any rollout: chunks 1, 2, 3, and **8**." The implementation sketch defines only chunks 1–6. "8" is a cross-reference to **Design Rule 8** (the stale gate), not a chunk numbered 8. The phrase should read "chunks 1, 2, 3, and the Rule-8 stale gate." The closure notes (`docs/phase-5/closure-notes.md`) say "at minimum Chunks 1 + 3" — a narrower cut. Three authorities exist; this recon reconciles them: **Demo minimum = Chunks 1 + 2 + 3; Toggle-flip minimum = Chunks 1 + 2 + 3 + 6**.

---

## 2. Inventory of Talk Detail Page

**File:** `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` (506 lines)  
**Shell:** `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/page.tsx`

Current state is entirely read-only. There are no edit affordances except the "Edit" button that routes to the legacy edit form (`/talks/[id]/edit`).

### Existing Tabs

| Tab | Content | Edit affordance? |
|-----|---------|-----------------|
| Overview | KPI cards, Talk Details card (scalar fields), Sections accordion (plain text), Questions list | None — all read-only |
| Validation | `<ValidationHistoryTab talkId={talkId} />` | None (shows run history) |

### Translation Note (Current)

Line ~333: a plain `<Link>` saying "use the Edit page" — no TranslationWorkflowPanel, no workflow state indicators, no start-translation button. §24 Chunk 2 replaces this entirely.

### Sections Display (Important)

Sections render via `whitespace-pre-wrap` plain text — NOT the wizard's `SectionList` component. Chunk 1 will need to swap this to the editable `SectionList` behind an edit mode toggle.

---

## 3. Wizard Component Reusability Classification

### Lift As-Is (zero changes)

| Component | File | Props | Cache key used | Notes |
|-----------|------|-------|---------------|-------|
| `TranslateStep` | `learning-wizard/steps/TranslateStep.tsx` | `{ talkId }` | `['learnings', talkId]` for talk; `['toolbox-talks', talkId, 'workflow-state']` for states | Fully self-contained. Renders WizardTranslationPanel + WorkflowSubscriber. Start All button included. |
| `ValidateStep` | `learning-wizard/steps/ValidateStep.tsx` | `{ talkId }` | Same split | Fully self-contained. Language tabs, ValidationSectionCard, SendExternalReviewDialog, accept/edit/retry. |

Both components use `useTalk` (cache key `['learnings', talkId]`) for talk metadata (title, targetLanguageCodes). The detail page uses `useToolboxTalk` (`['toolbox-talks', talkId]`). These are different TanStack Query buckets for the same API call. This creates a minor divergence: section edits via Chunk 1 (which invalidate `['toolbox-talks', talkId]`) will not automatically refresh the `['learnings', talkId]` bucket used by TranslateStep and ValidateStep. **This is non-blocking**: both steps only read `talk?.targetLanguageCodes` from the talk object, which does not change on section or question edits. The workflow states (which do change after MarkStale) are fetched under `['toolbox-talks', talkId, 'workflow-state']` and will update correctly. Flag in code review; not a blocker.

### Wrap Required (props coupling)

| Component | File | Issue | Recommendation |
|-----------|------|-------|----------------|
| `ParseStep` | `learning-wizard/steps/ParseStep.tsx` | `onContinue` prop (wizard nav), uses `['learnings', talkId]` cache key, calls `useUpdateTalkSections` which is wizard-scoped | Extract the `SectionList` sub-component for rendering; write a new `SectionEditPanel` for the detail page |
| `QuizStep` | `learning-wizard/steps/QuizStep.tsx` | Same: `onContinue` prop + calls `PUT /toolbox-talks/{id}/questions` which is **Draft-only** (hard-gated at handler line 36) | Cannot reuse directly. Write a `QuizEditPanel` for the detail page using `UpdateToolboxTalkCommand` |

---

## 4. Workflow Service Surface Gaps

### Methods Available (all confirmed in `ITranslationWorkflowService.cs`)

`GetState`, `GetHistory`, `StartTranslation`, `RecordTranslationCompleted`, `StartValidation`, `RecordValidationCompleted`, `SubmitInternalReview`, `InitiateExternalReview`, `SubmitExternalReview`, `ConfirmExternalReview`, `CancelExternalReview`, `DeclineExternalReview`, `GetPortalContext`, `AcceptAsFinal`, `MarkStale`

All methods needed by Chunks 1–3 exist. No new service methods needed.

### Backend Endpoint Gaps

**Gap 1 (Chunk 1 — critical finding):** The two wizard-scoped section/question endpoints cannot be used for post-publish edits:

| Endpoint | Handler | Problem |
|----------|---------|---------|
| `PUT /toolbox-talks/{id}/sections` | `UpdateToolboxTalkSectionsCommandHandler` | No MarkStale call — designed for wizard draft phase where translations don't exist yet |
| `PUT /toolbox-talks/{id}/questions` | `UpdateToolboxTalkQuestionsCommandHandler` | Hard-gates: `if (talk.Status != Draft) return Fail(...)` at line 36. Will always 400 for Published talks |

**Resolution:** Use the existing **`PUT /toolbox-talks/{id}`** (`UpdateToolboxTalkCommandHandler`) for all Chunk 1 edits. This endpoint:
- Handles sections, questions, and scalar fields in one payload
- Already calls MarkStale on content changes (sections added/removed/edited, question text/options changed)
- Has NO status gate — works on Published talks
- Is the existing path used by the old edit form (`ToolboxTalkForm`)
- No backend changes needed for Chunk 1

**Gap 2 (Chunk 5 — backend required):** `StartTalkTranslationCommandHandler` validates `IsLanguageInTargets(talk.TargetLanguageCodes, request.LanguageCode)` at handler startup. There is no endpoint to append a new language to `TargetLanguageCodes` on a Published talk. `PUT /toolbox-talks/{id}` accepts the full payload but adding a language here would silently succeed only if the frontend sends the updated `targetLanguageCodes` array. However, `StartTalkTranslation` will still fail until `TargetLanguageCodes` is updated in DB first. A dedicated `PUT /toolbox-talks/{id}/target-languages` command is the cleanest approach — see Chunk 5 spec below.

### MarkStale Cascade — Already Working

`UpdateToolboxTalkCommandHandler.UpdateSectionsAsync` returns `staleningChange = true` on: content change, title change, section add, section remove. `UpdateQuestionsAsync` returns `staleningChange = true` on: question text change, options change, question add/remove. After `SaveChangesAsync`, the handler iterates affected language codes and calls `workflowService.MarkStale(talkId, languageCode)`. This is the path Chunk 1 must route through — and it does, if the full `PUT /toolbox-talks/{id}` is used.

---

## 5. Open Design Sub-Question Proposals

### Rule 6 — External Review from Detail Page

**Proposal: Close as resolved by Chunk 3.** `ValidateStep` already renders `SendExternalReviewDialog` and wires `useInitiateExternalReview`. Cancel external review is wired via `useCancelExternalReview` in `use-toolbox-talks.ts`. When `ValidateStep` is lifted to the detail page in Chunk 3, external review initiation and cancellation come for free — no separate chunk needed. Mark Rule 6 as resolved.

### Rule 8 — Stale Translation Gate Policy

**Proposal: Warn-only (soft-block).** When any target language is in `Stale` workflow state, show a persistent amber banner on the talk detail page: _"One or more translations are outdated — re-run translations before scheduling new assignments."_ Do not block scheduling at the API level. Rationale: blocking would fail schedule creation mid-refresh-cycle, creating operational friction. The banner gives admins full visibility; if they schedule anyway, completions will use the stale (but still valid) translated content. Hard-block at the API is a future escalation if compliance requirements demand it. This resolves Rule 8 as Chunk 6 (1 day).

---

## 6. Chunk Specifications

### Chunk 1 — Section & Quiz Inline Edit on Talk Detail Page

**Effort:** ~3 days  
**Dependencies:** None (existing `PUT /toolbox-talks/{id}` endpoint, `useUpdateToolboxTalk` hook)  
**Backend changes:** None  
**Risk:** Medium — the full-update payload must be constructed from current talk state; stale reads between fetches could cause silent overwrites. Mitigation: always refetch talk before opening edit mode.

**Scope:**

Frontend only. The Overview tab gains two collapsible edit panels:
1. `SectionEditPanel` — toggle between read-only accordion (current) and edit mode. Edit mode renders `SectionList` from `learning-wizard/components/SectionList.tsx` (reuse the component, not the hook). On save: call `useUpdateToolboxTalk({ id, data: { ...currentTalk, sections: editedSections } })`. On success: `queryClient.invalidateQueries(['toolbox-talks', talkId])`.
2. `QuizEditPanel` — similar pattern, renders quiz questions from `SectionQuestionGroup` (reuse component). On save: same full-update approach with updated questions array.

Both panels must:
- Load existing data from `useToolboxTalk` (main detail page query key)
- Disable save while `isPending`
- Show per-field validation errors
- Refetch talk on open (ensure fresh data before edit session starts)

**Files touched:**
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` — add SectionEditPanel + QuizEditPanel in Overview tab
- New: `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx`
- New: `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx`

**Files not touched:** `UpdateToolboxTalkSectionsCommandHandler.cs`, `UpdateToolboxTalkQuestionsCommandHandler.cs` (wizard-only endpoints stay wizard-only)

---

### Chunk 2 — TranslateStep Lifted to Talk Detail Page

**Effort:** ~1–2 days  
**Dependencies:** None  
**Backend changes:** None  
**Risk:** Low — component is fully props-driven; risk is only cache key divergence (documented above, non-blocking)

**Scope:**

Replace the current "use the Edit page" translation note in `ToolboxTalkDetail.tsx` with a "Translations" collapsible section (or a dedicated Translations tab for talks with `targetLanguageCodes`). Render `<TranslateStep talkId={id} />` directly. The component handles Start/Retranslate buttons, WorkflowSubscriber SignalR wiring, staggered Start All, and WizardTranslationPanel state badges out of the box.

Only show the Translations section when `talk.targetLanguageCodes` is non-null and non-empty; otherwise show a "No target languages configured" note.

**Files touched:**
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` — swap translation note for TranslateStep

**Files not touched:** `TranslateStep.tsx` (zero changes)

---

### Chunk 3 — ValidateStep Lifted to Talk Detail Page

**Effort:** ~2 days  
**Dependencies:** Chunk 2 (logically — Translate must be visible before Validate is useful)  
**Backend changes:** None  
**Risk:** Low — same props-driven reuse as Chunk 2

**Scope:**

The existing Validation tab already renders `<ValidationHistoryTab talkId={talkId} />` (run history list). For Published talks with active or recent validation runs, add a "Current Validation" panel above the history list that renders `<ValidateStep talkId={id} />`. The `ValidateStep` component already handles: language tab strip, ValidationProgressPanel, ValidationSectionCard (accept/edit/retry), SendExternalReviewDialog, and "Ready to publish" indicator.

Design decision: show ValidateStep only when at least one validation run exists for the talk (`validationRuns.length > 0`). When no runs exist, show only the history list (empty state).

This also resolves Rule 6 (external review) — `ValidateStep` includes the `SendExternalReviewDialog` and `useInitiateExternalReview` wiring already.

**Files touched:**
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` — add ValidateStep above ValidationHistoryTab in the Validation tab

**Files not touched:** `ValidateStep.tsx` (zero changes)

---

### Chunk 4 — Settings Inline Edit on Talk Detail Page

**Effort:** ~2 days  
**Dependencies:** Chunk 1 (same edit pattern)  
**Backend changes:** None  
**Risk:** Low — uses same `PUT /toolbox-talks/{id}` full-update pattern as Chunk 1

**Scope:**

Add a `SettingsEditPanel` to the Overview tab (below or replacing the existing "Talk Details" read-only card). Fields:
- Quiz settings: `requiresQuiz`, `passingScore`, `shuffleQuestions`, `shuffleOptions`, `useQuestionPool`, `allowRetry`
- Refresher: `requiresRefresher`, `refresherIntervalMonths`
- Certificate: `generateCertificate`
- Due days: `autoDueDays`

These scalar changes also cascade MarkStale via `UpdateToolboxTalkCommandHandler` (the handler checks for title/description changes; quiz-setting changes should also mark stale — verify in handler that quiz setting changes set `staleningChange = true`). If they don't, note as a minor gap (settings changes don't affect translated content directly, so not marking stale is arguably acceptable).

**Files touched:**
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx`
- New: `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`

---

### Chunk 5 — Add Target Language to Published Talk

**Effort:** ~3 days  
**Dependencies:** Chunks 2 + 3 (language must be visible in Translate/Validate panels once added)  
**Backend changes:** Yes — new command + endpoint  
**Risk:** Medium — backend state consistency (TargetLanguageCodes JSON field mutation, workflow event initialisation for the new language)

**Scope:**

Backend: new `AddTargetLanguageCommand` + `AddTargetLanguageCommandHandler`:
- Validates language is not already in `TargetLanguageCodes`
- Deserialises `TargetLanguageCodes` JSON, appends new code, re-serialises
- Creates a `TranslationWorkflowEvent` with type `Initial` for the new language (so `useWorkflowSubscription` includes it in the state list)
- Returns updated `ToolboxTalkDto`

New controller action on `ToolboxTalksController`:
```
POST /api/toolbox-talks/{id}/target-languages
Body: { languageCode: string }
Policy: Learnings.Manage
Returns: Result<ToolboxTalkDto> envelope (matches existing controller pattern)
```

Frontend: a language selector dropdown (combobox from shadcn) in the Translations section. On add: call new endpoint, invalidate `['toolbox-talks', talkId]` and `['toolbox-talks', talkId, 'workflow-state']`. The new language appears in `TranslateStep` automatically.

**Files touched (backend):**
- New: `Commands/AddTargetLanguage/AddTargetLanguageCommand.cs`
- New: `Commands/AddTargetLanguage/AddTargetLanguageCommandHandler.cs`
- `Controllers/ToolboxTalksController.cs` — new action

**Files touched (frontend):**
- New: `web/src/lib/api/toolbox-talks/` — `addTargetLanguage` function + `useAddTargetLanguage` hook
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` — language add UI in Translations section

---

### Chunk 6 — Stale Translation Banner (Design Rule 8 Gate)

**Effort:** ~1 day  
**Dependencies:** Chunk 2 (Translations section must be visible; banner lives adjacent to it)  
**Backend changes:** None  
**Risk:** Low

**Scope:**

When `useWorkflowSubscription(talkId)` returns any workflow state with `state === 'Stale'`, render an amber `Alert` banner on the Overview tab of the talk detail page:

```
⚠ One or more translations are outdated.
  Content changes have been made since the last translation run.
  Re-run translations before scheduling new assignments.
```

The banner is purely informational — it does not block scheduling, assignment, or any other action. It disappears when all languages return to a non-Stale state (on next workflow-state refetch after re-translation completes).

**Files touched:**
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` — amber Alert under the stats cards, conditional on any-stale language

---

## 7. Minimum-Shippable Cut for Demo

| Chunk | Purpose | Effort | Order |
|-------|---------|--------|-------|
| **Chunk 1** | Inline section + quiz editing | 3 d | First |
| **Chunk 2** | TranslateStep on detail page | 1–2 d | Second (or parallel with 1) |
| **Chunk 3** | ValidateStep on detail page | 2 d | Third (after 2) |
| Total | | **5–7 days** | |

Chunks 2 and 3 have no backend dependencies and can proceed independently of Chunk 1. In practice Chunk 1 should land first because it creates the edit state that makes Chunks 2–3 meaningful to Demo. Chunks 4, 5, 6 are post-Demo.

Toggle-flip minimum (for production tenants): Chunks 1 + 2 + 3 + 6.

---

## 8. Cross-Cutting Concerns

### Cache Key Split

`useTalk` (learning-wizard hooks) → `['learnings', talkId]`. `useToolboxTalk` (main app) → `['toolbox-talks', talkId]`. Both call `getToolboxTalk`. After Chunk 1 edits (via `useUpdateToolboxTalk`), the `['toolbox-talks', talkId]` bucket refreshes but `['learnings', talkId]` does not. `TranslateStep` and `ValidateStep` read `talk?.targetLanguageCodes` from the `['learnings', talkId]` bucket — stale there after Chunk 5 (add language). For Chunks 2 and 3 this is safe (targetLanguageCodes doesn't change on section edits). For Chunk 5, the add-language success handler must invalidate both buckets.

### Permission Model

Current detail page uses `usePermission('Learnings.Manage')` and `usePermission('Learnings.Schedule')`. Edit panels (Chunk 1, 4) should gate on `Learnings.Manage`. The existing "Edit" button already gates this way; edit panels should follow suit. No new permission required.

### Audit Trail

`SystemAuditLog` is not currently wired to talk edits (only auth events, user management, tenant management). Published-talk edits via `UpdateToolboxTalkCommand` are not audited. This is a pre-existing gap — §24 does not require audit logging for edits. Flag for future, not a blocker.

### Legacy Edit Form Co-existence

The old edit form (`/talks/[id]/edit`) stays in place. Users can still navigate to it via the "Edit" button. In phase, we should change the "Edit" button to route to the detail page (or remove it) and let the inline edit panels on the detail page take over. This navigation change belongs in Chunk 1 as a final step.

---

## 9. Sequencing Narrative

The logical ordering respects dependency and demo value:

1. **Chunk 1** — section/quiz editing is the core of §24 and enables MarkStale to fire, which makes Translate/Validate meaningful
2. **Chunk 2** — trivial lift of TranslateStep; immediately demonstrates that post-edit re-translation is accessible
3. **Chunk 3** — trivial lift of ValidateStep; completes the loop (edit → translate → validate) on one page
4. **Chunk 4** — settings editing; rounds out the edit surface but not Demo-critical
5. **Chunk 6** — stale banner; closes Rule 8 and gates the toggle flip
6. **Chunk 5** — add target language; only needed when a tenant needs to expand to a new market post-publish; can ship after the toggle flip

The 6-step creation wizard remains untouched throughout §24.

---

## 10. Per-Chunk Recon Scaffolding

Before beginning each chunk, a brief recon note should answer:

**Chunk 1 — Before starting, verify:**
- `useUpdateToolboxTalk` in `use-toolbox-talks.ts` sends `ToolboxTalk` payload correctly via `PUT /toolbox-talks/{id}` → confirmed (line 98 of use-toolbox-talks.ts)
- `SectionList` component props surface (accepts sections array, onChange) → confirmed from ParseStep usage
- Detail page already imports `useToolboxTalk` for the `talk` object → confirmed in ToolboxTalkDetail.tsx

**Chunk 2 — Before starting, verify:**
- `TranslateStep` import path resolves from `ToolboxTalkDetail.tsx` → `@/features/toolbox-talks/components/learning-wizard/steps/TranslateStep`
- `WizardTranslationPanel` renders state badges for Stale correctly → confirmed (shows Start/Retranslate button when state is Stale)

**Chunk 3 — Before starting, verify:**
- `ValidateStep` import path resolves → `@/features/toolbox-talks/components/learning-wizard/steps/ValidateStep`
- `ValidationHistoryTab` co-exists: ValidateStep goes above it, not replacing it
- `useValidationRuns` query key: `contentCreationKeys.validationRuns(talkId)` from `use-content-creation.ts`

**Chunk 4 — Before starting, verify:**
- Which scalar changes in `UpdateToolboxTalkCommandHandler` trigger MarkStale (quiz-settings changes vs. section content changes) — audit the handler's scalar-change detection logic

**Chunk 5 — Before starting, verify:**
- `TranslationWorkflowEvent` creation pattern for a brand-new language (Initial event) — check existing `StartTranslation` handler for the event-creation shape to copy
- Language code selector: reuse the existing language combobox from the wizard's InputConfigStep or settings

**Chunk 6 — Before starting, verify:**
- `useWorkflowSubscription` returns individual language states with `state` field → confirmed (returns `{ languageCode, state, ... }[]`)

---

## 11. Out-of-Scope Items Flagged

The following were considered and explicitly excluded from §24:

| Item | Reasoning |
|------|-----------|
| Title / Description inline edit | Not in BACKLOG §24 design rules. Title/description are scalar fields; they cascade MarkStale via `UpdateToolboxTalkCommandHandler` but the UI scope for editing them on the detail page was not specified. Add to post-§24 polish. |
| Video / PDF file replacement | File management endpoints exist (`DELETE /video`, `POST /video`) but replacing files on a Published talk has broader downstream effects (subtitle invalidation, slide regeneration). Out of scope for §24. |
| Subtitle regeneration trigger | Covered by the existing subtitle processing flow, not §24. |
| Slideshow regeneration | Similarly covered by existing AI slideshow endpoints. |
| `TranslationWorkflowPanel` (old full-feature panel) | The old panel (from `ToolboxTalkForm.tsx`) has Validate, Review, History, Cancel External Review buttons. §24 replaces its role on the detail page with `TranslateStep` + `ValidateStep`. Do not import the old panel. |
| Wizard step navigation from detail page | Talk detail does not link back into the wizard URL space. The wizard's `/learnings/[talkId]/...` routes are for creation only. |
| Course validation run integration | `ToolboxTalkDetail` already hides the Validation tab when `isPartOfCourse`. Course-level validation stays on the course pages. Not changed by §24. |
| Bulk section import / replacement from new file | Out of scope — this would be a new feature, not an edit workflow. |

---

## Appendix — Key File Paths

| Role | Path |
|------|------|
| Talk detail component | `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` |
| Detail page shell | `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/page.tsx` |
| TranslateStep (lift as-is) | `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` |
| ValidateStep (lift as-is) | `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx` |
| WizardTranslationPanel | `web/src/features/toolbox-talks/components/learning-wizard/components/WizardTranslationPanel.tsx` |
| SectionList (reuse component) | `web/src/features/toolbox-talks/components/learning-wizard/components/SectionList.tsx` |
| useWorkflowSubscription | `web/src/features/toolbox-talks/components/learning-wizard/hooks/useWorkflowSubscription.ts` |
| useToolboxTalk / useUpdateToolboxTalk | `web/src/lib/api/toolbox-talks/use-toolbox-talks.ts` |
| Full update handler (no status gate, has MarkStale) | `src/Modules/ToolboxTalks/.../Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs` |
| Sections wizard handler (Draft only, no MarkStale) | `src/Modules/ToolboxTalks/.../Commands/UpdateToolboxTalkSections/UpdateToolboxTalkSectionsCommandHandler.cs` |
| Questions wizard handler (Draft-gated) | `src/Modules/ToolboxTalks/.../Commands/UpdateToolboxTalkQuestions/UpdateToolboxTalkQuestionsCommandHandler.cs` |
| Workflow service (all 15 methods) | `src/Modules/ToolboxTalks/.../Services/Workflows/TranslationWorkflowService.cs` |
| StartTalkTranslation handler (validates TargetLanguageCodes) | `src/Modules/ToolboxTalks/.../Commands/StartTalkTranslation/StartTalkTranslationCommandHandler.cs` |
