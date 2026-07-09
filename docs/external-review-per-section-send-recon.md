# Send-Only-Chosen-Sections to External Reviewer — Recon

**Date:** 2026-07-09
**Branch:** transval
**Scope:** Read-only investigation. No application files, migrations, or data modified.
**Context:** Boss's item #4, verbatim: *"When sending a learning to a third party, they receive the full learning (I think this is what was originally requested), but we should now only send the sections chosen to be sent."*

---

## 1. Headline

**No per-section infrastructure exists — this is a full-stack change, small-to-medium scope, and it lands on top of a state machine that just changed shape (auto-apply, shipped 2026-07-08).**

Today, sending a translation to a third-party reviewer always sends **every section** of the talk's latest completed validation run, unconditionally. There is no concept anywhere in the domain model — entity, DTO, or UI — of a subset of sections being "chosen" for review. The admin-facing send dialog (`SendExternalReviewDialog.tsx`) collects only a reviewer email; its own description text ("X flagged words will be included in the review") already implies scoped content, but the backend it calls (`GetPortalContext`) ignores flags entirely and returns every section regardless of flag status. That mismatch between the dialog's copy and the actual behavior is itself evidence for the boss's "I think this is what was originally requested" — the product language was written as if scoping existed; the implementation never did it.

Good news on complexity: this week's auto-apply refactor (commits `28f12a1`, `a031ba0`, `3d7c84c`) is not hostile to a per-section model — in some ways it's already halfway compatible, because the submit-side validation gate never required full-section coverage. The complexity is concentrated on the **send side** (what the reviewer is shown) and on two design questions that ripple outward: what happens to non-chosen sections in the reviewer's view, and what "externally reviewed" means when it's no longer the whole translation.

---

## 2. Part 1 — Current send flow (verified at HEAD)

### `InitiateExternalReview` — `TranslationWorkflowService.cs:339-431`

Signature: `InitiateExternalReview(Guid talkId, string languageCode, string invitedEmail, Guid? explicitTenantId, CancellationToken ct)`.

