# Reviewer Portal Edited-Section Submission Returning Null — Recon

**Date:** 2026-07-14
**Branch:** transval
**Scope:** Read-only investigation. No application files, migrations, or data modified.

---

## 0. Important context this recon had to establish first

Before tracing the symptom, this recon confirmed that the feature under investigation is **not** the same one described in three prior recon documents dated 2026-07-08/09 (`docs/third-party-edit-not-landing-recon.md`, `docs/accept-endpoint-data-loss-recon.md`, `docs/external-review-auto-apply-recon.md`, `docs/external-review-per-section-impl-recon.md`, `docs/external-review-per-section-send-recon.md`). Those documents describe a since-fixed architecture where the reviewer's edits were parked in a `WorkflowReview` holding row and never propagated (`ConfirmExternalReview` had no controller route). Git history confirms all 7 chunks of that redesign shipped between 2026-07-09 and 2026-07-14:

```
4dccbe6 feat(external-review): data model foundation for per-section provenance
a5578a5 feat(external-review): send-side per-section selection (Chunk B)
0fadc9a feat(external-review): Gate 2 tightening + per-section provenance (Chunk D)
8c8a779 feat(external-review): reviewer UI for per-section editability (Chunk C)
e7b5de3 feat(external-review): derived per-section provenance display (Chunk E)
94fc8f9 feat(external-review): allow second-round reviews from ThirdPartyReviewed (Chunk F)
d699ea4 fix(external-review): timestamp precision + admin second-round entry
28f12a1 feat(external-review): auto-apply reviewer edits on submission
```

The current architecture **auto-applies** the reviewer's edits directly into `ToolboxTalkTranslation.TranslatedSections` inside `SubmitExternalReview` itself — there is no separate confirm step, and reviewers can now be scoped to a subset of "editable" sections (the rest render read-only, per `ExternalParticipantInvitation.EditableSectionIndices`). This recon traces the **current, live** implementation. Any fix scoped from the older documents would be solving an already-fixed problem.

---

## 1. Reviewer portal file structure (Part 1)

| Layer | File |
|---|---|
| Page | `web/src/app/external-review/[token]/page.tsx` (only file in the route tree) |
| Decline dialog | `web/src/features/external-review/components/DeclineConfirmationDialog.tsx` |
| Flag highlighting (reused, not owned by this route) | `web/src/features/toolbox-talks/components/create-wizard/steps/validate/FlaggedText.tsx` |
| API client | `web/src/lib/api/external-review.ts` — plain `fetch`, not the Axios `apiClient` (correct: no JWT session exists on this anonymous route) |
| Types | `web/src/types/external-review.ts` |
| Submission mutation | `handleSubmit()` inside `page.tsx` (no TanStack Query mutation hook — direct `await submitExternalReview(...)` call) |
| Admin send-side UI | `web/src/features/toolbox-talks/components/SendExternalReviewDialog.tsx`, invoked from `TranslationWorkflowPanel.tsx` and `ValidateStep.tsx` |
| Backend controller | `src/QuantumBuild.API/Controllers/ExternalReviewController.cs` (`[AllowAnonymous]`, `api/external-review`) |
| Backend service | `TranslationWorkflowService.cs` — `InitiateExternalReview`, `GetPortalContext`, `SubmitExternalReview`, `ValidateExternalReviewSubmissionAsync`, `ValidateEditableSectionIndicesAsync` |
| DTOs | `ExternalReviewPortalDto.cs`, `ExternalReviewSubmissionDto.cs` (backend); `external-review.ts` (frontend) |
| Entity | `ExternalParticipantInvitation.cs` (new `EditableSectionIndicesJson`/`EditableSectionIndices` since Chunk A); `WorkflowReview.cs` (audit row, unchanged shape) |

No CQRS/MediatR layer exists for any of this — every action is a direct `ITranslationWorkflowService` method call with inline guard-clause validation, consistent with the rest of the workflow service.

---

## 2. Frontend state capture (Part 2)

`page.tsx` uses plain `useState`, not a form library.

