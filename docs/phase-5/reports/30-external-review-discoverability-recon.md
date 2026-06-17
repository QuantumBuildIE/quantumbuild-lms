# §30 External Review Discoverability — Recon Report

**Date:** 2026-06-17  
**Scope:** Read-only investigation. No code changes.  
**Prompt reference:** §30 External review user journey gap  

---

## Summary

External review UI exists — it lives in `TranslationWorkflowPanel.tsx`, rendered exclusively on the **Edit page** (`/admin/toolbox-talks/talks/{id}/edit`), not the talk detail page. The new wizard's Publish step navigates to the detail page on success, which has no translation management at all. A user completing the new wizard must click "Edit" (from the detail page's action bar), scroll to the bottom of the full edit form, and then discover the `Content Translations` card there.

There is a deeper gap than the §30 investigation note recognised: the **"Send for review" button requires `ReviewerAccepted` state, which is unreachable through any current UI path**. `SubmitInternalReview()` is the only method that produces `ReviewerAccepted`, and it has no controller endpoint and no frontend caller. The button is permanently invisible under normal usage.

The recommended fix combines two parts: (1) relax the backend state gate to accept `Validated → AwaitingThirdParty` directly, and (2) add a per-language "Send for external review?" prompt at the Publish step's back-translation scores card. This is where the user already has the validation scores in front of them and the decision context is richest.

---

## Step 1 — Existing external review surface

### Location

`TranslationWorkflowPanel` is defined at:

- `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx` (full component, 505 lines)

It is **rendered in exactly one place**:

- `web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx:1061` — inside the `{isEditing && talk && ...}` branch

`ToolboxTalkForm` is used by the **Edit page** only:

- `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx:86`

### Conditions for rendering

```tsx
{isEditing && talk && (
  talk.translations.length > 0 || talk.sections.length > 0 || talk.questions.length > 0
) && (
  <TranslationWorkflowPanel
    toolboxTalkId={talk.id}
    existingTranslations={talk.translations}
  />
)}
```

`ToolboxTalkForm.tsx:1048-1065`. The panel renders for any published talk that has content — so a new-wizard talk that has been published will show the panel on the edit page.

### Per-language actions exposed

For each language row in the panel:

| Action | When visible | What it does |
|--------|-------------|-------------|
| Translate | `state === 'Initial' \| 'Stale' \| 'Accepted'` | Calls `generateMutation.mutateAsync` → `POST /translations/generate` |
| Validate | `state === 'AIGenerated'` | Calls `validateMutation.mutateAsync` → `POST /validation/validate` |
| Review | `state === 'Validated' \| 'ReviewerAccepted' \| 'ThirdPartyReviewed'` | Navigates to `/admin/toolbox-talks/talks/{id}/translations/{lang}/review` |
| **Send for review** | **`state === 'ReviewerAccepted'` only** | Opens `SendExternalReviewDialog` → `POST /translations/{lang}/initiate-external-review` |
| Cancel invitation | `state === 'AwaitingThirdParty'` | Opens `CancelExternalReviewDialog` → `POST /translations/{lang}/cancel-external-review` |
| History | Always | Opens `WorkflowHistoryModal` |

Source: `TranslationWorkflowPanel.tsx:58-76` (state-gate functions), `270-421` (JSX rendering).

### Visual prominence

- "Send for review" is a secondary `variant="outline"` button with a `Send` icon at sm size.
- It is contextually positioned in the per-row action group, not as a prominent CTA.
- No help text explains what external review is or when to use it.
- No persistent notice when no language is in a reviewable state.

### Navigation path to reach the panel

New wizard flow → publish → `/admin/toolbox-talks/talks/{id}` (detail page) → click "Edit" → `/admin/toolbox-talks/talks/{id}/edit` → scroll past all form fields (title, description, sections, quiz) → reach `TranslationWorkflowPanel` at the bottom.