- Takes **no section list, no filter, no selection of any kind.** The only inputs identifying "what's being reviewed" are `talkId` + `languageCode` — a whole-translation pointer, not a section-scoped one.
- Writes an `ExternalParticipantInvitation` row (line 370-384). `ContextPayload` (a free-form nullable string column) is populated with `JsonSerializer.Serialize(new { contextType = "TranslationReview", flaggedWordCount })` — a single aggregate number, not a section list. This is the only per-invitation payload field, and today it carries billing/estimation metadata only (see CLAUDE.md TransVal Decision 10), nothing about scope.
- **No "sections to review" list is stored anywhere.** Confirmed by reading `ExternalParticipantInvitation.cs` in full (11 properties, none section-related) and `WorkflowReview.cs` (the sibling entity for the reviewer's response — also has no section-list field, only `EditedContent`, a JSON blob of *whatever the reviewer typed*, not a record of what they were *offered*).

### Invitation email — `InitiateExternalReview` lines 392-422

Builds `portalUrl = "{baseUrl}/external-review/{rawToken}"` and calls `IEmailService.SendExternalReviewInvitationEmailAsync`. The token is opaque (`Guid.NewGuid("N")`, SHA-256 hashed for storage) and carries no section information — the portal derives everything else at load time from the token alone.

### Reviewer's portal loader — `GetPortalContext`, `TranslationWorkflowService.cs:656-747`

- Backend endpoint: `GET /api/external-review/{token}` → `ExternalReviewController.GetPortalContext` → this method.
- When `portalStatus == "Active"` (lines 696-731): loads the **latest completed** `TranslationValidationRun` for `(talkId, languageCode)`, then loads **all** `TranslationValidationResult` rows for that run (`.Where(r => r.ValidationRunId == run.Id)`, no further filter), ordered by `SectionIndex`, and maps every one of them into `ExternalReviewSectionDto`.
- **There is no filtering by flag presence, flag severity, or any other criterion.** `Sections = results.Select(...).ToList()` — the whole result set, always.
- Returned DTO (`ExternalReviewPortalDto`) carries `Sections: List<ExternalReviewSectionDto>` with no `isEditable`/`isChosen`/similar discriminator field.

### Reviewer's portal UI — `web/src/app/external-review/[token]/page.tsx`

- Line 328: `{portalData.sections.map((section) => ( ... ))}` — renders every section returned by the backend, each with a read-only source panel and an **editable** `Textarea` for the translation (lines 347-361).
- On load (lines 107-112), `editedSections` state is pre-populated with every section's original translated text — so the reviewer sees, and can edit, every section, always. There is no concept of "context-only, not editable" sections in this component today.

### Admin send dialog — `SendExternalReviewDialog.tsx`

- Only field: reviewer email (lines 56-66). No section picker, no checkbox list, nothing that lets an admin choose a subset before sending.
- Description copy (lines 48-54): *"Send the `{languageName}` translation to a third-party reviewer. `{flaggedWordCount}` flagged word(s) will be included in the review... [or] There are no flagged words; the reviewer will see the full translation."* This text asserts a scoping behavior ("flagged words will be included") that the backend does not implement — `flaggedWordCount` is purely an informational/billing number (see `ComputeFlaggedWordCountAsync`, `TranslationWorkflowService.cs:912+`), computed from merged flag spans across the run, and has **zero effect** on which sections `GetPortalContext` returns. This is the clearest textual evidence that "only send what's flagged/chosen" was the original mental model, never the shipped behavior.

### Submit path — `SubmitExternalReview` (auto-apply, shipped 2026-07-08), `TranslationWorkflowService.cs:433-546`

- Reviewer's submission (`editedContent`, a JSON array of `{sectionIndex, translatedText}`) is validated by `ValidateExternalReviewSubmissionAsync` (lines 989-1046) with four gates:
  1. Non-empty submission.
  2. **Every submitted `SectionIndex` must be in range against the live `ToolboxTalkTranslation.TranslatedSections`** — not against "the sections the reviewer was shown," and **not required to cover every section**. A submission touching 2 of 8 sections passes this gate today with zero code changes needed.
  3. Every submitted section's text non-empty.
  4. Coarse XSS denylist.
- On acceptance, edits are merged section-by-section into the live translation (lines 497-504) and `LastExternalReviewedAt`/`LastExternalReviewedBy` are stamped on the **`ToolboxTalkTranslation` row** — i.e., at the whole-translation level, not per-section.

**Key finding for scoping purposes:** because Gate 2 only checks range-against-live-translation and never required full-section coverage, **the submit-side validation logic does not need to change to support "only some sections were sent."** The gap is entirely upstream — what `GetPortalContext` returns and what the portal UI renders — plus a new gap this creates (see §4, Q10 edge case) that Gate 2 as written does **not** currently guard against: nothing stops a submission from including a `SectionIndex` the reviewer was *never shown*, since Gate 2's authority is "is this index valid for the translation," not "was this section actually offered to this invitation."

---

## 3. Part 2 — Existing per-section infrastructure

### What exists

- **`TranslationFlag`** (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/TranslationFlag.cs`) — the closest thing to a "this section needs attention" signal. Attached to `TranslationValidationResult` via `.Flags`, populated automatically by the validation engine (Phase 2a section-level fallback + Phase 2b phrase-level, per `TRANSLATION_WORKFLOW_DESIGN.md` §9). Carries `StartOffset`/`EndOffset`/`Severity`/`Reason` — used today purely for **inline highlighting** in both the internal `ReviewScreen` and the external portal's `FlaggedText` component. It is never used to filter *which sections are sent* — `GetPortalContext` loads `.Include(r => r.Flags)` for display purposes only, then maps every section regardless of whether `Flags` is empty or not.
- **`flaggedWordCount`** — an aggregate, run-wide number computed at invitation time (§2 above). Informational/billing only; not a filter.

### What does NOT exist

- No boolean flag anywhere resembling `NeedsReview`, `IsFlagged`, `UnderReview`, `SelectedForExternalReview`, etc. on `ToolboxTalkSection`, `TranslationValidationResult`, or any other entity. Confirmed by repo-wide grep for these and similar identifier patterns across `.cs` files — zero matches outside this recon and the Part 1 dialog copy discussed above.
- No admin UI surface — anywhere in `web/src/features/toolbox-talks/**` — lets an admin mark specific sections for review before initiating. The only pre-send admin-facing screens are the per-language `TranslationWorkflowPanel` (whole-language actions: Translate/Validate/Review/Send/Cancel) and the internal `ReviewScreen` (per-section **accept/edit/retry** decisions on `TranslationValidationResult`, unrelated to external-review scoping — those decisions gate whether a language can even reach `ReviewerAccepted`/`Validated`, the precondition for sending, but they don't mark sections for inclusion in what gets sent).
- No junction table, no `ExternalParticipantInvitationSection` or similar, exists in the schema.

### Conclusion for Part 2

**Zero per-section "chosen for review" state exists today, in any form — automatic or admin-driven.** `TranslationFlag` is the nearest analog (an automatic, validation-derived quality signal at section granularity) but it is structurally a *highlighting* mechanism, not a *selection* mechanism, and it is completely disconnected from the send path. Building "send only chosen sections" is new work end-to-end: new storage, new DTOs, and — depending on which interpretation of "chosen" the boss means (see §5) — possibly a new admin UI affordance.

---

## 4. Part 3 — Design implications (enumerated, not decided)

### Q9 — What does the reviewer see for sections NOT chosen?

| Option | Backend shape | Frontend shape | Trade-off |
|---|---|---|---|
| **A. Nothing** | `GetPortalContext` filters `results` to the chosen `SectionIndex` set before mapping (small, surgical change to the `.Where(...)` at line 710-715 or a post-filter on `results`) | `page.tsx` unchanged — it already just renders whatever `portalData.sections` contains | Simplest change. Reviewer loses talk-level context (may not know how their section fits the whole). Naturally prevents the Q10 edge case below, since the reviewer's `editedSections` state can never contain a non-offered index. |
| **B. Read-only context** | `ExternalReviewSectionDto` gains an `IsEditable` (or `IsChosen`) bool; `GetPortalContext` still returns all sections but flags which are editable | `page.tsx` needs a conditional: chosen sections render the current editable `Textarea`; non-chosen sections render read-only text (or the source-only half of the current grid) | Reviewer keeps full context. More surface area: DTO change, submit-payload still needs Gate 2 (or a new gate) to reject edits to non-editable sections defensively, since a technical reviewer could still POST an edit for a read-only section (see Q10). |
| **C. Placeholder/stub** | Same DTO shape as B, but non-chosen sections carry only `SectionTitle`, no `OriginalText`/`TranslatedText` | `page.tsx` renders a collapsed "not part of this review" row per stub | Middle ground — reviewer sees the talk's structure without seeing (or risking leaking) content outside the reviewed scope. Marginally more frontend work than A or B. |

Recon does not recommend one — this is a product/UX call the boss needs to make, and it interacts with how "chosen" gets decided (§5).

### Q10 — Auto-apply behavior under a partial-send model

- **Happy path:** unchanged from today's mechanics. The reviewer submits edits for whichever sections they were given (a strict subset under Option A/B/C above); `SubmitExternalReview` merges each into `TranslatedSections` by index; non-chosen sections are untouched. This is **already how the code behaves** — no new merge logic needed.
- **Gate 2's semantics do need to tighten, though.** Today Gate 2 asks "is this index valid against the live translation" (whole-translation range check). Under a chosen-sections model, the correct question becomes "is this index a member of the set this specific invitation was scoped to" — a stricter, invitation-specific check. That set does not exist anywhere today (§3), so it would need to be persisted at `InitiateExternalReview` time — most naturally as an extension of the existing `ContextPayload` JSON blob (a designed-for-future-use field per `TRANSLATION_WORKFLOW_DESIGN.md` §13 item 3 — the discriminator/payload pattern is explicitly meant to carry future per-invitation context) — and read back during `ValidateExternalReviewSubmissionAsync`.
- **The specific edge case the prompt asks about — reviewer's submission covers ALL sections but only some were "sent":** confirmed as a real, currently-unguarded gap. `ExternalReviewController.Submit` is `[AllowAnonymous]`; nothing about the current request/response cycle cryptographically or structurally ties "what GetPortalContext returned" to "what SubmitExternalReview will accept." A reviewer's browser only ever populates `editedSections` from what it rendered, so the *normal* client can't produce this case — but a raw HTTP client using the same (leaked, guessed, or shared) token could submit edits for any in-range `SectionIndex`, chosen or not, because Gate 2 has no per-invitation scope concept to check against. **This is a genuine, newly-surfaced design requirement**, not something inferable from the original brief — flagged here because building "send only chosen sections" without also tightening Gate 2 would produce a feature that *looks* scoped in the UI but is not actually enforced server-side.

### Q11 — Provenance implications

- `LastExternalReviewedAt`/`LastExternalReviewedBy` (this week's addition, migration `20260708101748_AddExternalReviewProvenanceToTranslations`) live on **`ToolboxTalkTranslation`** — one row per (talk, language), i.e. whole-translation granularity. Confirmed via the entity, the migration, and `TranslationWorkflowPanel.tsx:268-273`, which renders *"Externally reviewed by `{email}` on `{date}`"* under the language name — a per-language badge, not a per-section one.
- Under partial review, stamping the **whole translation** as externally reviewed becomes actively misleading if (say) 2 of 8 sections were reviewed — the panel's badge, and the re-translate warning it feeds (`overwriteWasExternallyReviewed`, lines 101/173, and the AlertDialog copy at lines 529-535: *"was reviewed and edited by a trusted external reviewer... discard those edits"*) would overstate coverage.
- Three non-exclusive options, enumerated not decided:
  1. **Keep the whole-translation flag as-is**, treat it as "this language has had at least one external review round," and accept the imprecision. Zero schema change. Weakest signal.
  2. **Add per-section provenance** — e.g., new nullable `LastExternalReviewedAt`/`By` (or a `SectionIndex → reviewedAt/by` map) on `TranslationValidationResult`, or a dedicated join table. Real schema change, but gives the re-translate warning (and any future diff view) precise, per-section truth.
  3. **Derive it from existing data without a schema change** — `WorkflowReview.EditedContent` already contains the exact list of `{sectionIndex, translatedText}` the reviewer touched, per submission. A read path could parse the most recent accepted `WorkflowReview` row and answer "which sections were externally reviewed" on demand, without adding columns. Cheapest option that still gets per-section truth, at the cost of a query/parse instead of a direct column read.

### Cost-aware re-translate warning under partial review

The current warning (`TranslationWorkflowPanel.tsx:529-535`) already branches on `overwriteWasExternallyReviewed`, a boolean derived from the whole-translation flag. Under a partial model, this copy would need to know **which** sections were reviewed to be accurate (e.g., "sections 2 and 5 were reviewed by a trusted external reviewer — re-translating will discard those edits"). That requires whichever of the §Q11 options is chosen to expose section-level granularity to this specific screen, not just a yes/no.

### Second round — "send section A, later send section B"

Not evaluated in the original brief but surfaced by tracing the state machine: today, **only one external-review round can be in flight per (talk, language) at a time**, enforced structurally (not by a DB constraint) — `InitiateExternalReview` only proceeds from `Validated`/`ReviewerAccepted`; once initiated, the language sits in `AwaitingThirdParty` until the reviewer responds (→ `ThirdPartyReviewed` under auto-apply) and an admin clicks Accept (→ `Accepted`). There is no path back from `ThirdPartyReviewed`/`Accepted` to a state that permits a **second** `InitiateExternalReview` call without a full re-translate (which itself discards prior edits — see the warning above). So "send section A now, section B later" is not currently supported even conceptually, independent of the per-section-send question — it would need either a new state-machine allowance (re-open `AwaitingThirdParty` for the same language) or an explicit product decision that a second round always means a fresh full re-translate cycle.

---

## 5. Product questions for the boss

These need his decision before an implementation prompt can be scoped — the recon deliberately does not guess:

1. **Who selects the sections, and how?** Three plausible readings of "chosen":
   - **Admin manually selects** specific sections at send time (needs new UI — a checkbox list in `SendExternalReviewDialog` or a preceding step).
   - **Automatic, based on validation state** — e.g., only sections carrying a `TranslationFlag` (Review/Fail outcome or phrase-level flag) are sent, since those are the ones the AI validation engine already thinks need human eyes. Needs no new admin UI, just a filter in `GetPortalContext`.
   - **Something else** — e.g., admin selects at the individual `ReviewScreen` section-decision stage (repurposing the existing accept/edit/retry per-section flow to also mean "and send this one externally").
2. **What does the reviewer see for sections not chosen?** Nothing / read-only context / placeholder (§4 Q9) — each has different cost and different reviewer experience.
3. **Does "externally reviewed" provenance apply to the whole translation, per-section, or is the current whole-translation flag acceptable with a caveat?** (§4 Q11) — this also determines whether the freshly-shipped `LastExternalReviewedAt`/`By` columns need a follow-up migration.
4. **Does the cost-aware re-translate warning (shipped this week) still apply as worded, or does it need per-section precision?** Directly downstream of Q3.
5. **Second-round sends.** If an admin sends section A for review, completes that round, and later wants section B reviewed too — is that a second invitation (needs the state-machine allowance described above), an extension of the first (needs the invitation to stay open across multiple reviewer visits, a bigger behavioral change), or explicitly unsupported (must always be a fresh full re-translate + full external-review cycle)?
6. **Should Gate 2 be tightened to enforce "reviewer can only submit edits for sections actually offered to them"?** Recon recommends yes regardless of which of Q1-Q5 the boss picks (§4 Q10), since without it the UI-level scoping would be cosmetic only.

---

## 6. Recommended next step

**Send the product questions in §5 to the boss before writing an implementation prompt.** This is not a case where the answers are safely inferable from context — Q1 in particular (who/what decides "chosen") changes the shape of the work from "backend filter + no new UI" (automatic/flag-driven) to "new admin selection UI + backend filter + storage for the chosen set" (manual). The two are meaningfully different scopes, and guessing wrong means throwaway work in either the UI or the data model.

Suggested framing for the boss, given this recon: *"Sending currently always includes every section — there was never a filter, which matches what you're seeing. Before I build the fix, I need to know: should the sections be picked automatically (only the ones our AI validation already flagged as needing review) or should you/an admin manually choose them per send? And when a reviewer only touches some sections, should 'externally reviewed' still describe the whole translation, or should we track it per-section?"*