- **Load:** on mount, `getExternalReviewPortal(token)` returns `ExternalReviewPortalDto`. If `portalStatus === "Active"`, every section in `body.sections` is seeded into `editedSections: Record<number, string>` keyed by `section.sectionIndex`, value = `section.translatedText` (lines 136-142). This is unconditional — read-only sections are seeded too.
- **Edit:** the `Textarea`'s `onChange` only fires the state update when `editable` is true (line 397-403); read-only sections render `disabled` and never receive keystrokes.
- **No dirty-tracking / changes-only filtering exists.** Every editable section's current text (whether the reviewer touched it or left the pre-filled AI translation untouched) is included in the submission — there is no "only submit what changed" logic anywhere in this file. This rules out one common "submitted empty" cause outright.
- **What is included at submit time** is governed by `filterEditableSections()` (exported helper, lines 88-95): it maps `editedSections` to `{sectionIndex, translatedText}[]` and filters to only indices where `isSectionEditable(index, portalData.editableSectionIndices)` is true. `editableSectionIndices: null` means "no restriction" (every index passes).

**Conclusion for Part 2: this mechanism is sound.** It only omits sections by design (read-only ones), not accidentally. The interesting question is whether `portalData.sections` and `portalData.editableSectionIndices` — both handed down from the backend — ever disagree about which indices exist, which is traced in §4.

---

## 3. Submission payload shape vs backend DTO (Part 3)

**Frontend request** (`page.tsx:163-168`):
```ts
const editedArray = filterEditableSections(editedSections, portalData.editableSectionIndices);
const editedContent = JSON.stringify(editedArray);
await submitExternalReview(token, { accepted: true, editedContent });
```
`SubmitExternalReviewRequest` (`types/external-review.ts:34-37`):
```ts
export interface SubmitExternalReviewRequest {
  accepted: boolean;
  editedContent: string | null;   // JSON-serialised ExternalReviewEditedSectionDto[]
}
```

**Backend DTO** (`ExternalReviewController.cs:136-140`):
```csharp
public record SubmitExternalReviewRequest
{
    public bool Accepted { get; init; }
    public string? EditedContent { get; init; }
}
```

Field names match exactly modulo casing (`accepted`/`editedContent` vs `Accepted`/`EditedContent`) — this project uses ASP.NET Core's default `System.Text.Json` behaviour (camelCase output, case-insensitive input binding), the same pattern used by every other controller in the codebase. No `[FromBody]` issue: the parameter is explicitly annotated. No Newtonsoft-vs-System.Text.Json split found anywhere in this controller or its dependencies. **This DTO pair is not the source of the bug** — the shapes are aligned and this exact contract was already exercised successfully by the pre-per-section version of this endpoint (confirmed by the 07-08 recon's data-flow trace, and unchanged since).

The inner payload (the JSON string inside `editedContent`) also matches: `ExternalReviewEditedSectionDto { sectionIndex, translatedText }` (frontend, `types/external-review.ts:29-32`) vs `ExternalReviewEditedSectionDto { SectionIndex, TranslatedText }` (backend, `DTOs/Workflows/ExternalReviewSubmissionDto.cs`). Same conclusion.

**Where the DTOs do carry a real asymmetry** (not a bug, but a design fact worth flagging): the outer request only carries `editedContent` for *edited* sections; there is no field that tells the backend which sections the reviewer's UI *displayed as editable*. The backend independently re-derives the allowed set from `invitation.EditableSectionIndices` (persisted at send time) rather than trusting anything in the request — this is intentional defense-in-depth (§5 of `docs/external-review-per-section-impl-recon.md`), not a client/server mismatch.

---

## 4. Backend read and persistence (Part 4)

`SubmitExternalReview` (`TranslationWorkflowService.cs:478-621`) — traced end-to-end against the live code:

1. Token hashed, invitation loaded (`IgnoreQueryFilters` — correct, no JWT).
2. Guards: invitation missing → 404; already `Used` → 409; expired/not-`Pending` → 410.
3. State guard: current derived workflow state must be `AwaitingThirdParty`, else 409.
4. **Accept path only** (`accepted == true`, which the current frontend always sends on Submit — Decline is a separate button/endpoint): `editedContent` is deserialised into `List<ExternalReviewEditedSectionDto>`; malformed JSON is swallowed to an empty list (not an error) — see §6 for why this matters.
5. **Four validation gates run** (`ValidateExternalReviewSubmissionAsync`, lines 1072-1142), in order, fail-fast, before any write:
   - Gate 1: `edits.Count == 0` → reject (`WorkflowSubmissionInvalid`, HTTP 400). **A wholesale-empty submission is caught and surfaced as a visible error — it does not silently succeed.**
   - Gate 2: every submitted `SectionIndex` must be a member of `invitation.EditableSectionIndices` (if non-null) or merely in-range against the *live* `ToolboxTalkTranslation.TranslatedSections` (if null/full-scope).
   - Gate 3: every submitted section's text must be non-blank.
   - Gate 4: coarse XSS denylist.
6. **Persistence** (only if all gates pass): loads the live `ToolboxTalkTranslation` row, deserialises `TranslatedSections` into `List<TranslatedSectionEntry>`, and for each edit does `sections[edit.SectionIndex].Content = edit.TranslatedText` plus stamps `ReviewedAt`/`ReviewedBy` on that entry (lines 557-562). Re-serialises back onto `translation.TranslatedSections`. This **is** a real, working write path into the field employees/admins actually see — the "missing endpoint" bug from the 07-08 recons is gone.
7. A `WorkflowReview` audit row and a `WorkflowEvent` (`ExternalReviewSubmitted`) are written; everything (translation update, review row, event row, invitation status flip) rides one `SaveChangesAsync` call — atomic, no partial-write window.
8. Admin notification email fires afterward (best-effort, exception-swallowing — cannot roll back the write, cannot cause it to appear to fail either).

**No conditional/skip logic exists that would silently drop an edit that was actually submitted and passed the gates.** The merge loop (`foreach (var edit in edits)`) is unconditional for every entry in `edits`.

**Where this recon's investigation therefore had to pivot:** if edits that made it into the `edits` list are always written, and Gate 1 catches a wholesale-empty list with a visible error, then "section data appears null" most plausibly describes **specific sections silently missing from what the reviewer could act on in the first place** — never entering `edits` at all, without tripping any gate (since a gate can only reject what's present, not detect what's absent). §5 below identifies the concrete mechanism.

---

## 5. Diagnosis — most likely root cause, with cited evidence (Part 6, addressed early because Parts 4-5 evidence converges here)

### The section-index domain is assembled from three independently-queried sources that the architecture assumes — but never verifies — stay in lockstep

| # | Source | Used for | Index domain |
|---|---|---|---|
| (a) | `sortedSections` — the talk's live `ToolboxTalkSection` list, sorted by `sectionNumber`, passed into `SendExternalReviewDialog` as array-position indices | The **admin's checkbox selection** of which sections are editable, at send-time (`TranslationWorkflowPanel.tsx:127-129, 554-557`) | Current, live `ToolboxTalkSection` count/order |
| (b) | `TranslationValidationResult` rows from the **latest completed** `TranslationValidationRun`, ordered by `SectionIndex` | The **reviewer's entire visible portal** — `portalData.sections` — is built exclusively from this (`GetPortalContext`, `TranslationWorkflowService.cs:773-806`) | Whatever validation run last completed — a snapshot, not live |
| (c) | `ToolboxTalkTranslation.TranslatedSections` — a positionally-indexed JSON array | The actual **merge target** on submit (`sections[edit.SectionIndex]`, line 559) and Gate 2's in-range check when scope is null | Current, live `TranslatedSections` |

`TranslationWorkflowPanel.tsx:127-129` states the assumption in a comment, verbatim:
```ts
// Sorted by sectionNumber — array index is the SectionIndex the backend validates against
// (translated sections are generated from these in the same order).
```
This is an assumption about (a) vs (c), stated but never enforced in code. Nothing links (a)'s index domain to (b)'s at all — the admin's selection UI has no dependency on, or awareness of, which validation run the reviewer's portal will end up sourcing from.