**The detail page (`ToolboxTalkDetail.tsx`) does NOT render `TranslationWorkflowPanel`.** It renders "Overview" and "Validation" tabs only. There is a small muted-text note at `ToolboxTalkDetail.tsx:330-337`:

```tsx
<p className="text-sm text-muted-foreground">
  To generate translations or subtitles, use the{' '}
  <Link href={`${basePath}/${talk.id}/edit`}>Edit page</Link>.
</p>
```

This note appears under the sections list and is likely to go unnoticed.

---

## Step 2 — Wizard flow walk

### Step 1 — Input & Config

No mention of external review. The user selects target languages but is given no indication that any further review workflow exists.

### Step 2 — Parse

No mention.

### Step 3 — Quiz

No mention.

### Step 4 — Settings

No mention.

### Step 5 — Translate (`TranslateStep.tsx`)

Shows per-language `WizardTranslationPanel` with "Start" buttons per language. The panel shows workflow state badges (`Initial`, `Translating`, `AIGenerated`, `Validated`, etc.) but offers only "Start translation" actions. No mention of external review as an option now or later.

### Step 6 — Validate (`ValidateStep.tsx`)

Shows per-section validation results with Accept/Edit/Retry actions, plus an aggregate progress panel. Description reads: *"Review back-translation results for each language. Pass sections are accepted automatically. Accept, edit, or retry any Review or Fail sections before publishing."*

This is the natural moment at which external review would be most relevant — the user has just seen the back-translation scores and may want a third-party sign-off before publishing. No mention of external review. No link. No "alternatively, send to a third party" pointer.

### Step 7 — Publish (`PublishStep.tsx`)

Shows `ExternalReviewWarningBanner` (`PublishStep.tsx:413-456`) if any target language is in `AwaitingThirdParty` state. The banner reads: *"N language(s) awaiting external review — when the reviewer submits, their translation will be applied automatically..."*

**This banner can never display for a pure-wizard user.** Reaching `AwaitingThirdParty` requires initiating external review, which requires `ReviewerAccepted` state, which is unreachable (see Step 4 below). The banner is defensive code that has never been reachable in practice from the new wizard flow.

---

## Step 3 — Post-publish redirect

Publish success in `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/publish/page.tsx:32`:

```tsx
router.push(`/admin/toolbox-talks/talks/${result.talkId}`);
```

This navigates to the **talk detail page**, not the edit page.

### What the detail page shows

`ToolboxTalkDetail.tsx` renders:
- Header: title, status badge, Edit/Schedule/Delete action buttons
- Statistics cards (assigned/completed/pending/overdue counts)
- Tabs: "Overview" and "Validation"
- Overview tab: Talk Details card, Sections accordion, Quiz Questions list
- A small muted text note pointing to the Edit page for translations (`ToolboxTalkDetail.tsx:330-337`)

**No `TranslationWorkflowPanel`. No per-language workflow state display. No way to initiate external review from this page.**

### Is the landing page good enough?

A user wanting external review after publishing must:
1. Land on detail page
2. Read the small grey note about the Edit page
3. Click "Edit"
4. Navigate to `/admin/toolbox-talks/talks/{id}/edit`
5. Scroll past title, description, sections, quiz questions
6. Find the `Content Translations` card

That is a 5-step path with no visual signal along the way.

---

## Step 4 — Pre-publish vs post-publish intent

### What the `AwaitingThirdParty` banner implies

The banner on the Publish step (`PublishStep.tsx:437-455`) says: *"When the reviewer submits, their translation will be applied to the published talk automatically."* This confirms external review can legitimately happen **before or after publishing** — the system does not block publishing while review is in flight.

### Backend state requirements

`InitiateExternalReview` in `TranslationWorkflowService.cs:348`:

```csharp
if (state != TranslationWorkflowState.ReviewerAccepted)
    return Result.Fail(
        $"Cannot initiate external review from state {state}; requires ReviewerAccepted.",
        FailureCode.WorkflowInvalidState);
```