### The concrete drift trigger already exists in the codebase: `NeedsRevalidation`

`ToolboxTalkTranslation.NeedsRevalidation` is set to `true` by `UpdateToolboxTalkCommandHandler.cs:193-194` and `UpdateToolboxTalkSettingsCommandHandler.cs:104-105` whenever a talk's sections are edited **after** a translation/validation already exists. This flag is:
- **Not** part of `TranslationWorkflowState` derivation (state is computed purely from the `WorkflowEvent` log — `TranslationWorkflowService.cs`, confirmed in the 07-09 recon's §6.1 trace and re-confirmed here by reading `GetState`, `TranslationWorkflowService.cs:119-127`, where `NeedsRevalidation` is surfaced only as a passive DTO field).
- **Not** checked by `InitiateExternalReview`'s state guard (`TranslationWorkflowService.cs:389-394` — only checks `Validated`/`ReviewerAccepted`/`ThirdPartyReviewed`).
- **Not** checked by `GetPortalContext` (queries the latest *completed* run unconditionally, with no freshness check against the translation's current content).
- **Not** checked by either validation-gate method (`ValidateEditableSectionIndicesAsync` at send-time, `ValidateExternalReviewSubmissionAsync` at submit-time) — both only range-check indices against counts, never compare (a)/(b)/(c) against each other for consistency.

So: a talk can sit with `NeedsRevalidation = true` — meaning its source sections were edited after the last validation run — for an indefinite period while workflow `state` remains `Validated`/`ReviewerAccepted`/`ThirdPartyReviewed` (fully eligible to send for external review) and nothing in the send or submit path detects or blocks on the drift.

### The failure mode this produces

When (a) the admin's live section list and (b) the validation run's snapshot disagree in count (most directly: a section was added/removed from the talk after the last validation run, before the admin opens "Send for External Review"):

- The admin selects "editable" indices against the **current, larger/different** section list (a).
- Those indices are persisted onto the invitation as `EditableSectionIndices`.
- `ValidateEditableSectionIndicesAsync` at send-time checks them against `TranslatedSections` (c) — if (c) hasn't been regenerated either (translations aren't auto-regenerated on `NeedsRevalidation`, only flagged), (a) and (c) can still coincidentally agree on *count* while genuinely representing different content, or can disagree on count and get caught here (a visible error at send time — not the symptom described).
- Critically, **(b) is never consulted or validated against at all**, at either send-time or submit-time. If the reviewer's portal (b) has fewer entries than the admin's selected editable indices (a) — e.g. the validation run only produced results for the section count as of whenever it last ran — then some indices the admin marked "editable" and the backend has flagged as an editable scope **simply never appear as rendered section cards in the reviewer's UI at all** (`page.tsx:355` `.map()` iterates `portalData.sections`, which only has entries for what (b) returned).
- The reviewer cannot see, and therefore cannot type into, those sections. Nothing is ever added to `editedSections` for those indices (§2). `filterEditableSections` can only filter what's present — it has no way to notice an index is *missing* rather than merely *not edited*.
- The submission still passes all four gates (Gate 1 passes if the reviewer edited *any* section; Gate 2 only checks that submitted indices are a *subset* of the editable set, never that the submitted set is *complete*) and returns `200 OK`. **From the reviewer's and the system's perspective, the submission succeeded — but specific sections' edits were never captured anywhere, silently.** This is the shape of "section data appears null": not a null field in a populated response, but whole sections absent end-to-end with no error raised at any layer.

### Confirming this is untested, unaddressed territory

`tests/QuantumBuild.Tests.Integration/Workflows/TranslationWorkflowServiceTests.cs` has extensive coverage of the per-section-editable feature (`InitiateExternalReview_WithValidEditableIndices_PersistsList`, `SubmitExternalReview_AcceptedTrue_PartialEdits_UpdatesOnlyEditedSections`, `Submit_WithScopedInvitation_DerivesWholeTranslationFlagsFromSections`, `SubmitExternalReview_SectionIndexOutOfRange_Returns400WithSubmissionInvalid`, etc.) — but every test constructs its `TranslationValidationResult` fixtures and its `TranslatedSections` fixtures with matching, consistent section counts. **No test exercises a scenario where the validation run's section count/order differs from the live translation's**, and no test touches `NeedsRevalidation` in combination with external review at all. This is a gap in coverage that lines up exactly with the architectural seam identified above, not proof by itself, but corroborating evidence that this scenario was never designed for or verified.

This diagnosis was flagged as an unresolved, named risk in the planning recons themselves before implementation — `docs/external-review-per-section-impl-recon.md` §9 risk #2 ("`SectionIndex` (positional int) vs. `SectionId` (Guid) — a pre-existing identity mismatch this feature inherits... fragile if sections are ever reordered between invitation-send-time and reviewer-submit-time") and the original `docs/third-party-edit-not-landing-recon.md`'s observation that the portal sources "the original AI section content, not anything from a prior reviewer round." Neither risk was closed by chunks A-F; both chunks focused on the editability/provenance feature itself, not on synchronizing the three section-index sources.

---

## 6. A second, independent contributing factor worth flagging (lower confidence, smaller blast radius)

`SubmitExternalReview` line 513-521:
```csharp
try
{
    if (!string.IsNullOrWhiteSpace(editedContent))
        edits = JsonSerializer.Deserialize<List<ExternalReviewEditedSectionDto>>(editedContent) ?? new();
}
catch (JsonException)
{
    edits = new();
}
```
A malformed `editedContent` string (e.g., a frontend serialization bug, or a `null` value being coerced oddly) is **silently treated as zero edits**, not surfaced as an error at this point — it only fails downstream at Gate 1 with a generic "must include at least one section" message that gives the reviewer no indication *why* (a JSON parse failure looks identical to "you submitted nothing"). This is not the primary suspect (the frontend's `JSON.stringify(editedArray)` at `page.tsx:167` is straightforward and was not observed to produce malformed output in this static read), but it is a place where a genuine parse-level bug would manifest as exactly the reported symptom's texture ("submission looks empty/null") with zero diagnostic signal in the response. Worth instrumenting (a distinct error message or a logged warning) regardless of whether it's the root cause here.

---

## 7. Recent git history (Part 5)

All external-review-portal-adjacent commits in the last 6 days:

```
d699ea4 fix(external-review): timestamp precision + admin second-round entry
94fc8f9 feat(external-review): allow second-round reviews from ThirdPartyReviewed (Chunk F)
e7b5de3 feat(external-review): derived per-section provenance display (Chunk E)
8c8a779 feat(external-review): reviewer UI for per-section editability (Chunk C)
0fadc9a feat(external-review): Gate 2 tightening + per-section provenance (Chunk D)
a5578a5 feat(external-review): send-side per-section selection (Chunk B)
4dccbe6 feat(external-review): data model foundation for per-section provenance
28f12a1 feat(external-review): auto-apply reviewer edits on submission
1bf75f6 feat(workflows): Send and Cancel external review UI on per-language panel, FlaggedWordCount in workflow state (Phase 4.6, closes Phase 4)
```
Plus, most recently (2026-07-14), a cluster of UX-polish commits on the *send* side that touch the same components but not the submit/persistence path:
```
35eac44 fix(wizard): ValidateStep send-for-review gate now includes ThirdPartyReviewed
aa27513 fix(review): graceful UX for workflow-state-ineligible external review
e0bc4f7 feat(review): show per-section validation scores in both review dialogs
02b7f44 feat(learnings): Send for Review action and preview modal
```
`e0bc4f7` is notable: it's the commit that wired `sendReviewScoreByIndex` (validation-run-sourced) alongside `sortedSections` (talk-sourced) into the same dialog, purely to *display* a score next to each checkbox — it did not introduce or fix any consistency check between the two index domains; it's additional evidence that both index domains are actively juxtaposed in the current UI without anyone reconciling them. No commit in this window fixes or even names the (a)/(b)/(c) drift issue in §5.

None of these commits are suspect for introducing a *regression* — the per-section architecture is new since 07-09, so this isn't "something broke," it's "a gap that was present from the first per-section design and was never closed."

---

## 8. Diagnosis summary (Part 6)

**Most likely failure mode: Backend not persisting despite receiving data is ruled out; Frontend payload shape is ruled out. The likely cause is upstream of both — specific sections are absent from the reviewer's rendered portal in the first place, because the reviewer's visible section list (sourced from a validation-run snapshot) and the admin's "editable sections" selection (sourced from the live talk) are not guaranteed to describe the same set of sections, and nothing in the send or submit path detects the drift. `ToolboxTalkTranslation.NeedsRevalidation` — the system's own signal that this drift has occurred — exists but is checked nowhere in this flow.**

This is a **"silently absent," not "silently discarded"** bug: no field anywhere is populated with a JSON `null`; rather, whole section entries never make it into the reviewer's `editedSections` state, `filterEditableSections`' output, or the request body, and no validation gate is positioned to catch an *absence* (only gates present submissions). A submission built this way returns `200 OK`.

**Confidence level:** Medium-high on the architectural gap being real and unaddressed (multiple independent code citations converge on it, and it is corroborated by the planning recons' own pre-implementation risk notes). Lower confidence on this being *the exact* incident the boss observed, since:
- No live reproduction was performed (per this recon's read-only scope).
- No application/Railway logs were available to this environment (same limitation noted in the 07-08 recon; unchanged).
- The local dev database was not probed in this pass — doing so (checking `NeedsRevalidation` on the affected translation, comparing `TranslatedSections` array length to the relevant `TranslationValidationRun`'s result count, and to the talk's current `ToolboxTalkSection` count) would be the fastest way to convert this from "most likely" to "confirmed."

**What would resolve the remaining ambiguity:**
1. For the specific talk/language the boss tested: query `ToolboxTalkTranslations.NeedsRevalidation`, and compare `jsonb_array_length(TranslatedSections)` against the section count of the `TranslationValidationRun` that `GetPortalContext` would have selected (latest `Completed`, matching talk+language) and against the current `ToolboxTalkSections` count for that talk. A mismatch among these three numbers confirms §5 directly.
2. A browser network-tab capture (or Railway request log, if enabled) of the actual `POST /external-review/{token}/submit` body from the affected session — if `editedContent` genuinely contains fewer sections than the invitation's `EditableSectionIndices` count, that's the smoking gun for §5's "sections absent from the portal" mechanism specifically (as opposed to, e.g., a reviewer simply not editing sections they were shown).
3. Whether the affected talk's sections were edited (content added/removed/reordered) at any point after its last validation run but before this external-review round was sent — this is the operator action that would trigger the drift.

---

## 9. Rough fix size (if this diagnosis is confirmed)

Not attempted here per scope, but sized at a high level for planning:

- **Smallest defensive fix:** block `InitiateExternalReview` (and/or the "Send for External Review" UI) when `translation.NeedsRevalidation == true`, forcing a re-validation before a new external-review round can be sent. This closes the drift at its source without touching the three-source architecture. Small — one guard clause + one UI message, similar shape to the existing state-ineligibility messaging just added in `aa27513`.
- **More complete fix:** make `GetPortalContext` source its section list from the live `ToolboxTalkTranslation.TranslatedSections` (already positionally authoritative, per §5 source (c)) rather than from `TranslationValidationResults`, and have `SendExternalReviewDialog`'s checkbox list also read from the same source instead of the talk's raw `ToolboxTalkSection`s — collapsing three sources down to one. This is a real refactor (touches the reviewer's flag-highlighting data too, since `TranslationValidationResult.Flags` would need a different attachment point) — likely its own multi-chunk piece of work, not a quick patch.

Either way, this should go through the same scope-discipline process as prior chunks (stop and report if implementation surfaces something outside the stated fix) rather than being bundled into a "quick fix."