The backend strictly requires `ReviewerAccepted` to initiate external review.

### How `ReviewerAccepted` is set

`ReviewerAccepted` is produced by exactly one event:

```csharp
WorkflowEventTypes.InternalReviewSubmitted => TranslationWorkflowState.ReviewerAccepted,
```
`TranslationWorkflowService.cs:809`

`InternalReviewSubmitted` is emitted by `SubmitInternalReview()` (`ITranslationWorkflowService.cs:52`).

### Critical finding: `ReviewerAccepted` is unreachable

A codebase-wide grep finds `SubmitInternalReview` is called in exactly two places:
- Its own service implementation (`TranslationWorkflowService.cs:291`)
- The interface definition (`ITranslationWorkflowService.cs:52`)

**There is no controller endpoint, no command handler, and no frontend caller for `SubmitInternalReview`.** The method is defined but never invoked.

Consequences:
1. `ReviewerAccepted` state is never reached through any current UI path.
2. `canSendForExternalReview(state) = state === 'ReviewerAccepted'` is permanently false.
3. The "Send for review" button in `TranslationWorkflowPanel` never renders under normal usage.
4. The `ExternalReviewWarningBanner` on the Publish step can never fire from a pure-wizard user's session.

The only paths to `ReviewerAccepted` via the event map (`TranslationWorkflowService.cs:813-815`) are:
- `ExternalReviewRejected → ReviewerAccepted`
- `ExternalReviewCancelled → ReviewerAccepted`
- `ExternalReviewDeclined → ReviewerAccepted`

...all of which require a prior `AwaitingThirdParty` state, which itself requires `ReviewerAccepted` first. This is a dead cycle.

### Post-publish external review — also blocked

After publishing, a user on the edit page who discovers `TranslationWorkflowPanel` faces the same state gate: the "Send for review" button never renders. External review post-publish is explicitly described in §21 as a gap, but the gap is deeper than §21 scoped — the cancel path (which §21 tracks) is unreachable without being in `AwaitingThirdParty` first.

---

## Step 5 — Discoverability candidates

Based on the above, the fix has two separable concerns:

**Concern A — State gate unblock (backend + frontend):**
The `ReviewerAccepted` state requirement is the root blocker. All discoverability work is wasted if the button will never render.

Proposed resolution options:

1. **Relax backend gate to accept `Validated`** — Change `TranslationWorkflowService.InitiateExternalReview` to also accept `Validated` state. This means a user can send for external review immediately after validation, without a separate "internal review accepted" step. Most straightforward path. Would also need `canSendForExternalReview` to be expanded in the frontend:
   ```ts
   function canSendForExternalReview(state: TranslationWorkflowState): boolean {
     return state === 'Validated' || state === 'ReviewerAccepted';
   }
   ```
   Backend risk: low — the `SubmitInternalReview` concept may have been a Phase 4 design that was never needed in practice.
   
2. **Wire `SubmitInternalReview` as an API endpoint** — Add a controller action and front-end trigger that auto-submits the internal review (accepting all decided sections) before initiating external review. More faithful to the original design intent but adds complexity.

3. **Auto-transition on all-sections-decided** — After all sections are accepted/edited in the Validate step, automatically emit `InternalReviewSubmitted` in the background, advancing to `ReviewerAccepted`. No user action needed.

**Recommendation for Concern A:** Option 1 (relax gate to `Validated`). The `SubmitInternalReview` concept adds a step with unclear user value — the user has already reviewed sections individually, and an additional "submit internal review" click before "send for external review" is friction without benefit.

---

**Concern B — Discoverability (frontend only):**

| Candidate | Where | What it says | Cost | Risk |
|-----------|-------|-------------|------|------|
| **B1. Publish step CTA** | `PublishStep.tsx` — "Back-translation scores" card, per-language row | "Send for external review?" link/button per `Validated` language | Small: add per-row action to existing card | Navigates away from wizard — but wizard state is persisted via URL segments |
| B2. Validate step pointer | `ValidateStep.tsx` — summary bar (`ValidateStep.tsx:269-281`) | "Want external sign-off? Send for review after publishing from the Edit page." | One-liner text change, zero new backend | Weak call-to-action (deferred action to unknown future step) |
| B3. Post-publish detail page card | `ToolboxTalkDetail.tsx` — new "Translations" card below statistics | Per-language state + "Send for review" action, rendered on detail page | Medium: new component, coordinate with §24 | Detail page structure is in flux (§24); better to coordinate |
| B4. Promote detail page note | `ToolboxTalkDetail.tsx:330-337` | Upgrade the small muted note to an amber callout card | Small: restyling + clearer copy | Doesn't surface the actual action, only points elsewhere |

**Recommendation for Concern B: B1 — Publish step CTA.**

The Publish step's "Back-translation scores" card already shows per-language outcomes with score percentages and outcome badges (`PublishStep.tsx:307-338`). Extending this card with a conditional "Send for external review" link/icon per language in `Validated` state is:
- **Contextually correct** — the user just reviewed the scores and may want external sign-off before publishing
- **No new page** — the action can open a dialog inline (same `SendExternalReviewDialog` already used in `TranslationWorkflowPanel`)
- **Low cost** — the API client function and mutation hook already exist (`useInitiateExternalReview`, `TranslationWorkflowPanel.tsx:87`)
- **No backend changes beyond Concern A** (state gate relaxation)
- **Non-blocking** — the Publish button remains available; external review is optional

Copy suggestion (per-language row, appears when outcome is not Pass or state is `Validated`):
> "→ Send for external review"

If clicked, open `SendExternalReviewDialog` inline. On success, the language transitions to `AwaitingThirdParty`. The Publish step's `ExternalReviewWarningBanner` (currently dead code) would then fire correctly for the first time.

### What this does NOT require

- New backend endpoints (state gate fix is a one-line change)
- New navigation paths
- Changes to the talk detail page (safe to coordinate with §24 separately)
- Changes to the old wizard

---

## Step 6 — Surface coordination

### Current state of the talk detail page

`ToolboxTalkDetail.tsx` currently has: header, statistics, Overview tab, Validation tab. Translations and subtitles are explicitly deferred to the Edit page via a small note.

### Planned additions (§24)

§24 Chunk 2 ("Translation re-run UI on talk detail page") and Chunk 3 ("Validate step UI on talk detail page") will add translation management to the detail page. When §24 Chunk 2 lands, the detail page will presumably show per-language states and translation actions — at that point, wiring "Send for external review" from the detail page becomes natural.

### Recommendation

**B1 (Publish step CTA) is self-contained and does not need to wait for §24.** It works within the wizard's boundary. The §24 work can add a richer translation surface to the detail page (including external review) as part of its scope — that doesn't conflict.

The small note at `ToolboxTalkDetail.tsx:330-337` becomes obsolete once §24 Chunk 2 lands. It can be removed at that point.

### Is the panel becoming a kitchen sink?

The edit page `TranslationWorkflowPanel` + `SubtitleProcessingPanel` are already together. The combination is coherent (both are "content processing" concerns). The risk is that the Edit page is accessed for non-editing tasks (translation management) when a Read-only path on the detail page would be better UX. This is the §24 problem, not the §30 problem.

---

## Adjacent observations

1. **`PublishSuccessState` is dead code.** `PublishStep.tsx:478` exports a success state component but the publish page uses `router.push` on success rather than rendering this component. It was presumably intended for an inline success state that was replaced by navigation. Can be deleted if no other consumer exists.

2. **`ExternalReviewWarningBanner` is defensive code that has never fired from the new wizard.** The trigger condition (`AwaitingThirdParty`) is blocked by the state gate gap. Once both Concern A (state gate) and Concern B (CTA) are addressed, this banner becomes reachable and useful for the first time.

3. **The §30 investigation note (2026-06-15) overstates the discoverability gap.** It says "UI exists on the talk detail page via TranslationWorkflowPanel.tsx" — but the panel is on the **Edit page**, not the detail page. The detail page has no translation management at all. This distinction matters for sizing the discoverability fix.

4. **The 2026-06-15 note missed the state gate.** It concludes "the gap is journey/discoverability, not missing UI." But the "Send for review" button will never render regardless of where users navigate, because `ReviewerAccepted` is unreachable. The implementation gap is structural, not just discoverability.

5. **`canReview` includes `ReviewerAccepted` but `ReviewerAccepted` is unreachable.** The "Review" button (`TranslationWorkflowPanel.tsx:347`) shows for `ReviewerAccepted` state, but since that state can't be reached, this branch of the condition is dead. Not harmful, but worth noting if the fix uses option 1 (relax backend gate) rather than option 2 (wire `SubmitInternalReview`).

6. **§11 ("Cancel external review — end-to-end") tracks the cancel button gap.** Backend is complete; the cancel button exists in `TranslationWorkflowPanel` (it renders for `AwaitingThirdParty` state). But since `AwaitingThirdParty` is unreachable (see state gate gap), the cancel button is also dead. §11 and §30 are blocked by the same root cause.

---

## Recommended direction

**Two-part fix, phased:**

**Part 1 (backend, small):** Relax `InitiateExternalReview` to accept `Validated` in addition to `ReviewerAccepted`. Update `canSendForExternalReview` in `TranslationWorkflowPanel.tsx:70-73` to include `Validated`. This unblocks both the existing panel button and the proposed wizard CTA.

**Part 2 (frontend, small):** Add a per-language "Send for external review" action to the Publish step's "Back-translation scores" card. Reuse the existing `SendExternalReviewDialog` and `useInitiateExternalReview` hook. The banner (`ExternalReviewWarningBanner`) already handles the post-initiation confirmation display.

Combined effort estimate: ~half a day. No new components, no new backend endpoints, no coordination blockers.

**Do not make this fix depend on §24.** §24 will add a better long-term home for translation management on the detail page, but §30's UX gap is solvable independently within the wizard's existing Publish step.

---

## Coverage table

| Item | Status | File:line |
|------|--------|----------|
| `TranslationWorkflowPanel` location | Edit page only | `ToolboxTalkForm.tsx:1061` |
| `canSendForExternalReview` gate | `ReviewerAccepted` only | `TranslationWorkflowPanel.tsx:70-73` |
| `SubmitInternalReview` controller endpoint | Missing — no endpoint exists | grep confirms 0 controller routes |
| `ReviewerAccepted` state reachability | Unreachable (circular dependency) | `TranslationWorkflowService.cs:809,813-815` |
| Post-publish navigate target | Detail page (not edit page) | `publish/page.tsx:32-34` |
| Detail page — `TranslationWorkflowPanel` present | No | `ToolboxTalkDetail.tsx` (whole file) |
| Detail page note to Edit page | Small muted text, easy to miss | `ToolboxTalkDetail.tsx:330-337` |
| Wizard Step 5 (Translate) — external review mention | None | `TranslateStep.tsx` (whole file) |
| Wizard Step 6 (Validate) — external review mention | None | `ValidateStep.tsx` (whole file) |
| Wizard Step 7 (Publish) — `ExternalReviewWarningBanner` | Present but unreachable | `PublishStep.tsx:413-456` |
| `ExternalReviewWarningBanner` — can fire from wizard | No (circular state gate) | `PublishStep.tsx:420-424` |
| §23 action 1 (initiate external review) | Still missing from wizard | BACKLOG §23, §30 |
| §11 cancel button in `TranslationWorkflowPanel` | Present but unreachable | `TranslationWorkflowPanel.tsx:384-396` |

---

*Recon by Claude Code, 2026-06-17. No code changes made.*
