# External Review Per-Section-Editable — Implementation Shape Recon

**Date:** 2026-07-09
**Branch:** transval
**Scope:** Read-only investigation. No application files, migrations, or data modified.
**Context:** Boss's item #4, design decisions locked (see prompt). Builds on `docs/external-review-per-section-send-recon.md`, which established that no per-section infrastructure exists today.

---

## 1. Headline

**Scope confirmed. 6 chunks recommended, one (data model) safely ship-independent; the rest are behaviourally coupled with one hard sequencing constraint: submit-side Gate 2 tightening (Chunk D) must land no later than the reviewer read-only UI (Chunk C), never after — otherwise the read-only rendering is cosmetic-only, exactly the gap the prior recon flagged.**

The refactor touches five layers (storage, send dialog, portal load, reviewer UI, submit validation) plus one workflow-state guard relaxation for second rounds. None of it requires new tables — the codebase's dominant pattern for "a list of X against one entity" is a plain JSON string column with manual `System.Text.Json` serialize/deserialize, and that pattern extends cleanly to both the editable-section-selection storage and the per-section provenance storage. The one piece of new architectural friction is that `TranslatedSectionEntry` — the class backing every section inside `ToolboxTalkTranslation.TranslatedSections` — is currently a `private sealed` nested class inside `TranslationWorkflowService`, and per-section provenance requires promoting it to a shared model touched by every read/write site of that JSON blob.

`InitiateExternalReview` and its siblings are **not** CQRS/MediatR commands — they're direct calls on `ITranslationWorkflowService`, with validation done via inline guard clauses (`Result.Fail(...)` returns), not FluentValidation. This is a deliberate, consistent pattern across the whole workflow service, not an oversight — new validation (editable-index range checks, Gate 2 tightening) should follow that same idiom rather than introducing new CQRS machinery for this one feature.

---

## 2. Data model decision (Part 1)

### 2.1 Editable-section-selection storage

**Decision: a new dedicated nullable JSON string column on `ExternalParticipantInvitation`** — `EditableSectionIndicesJson` (`string?`, `HasColumnType("text")`), holding a `System.Text.Json`-serialized `List<int>`. `null` = "no restriction, all sections editable" (this is what preserves full-scope review as the default — the send dialog defaults to all sections selected, and when all are selected the persisted value can simply be `null` rather than an explicit full list, though either is workable).

**Reasoning:**
- This is exactly the codebase's dominant, consistent pattern for "a list of ids/indices belonging to one parent row." Precedents: `ContentCreationSession.TranslationJobIds` (`List<string>`, JSON string column), `ContentCreationSession.ValidationRunIds` (`List<Guid>`, same pattern), `WorkflowReview.EditedContent` (`List<{sectionIndex, translatedText}>`, same pattern), `ToolboxTalkTranslation.TranslatedSections` itself. There is **no** existing use of EF value converters (`HasConversion`) for collections, no `OwnsMany`, and no junction table anywhere in this codebase for a simple index/id list — `HasConversion` here is reserved for enum-to-string/int mapping only.
- **Rejected alternative — extending `ContextPayload`:** the prior recon's §4 Q10 suggested this as the natural extension point, and it's technically workable, but `ContextPayload` today is schema-less: written via an anonymous object (`JsonSerializer.Serialize(new { contextType = "TranslationReview", flaggedWordCount })`, `TranslationWorkflowService.cs:381`) and read back defensively with raw `JsonDocument.Parse` inside a try/catch (`ParseFlaggedWordCount`, lines 893-908). Nothing about `ContextPayload` is currently typed or validated on write. The editable-section set, by contrast, is security-relevant (Gate 2 will enforce against it) and deserves a real C# type with compile-time-checked read/write sites rather than living inside an untyped, best-effort-parsed blob. A dedicated column costs one migration line and buys type safety for a field the submit-validation path depends on.
- **Rejected alternative — junction table** (`ExternalParticipantInvitationEditableSection`): no precedent anywhere in this codebase for a table whose sole purpose is "a set of indices belonging to one parent row with no independent query/lifecycle needs." Every genuinely relational per-item entity in this module (`TranslationFlag`, `ScheduledTalkSectionProgress`) exists because the child rows have their own mutable state and lifecycle (severity, offsets, `IsRead`, `TimeSpentSeconds`) — not applicable here.

### 2.2 Per-section provenance storage

**Decision: extend the (promoted) `TranslatedSectionEntry` model with nullable `ReviewedAt`/`ReviewedBy` fields, inside the existing `TranslatedSections` JSON blob. Keep the existing whole-translation `ToolboxTalkTranslation.LastExternalReviewedAt`/`LastExternalReviewedBy` columns as a coarse, derived "has this language ever had any external review" aggregate — do not drop them.**

**Reasoning, and the trade-off named honestly per the task's skepticism note:**
- The JSON-blob constraint is real. `TranslatedSectionEntry` today is `{ SectionId (Guid), Title (string), Content (string) }` — nothing else — and it is a **`private sealed` class nested inside `TranslationWorkflowService`** (`TranslationWorkflowService.cs:1059-1064`), not a shared/public model. Adding provenance fields here means: (a) promoting the class to somewhere shared (e.g. `Application/Models/` or similar, matching how other cross-cutting JSON-blob shapes are organized), and (b) auditing **every** read/write site of `TranslatedSections` to ensure round-trip serialization preserves the new fields rather than silently dropping them on the next deserialize→mutate→reserialize cycle. Known sites: `TranslationWorkflowService.cs:497,502,1016` (submit-merge and Gate 2), and at least `AiSectionGenerationService.cs` and `GetMyToolboxTalkByIdQueryHandler.cs` (flagged by research as other deserialization sites — **full enumeration of every site was not completed in this recon and should be a first implementation step**, see §9 Risks).
- **Rejected alternative — a new parallel `SectionProvenance` table** keyed by `(TranslationId, SectionIndex/SectionId, ReviewedAt, ReviewedBy)`: more code, a new migration, a new EF entity/configuration, and — critically — no existing precedent for a provenance table paired 1:1 with a JSON blob's array elements. The nearest analog, `TranslationValidationResult` (also keyed by `SectionIndex`), is populated by a completely different, transient pipeline (each validation run creates fresh rows) and is explicitly **not** the durable source of truth for translated content — keeping a separate provenance table in sync with `TranslatedSections` edits (manual edits, re-translation resets, reordering) would be genuinely new complexity with no existing pattern to lean on.
- **Rejected alternative — keep only the whole-translation flag, accept the imprecision:** rejected because it directly contradicts the locked design decision ("each section carries its own `LastExternalReviewedAt`/`LastExternalReviewedBy`").
- **Why keep the whole-translation columns too, rather than drop them:** they're consumed today by `GetState`/`TranslationWorkflowStateDto` (`TranslationWorkflowService.cs:126-127`) and by the badge/warning in `TranslationWorkflowPanel.tsx`. Recomputing "most recent reviewer across any section" as a derived value at read time (rather than a stored column) is possible and arguably cleaner, but keeping the two existing scalar columns as a cheap, already-migrated, already-indexed-nowhere-but-fine aggregate avoids forcing every consumer to migrate to the new per-section list in the same chunk — it lets Chunk E (read-side) be scoped independently from Chunk D (write-side). This is explicitly a phasing convenience, not a permanent architecture; flag for the boss that the two whole-translation columns become **redundant, derivable data** once per-section provenance exists, and could be removed in a later cleanup once all consumers read the per-section list directly.

### 2.3 Migration shape

Two schema changes, no new tables:

1. **`AddColumn<string>`** — `EditableSectionIndicesJson` on `ExternalParticipantInvitations` (schema `toolbox_talks`), `type: "text"`, `nullable: true`. Mirrors the shape of the existing `ContextPayload` column addition style.
2. **No schema migration needed for per-section provenance** — it lives entirely inside the existing `TranslatedSections` JSON string column on `ToolboxTalkTranslations`, which already has no fixed schema enforced by the database (it's `text`/`character varying` holding arbitrary JSON). This is a pure application-code change (new fields on the promoted `TranslatedSectionEntry` model); old rows deserialize fine with the new fields defaulting to `null` (no section has ever been reviewed) since `System.Text.Json` tolerates missing properties on deserialize.
3. **Optional, deferred:** a future `DropColumn` migration removing `ToolboxTalkTranslation.LastExternalReviewedAt`/`LastExternalReviewedBy` once every consumer reads per-section data directly (see §2.2). Not part of this feature's migration set — noted for later cleanup only.

Both new/changed columns are simple nullable scalars with no FKs, no indexes, no defaults beyond nullability — consistent with the low-risk shape of the migration that added the whole-translation provenance columns three days prior (`20260708101748_AddExternalReviewProvenanceToTranslations`), which this recon read in full as a template.

---

## 3. Send flow changes (Part 2)

### 3.1 Send dialog UI

File: `web/src/features/toolbox-talks/components/SendExternalReviewDialog.tsx` (currently 87 lines).

Current props: `open`, `onOpenChange`, `onConfirm`, `isLoading`, `flaggedWordCount: number`, `languageName: string`. No section data of any kind reaches this component today.

**Changes needed:**
- New props to carry section data in: either the raw list of `{ sectionIndex, sectionTitle, flagCount }` (derived from a validation run), or the whole `ValidationRunDetailDto.Results` array already fetched elsewhere.
- A checkbox list, one row per section (title + optional flag-count badge), defaulting to **all checked** (preserves today's full-scope behaviour as the default, per the locked decision).
- "Select all" / "Deselect all" affordances above the list.
- Client-side validation: disable the confirm button (or show a form error) when zero sections are checked — "at least one section must be selected" per the locked decision.
- The existing flagged-words copy (`"{n} flagged word(s) will be included in the review"`) is misleading today — it implies scoping that never existed. Recommend rewording to something that doesn't assert an inclusion/exclusion behaviour tied to flags (e.g. drop the "will be included" framing entirely, or reword to purely informational: "This translation has {n} flagged word(s)" with the section list doing the actual scoping communication). This is copy, not logic — low risk, but should not ship unchanged once section selection exists, or the dialog will contain two contradictory claims about what's being sent.

**Data-sourcing gap, not a new endpoint:** `SendExternalReviewDialog` and its parent `TranslationWorkflowPanel` currently have no section-level data in scope — `TranslationWorkflowPanel` only holds `TranslationWorkflowStateDto[]` (`useWorkflowStates`), a whole-language DTO with no `Sections` collection. However, `TranslationWorkflowStateDto.LastValidationRunId` (already present) is exactly the pointer needed, and `useValidationRun(talkId, runId)` (existing hook, already used by `ReviewScreen`/`ValidationRunDetailView`) already returns `ValidationRunDetailDto.Results: List<ValidationResultDto>` with `SectionIndex`/`SectionTitle`/`Flags` per section. **No new backend endpoint is required** — this is new frontend wiring only: thread `lastValidationRunId` down, call `useValidationRun` (likely gated on dialog-open, or lifted into the panel), and pass the resulting section list into the dialog.

### 3.2 `InitiateExternalReview` command and handler

No CQRS layer exists here — see §1. Changes are direct:

- **Request DTO** (`InitiateExternalReviewRequest`, `ToolboxTalksController.cs:2826-2829`): add `IReadOnlyList<int>? EditableSectionIndices { get; init; }`. `null`/omitted preserves today's behaviour (all sections editable) — this is what makes "full-scope reviews preserved as default" free at the API contract level.
- **Frontend API function** (`initiateExternalReview`, `web/src/lib/api/toolbox-talks/toolbox-talks.ts:663-671`): add the new parameter, threaded from the dialog's confirm handler through `TranslationWorkflowPanel.handleSendForExternalReview` and `useInitiateExternalReview`.
- **Service interface/impl** (`ITranslationWorkflowService.InitiateExternalReview`, impl at `TranslationWorkflowService.cs:339-431`): add `IReadOnlyList<int>? editableSectionIndices = null` parameter.
- **Validation:** follow the existing inline-guard-clause idiom used throughout this method (e.g. the state guard at lines 353-356), not FluentValidation — this service has no injected validator and no CQRS pipeline to hang one off. Range-check each index against the section count of the run/translation being sent (mirroring Gate 2's existing shape: `edits.Any(e => e.SectionIndex < 0 || e.SectionIndex >= sections.Count)`), returning `Result.Fail(..., FailureCode.WorkflowSubmissionInvalid)` (or a new, more specific `FailureCode`) on violation. Also reject an explicitly-empty (non-null, zero-length) list, matching the dialog-side "at least one section" rule.
- **Persistence:** serialize the validated list and write it to the new `EditableSectionIndicesJson` column when constructing the `ExternalParticipantInvitation` row (alongside the existing `ContextPayload` write at line ~370-384).

### 3.3 Invitation URL and portal load

**The portal load endpoint must return the editable set — there is no separate channel.** The external-review portal is a public, token-authenticated route (`GET /api/external-review/{token}` → `GetPortalContext`) that is the *only* thing the reviewer's browser calls before rendering; there's no secondary "invitation info" lookup the frontend could use to derive editability independently. So the editable set must be embedded in the response from `GetPortalContext`.

**DTO change:** add `bool IsEditable { get; init; }` to `ExternalReviewSectionDto` (currently `SectionIndex`, `SectionTitle`, `OriginalText`, `TranslatedText`, `Flags`), computed per-section inside `GetPortalContext`'s mapping loop (`TranslationWorkflowService.cs:717-730`) by checking `editableIndices is null || editableIndices.Contains(r.SectionIndex)`, where `editableIndices` is deserialized from `invitation.EditableSectionIndicesJson` (parsed once per request, same call site that already reads `invitation.ContextPayload` for the flagged-word count). A per-section boolean is preferred over a top-level `List<int>` on `ExternalReviewPortalDto` because it's directly consumable by the frontend's existing per-section `.map()` loop without a second lookup structure — see §4.1.

---

## 4. Reviewer UI changes (Part 3)

File: `web/src/app/external-review/[token]/page.tsx` (the only file in this route tree — no sibling components; `DeclineConfirmationDialog.tsx` under `features/external-review/` is unrelated).

### 4.1 Rendering — editable vs read-only

Current behaviour (verified in full): every section renders identically — a read-only source column (`FlaggedText` over `section.originalText`) and an **always-editable** `Textarea` over `section.translatedText`, seeded into `editedSections` state unconditionally for every section at load time (lines 107-113). There is no branching today.

**Changes:**
- Branch per section on the new `section.isEditable` field.
- **Editable sections:** unchanged — current `Textarea` + `onChange` behaviour.
- **Read-only sections:** render the translation panel as non-interactive. No existing dedicated "read-only content block" component exists in this codebase (confirmed — no rendered "Read-only" badge anywhere, despite the phrase appearing in code comments in several files). Recommend composing from existing primitives already used elsewhere for this exact situation: a `<Textarea disabled className="bg-muted" />` (mirrors `employee-form.tsx:199-204`'s disabled-input + `bg-muted` convention, and `Textarea`'s base classes already include `disabled:cursor-not-allowed disabled:opacity-50`) plus a `Badge variant="secondary"` (the codebase's existing "neutral/inert state" badge variant, e.g. used for a "Pending" state in `ValidationSectionCard.tsx:390-392`) labelled "Read-only" or "Not part of this review" next to the section title.
- Do **not** seed `editedSections` for non-editable indices (or, if seeded for display purposes, exclude them from the submit payload regardless — see §4.3). This avoids the current bug-shaped behaviour where every section's text is present in `editedSections` whether touched or not.

### 4.2 Passive flag highlighting — reuse `FlaggedText` as-is, no extraction needed

`FlaggedText` (`web/src/features/toolbox-talks/components/create-wizard/steps/validate/FlaggedText.tsx`) is a small, stateless, presentational component (`{ text, flags }` props only — no auth context, no admin-only dependency). It is **already imported directly by the reviewer portal today** (`page.tsx:17`, applied to the source/original panel only, via a local `adaptFlags()` helper that maps `ExternalReviewFlagDto[]` → the component's expected `TranslationFlag[]` shape). The admin-side `ReviewScreen`/`ValidationSectionCard` uses the exact same file/component with the exact same visual treatment (severity → colour class map, tooltip-on-hover reason).

**Conclusion: no duplication or extraction work required.** The locked design decision ("passive highlighting on all sections regardless of editability, same visual signal as admin") is satisfiable by continuing to apply `FlaggedText` to every section's source/original panel unconditionally — which is already what happens today, since there's currently no concept of editability to gate it on. The only new requirement introduced by this feature is to make sure the *new* editable/read-only branching (§4.1) does **not** accidentally wrap the flag highlighting in an `isEditable` condition — it should remain applied uniformly. One point to verify during implementation, not resolved by this recon: whether `ValidationSectionCard`'s admin-side `FlaggedText` usage highlights the *original* or *translated* text in all its call sites (the "displayText" variable name found in that component is ambiguous without deeper tracing) — worth a quick confirm so the reviewer portal's highlighting target matches the admin's exactly, per the locked "same visual signal" requirement.

### 4.3 Submit payload — filter client-side, enforce server-side (defense in depth)

Current behaviour: `handleSubmit` (lines 130-153) serializes `Object.entries(editedSections)` — **every** section present in state, regardless of whether it was actually edited, since state is seeded for all sections at load.

**Recommended approach — both ends filter, neither trusts the other:**
- **Client:** construct the submit payload only from `section.isEditable === true` entries (whether by never seeding non-editable sections into `editedSections`, or by filtering at submit time). This keeps payload size honest and avoids the client claiming edits for text the reviewer never had access to edit.
- **Server (Gate 2, §5.2):** independently reject any submission containing a `SectionIndex` outside the invitation's persisted editable set, regardless of what the client sent. This is the actual security boundary — the external-review submit endpoint is `[AllowAnonymous]`, token-authenticated only, so a raw HTTP client bypassing the rendered UI entirely must be defended against server-side. Client-side filtering alone is cosmetic (this is precisely the gap the prior recon's Q10 flagged).

---

## 5. Auto-apply and provenance updates (Part 4)

### 5.1 `SubmitExternalReview` handler changes

`TranslationWorkflowService.cs:433-546`. Today: loads the invitation (for token/expiry validation), merges edits into `TranslatedSections` by index (lines 497-502), then stamps `translation.LastExternalReviewedAt`/`LastExternalReviewedBy` **once, at the translation level** (lines 503-504), regardless of how many sections were touched.

**Changes:**
1. The invitation is already loaded in this method by the time Gate 2 runs — no new load needed, just threading it (or its editable-set) into the validator (see §5.2).
2. Section merge logic (lines 499-502) is unchanged in shape — same `sections[edit.SectionIndex].Content = edit.TranslatedText` pattern.
3. **Provenance stamping moves inside the merge loop**, per-section: for each `edit` actually merged, set `sections[edit.SectionIndex].ReviewedAt = DateTime.UtcNow` and `.ReviewedBy = invitation.InvitedEmail` on the (promoted, now-provenance-bearing) `TranslatedSectionEntry`.
4. Whole-translation `LastExternalReviewedAt`/`LastExternalReviewedBy` (§2.2 decision: kept as a coarse aggregate) — continue stamping these too, unconditionally on any accepted submission, exactly as today. This keeps existing consumers (`GetState`) working unchanged until they're explicitly migrated to per-section data in Chunk E.
5. `WorkflowEvent` emission (`ExternalReviewSubmitted`) — unchanged.

### 5.2 Gate 2 tightening

Current Gate 2 (`ValidateExternalSubmissionAsync` → `ValidateExternalReviewSubmissionAsync`, lines 989-1046) checks only `e.SectionIndex < 0 || e.SectionIndex >= sections.Count` against the **live translation's section count**, looked up fresh by `(talkId, languageCode)` — it never touches the invitation at all. Confirmed structurally: the caller passes only `invitation.TargetEntityId`/`invitation.TargetEntitySubKey` into this method today, never the invitation object or its `ContextPayload`/new `EditableSectionIndicesJson`.

**Change:** thread the invitation (or specifically its parsed `EditableSectionIndices`) into `ValidateExternalReviewSubmissionAsync`, and add a second check alongside the existing range check:

```
if (invitation.EditableSectionIndicesJson is not null) {
    var editable = JsonSerializer.Deserialize<List<int>>(invitation.EditableSectionIndicesJson);
    if (edits.Any(e => !editable.Contains(e.SectionIndex)))
        return Result.Fail("One or more sections were not part of this review invitation.", FailureCode.WorkflowSubmissionInvalid);
}
```

`null` editable set (full-scope invitation) skips this check entirely — no behaviour change for today's default case. This closes the gap the prior recon identified: without it, a raw HTTP client with a valid token could submit edits for sections the UI never rendered as editable.

**Test case needed:** a submission containing one in-range-but-non-editable `SectionIndex` must be rejected, distinct from the existing "out of range entirely" rejection — these are two different failure reasons and probably deserve either two `FailureCode` values or at least two distinguishable messages, since "your submission is stale" (today's message) and "you tried to edit something you weren't given" are different problems for a caller to reason about.

### 5.3 Provenance read paths — every consumer, and what changes

| # | File:line | Today | Under per-section provenance |
|---|---|---|---|
| 1 | `TranslationWorkflowService.cs:126-127` (`GetState`) | Reads two scalars off `ToolboxTalkTranslation`, maps to `TranslationWorkflowStateDto.LastExternalReviewedAt/By` | Keep the two scalars (aggregate, unchanged read) **and** add a new field, e.g. `IReadOnlyList<SectionReviewProvenanceDto> ReviewedSections`, built by deserializing `TranslatedSections` and projecting entries with non-null `ReviewedAt` |
| 2 | `TranslationWorkflowStateDto.cs:18-19` | Two scalar fields | Add the new list field alongside (non-breaking addition) |
| 3 | `web/src/types/workflows.ts:31-32` | Matching TS scalar fields | Add matching TS array type |
| 4 | `TranslationWorkflowPanel.tsx:268-273` (badge) | `"Externally reviewed by {by} on {date}"`, gated on `dto?.lastExternalReviewedAt` | See §5.4 for recommended copy |
| 5 | `TranslationWorkflowPanel.tsx:173` (`overwriteWasExternallyReviewed` derivation) | `!!stateByCode.get(languageCode)?.lastExternalReviewedAt` — boolean presence check | Needs to become "does the section list about to be discarded contain any reviewed section" — see §5.4 |
| 6 | `TranslationWorkflowPanel.tsx:526-543` (re-translate warning copy) | Two-branch copy, binary reviewed/not-reviewed | Needs per-section count in the copy — see §5.4 |
| 7 | Integration tests: `TranslationWorkflowServiceTests.cs:260-261,1047-1048,1098-1099,1285`; `ExternalReviewControllerTests.cs:344-345`; `ToolboxTalksControllerWorkflowActionsTests.cs:260-261` | Assert on the two scalar columns | Need new/updated assertions once stamping moves per-section (scalars are still stamped per §5.1.4, so existing assertions likely still pass unchanged — but new tests should assert the per-section fields too) |

### 5.4 Copy recommendations (flagged as a product/UX call, not purely mechanical)

- **Badge:** recommend **"3 of 10 sections externally reviewed · most recent 9 Jul 2026"** — precise enough to be honest about partial coverage (the core problem being fixed), compact enough for a per-language row. A full per-section reviewer/date breakdown is better suited to an expandable detail or tooltip than the compact badge row. (The task's alternatives — loose "Externally reviewed" vs. medium "Partial external review" — are both viable; recommending the precise form because the whole point of this feature is to stop overstating review coverage, and a vague badge re-introduces exactly that ambiguity at the display layer even after fixing it at the data layer.)
- **Re-translate warning:** current copy is binary and references a single reviewer/date. Recommend: *"{languageName} has {N} of {M} sections that were reviewed and edited by a trusted external reviewer (most recently {reviewer} on {date}). Re-translating will discard those edits. Continue?"* — drop the current line *"If you need the same trust level afterwards, a new external review round would be required"* or reword it, since (per §6) a second round today can only happen via exactly this destructive re-translate — the existing copy asserts a lighter-weight path that doesn't exist.
- Both are explicitly flagged for the boss's sign-off per the task's own skepticism note — this recon recommends but does not decide.

---

## 6. Workflow state findings (Part 5)

### 6.1 State machine, confirmed via full trace

`TranslationWorkflowState` (10 values: `Initial, AIGenerated, Validated, ReviewerAccepted, AwaitingThirdParty, ThirdPartyReviewed, Accepted, Stale, Translating, Validating`) is **derived**, not stored — computed from the latest `WorkflowEvent` row per `(talkId, languageCode)` via a pure event-type switch (`EventTypeToState`, lines 827-842).

Relevant guards, quoted exactly:

- `InitiateExternalReview` (line 353-356): `if (state != Validated && state != ReviewerAccepted) return Fail("Cannot initiate external review from state {state}; requires Validated or ReviewerAccepted.")`.
- `SubmitExternalReview` requires current state `AwaitingThirdParty` (line 456-459) → always writes `ExternalReviewSubmitted` → state becomes `ThirdPartyReviewed`, whether the reviewer accepted or declined at the payload level (the `accepted` boolean only gates whether the section-merge/provenance-stamp runs, lines 487-505; it doesn't change the resulting state).
- `StartTranslation` (a full AI regeneration) is unconditionally blocked while `AwaitingThirdParty`/`ThirdPartyReviewed` (line 183-186), and from `Accepted` requires `confirmOverwrite=true` and discards `TranslatedSections` entirely (no partial/section-scoped re-translate exists anywhere in this codebase).

### 6.2 Second-round reviews — confirmed genuinely blocked today, guard identified precisely

`ThirdPartyReviewed` is **not** in `InitiateExternalReview`'s allow-list (`Validated`/`ReviewerAccepted` only). There is no transition anywhere in `TranslationWorkflowService.cs` that moves state **out of** `ThirdPartyReviewed` back into `Validated`/`ReviewerAccepted` — the only exits are `AcceptAsFinal` (→ `Accepted`, terminal-ish) or a full destructive re-translate cycle. So: **today, a second external-review round with a different editable set is structurally impossible without discarding the first round's edits.**

**Required relaxation:** add `ThirdPartyReviewed` to `InitiateExternalReview`'s allow-list (line 353-356 becomes `state != Validated && state != ReviewerAccepted && state != ThirdPartyReviewed`). No change needed to `EventTypeToState` — `ExternalReviewInitiated` already maps to `AwaitingThirdParty` regardless of source state, so re-entering `AwaitingThirdParty` from `ThirdPartyReviewed` works with the existing event-mapping table unchanged. This matches the locked design decision exactly ("just send a new invitation with a different editable set — no extend-invitation concept") — content is **not** discarded, only a fresh invitation with a new editable set is created; prior sections' `Content` and per-section provenance remain intact in the JSON blob (round 2 simply adds/updates provenance on whatever new subset gets edited).

**Open question the recon cannot resolve — flag for confirmation before Chunk F:** should `Accepted` also be added to the allow-list (permitting a "re-open for further review after the admin already clicked Accept" flow), or should second rounds only be possible pre-Accept (i.e., only from `ThirdPartyReviewed`)? The locked decision doesn't explicitly address this. Recommend defaulting to **`ThirdPartyReviewed` only** (Accepted stays a stable terminal state, consistent with its current semantics), but this needs an explicit yes/no before implementing Chunk F.

### 6.3 Concurrent invitations — traced, not actually possible by construction

The prompt asked to trace "invitation 1 Accepted, invitation 2 InProgress simultaneously." Because state is a single derived value per `(talk, language)` — not per-invitation — and `InitiateExternalReview` requires the *current* state to be one of a specific set that excludes `AwaitingThirdParty` itself, **only one invitation can ever be "pending" at a time**; a second `InitiateExternalReview` call cannot be made while the first is still awaiting a reviewer response. Rounds are strictly sequential, never concurrent, by construction of the state machine — this is a structural guarantee, not something that needs new enforcement code. One item worth a quick verify during implementation rather than assumed: whether `ExternalParticipantInvitation.Status` transitions away from `Pending` on submit (so the round-1 token becomes unusable once round 2 is initiated) — behaviourally implied by `SubmitExternalReview`'s precondition check (`invitation Pending + unexpired`, line ~456-459) but not independently confirmed in this recon.

### 6.4 Data model for second rounds — confirmed, no additional changes needed

Per point 12 of the original scope: confirmed. Each `InitiateExternalReview` call already creates a fresh `ExternalParticipantInvitation` row (`TenantEntity`, own `Id`/timestamps/token). Under this feature, each row also carries its own `EditableSectionIndicesJson` (§2.1). Multiple historical invitation rows per `(talkId, languageCode)` over time is already the existing shape (evidenced by the fact that `CancelExternalReview`/`DeclineExternalReview` already return state to `ReviewerAccepted`, from which a fresh `InitiateExternalReview` can already be called today — i.e., the "multiple invitations over time for the same language" pattern already exists and works; §6.2's relaxation just adds one more source state, `ThirdPartyReviewed`, to the set that's allowed to spawn a new one). No new entity, no new relationship, no new migration beyond §2.3.

---

## 7. Chunk breakdown (Part 6)

| Chunk | Contents | Depends on | User-visible? |
|---|---|---|---|
| **A — Data model** | `EditableSectionIndicesJson` column + migration on `ExternalParticipantInvitation`; promote `TranslatedSectionEntry` to a shared model with `ReviewedAt`/`ReviewedBy`; audit + update every read/write site of `TranslatedSections` for round-trip safety | none | No — new column unused, new fields always null |
| **B — Send-side flow** | Dialog checkbox list + select-all/none + validation + copy fix; `InitiateExternalReviewRequest`/service/controller changes; `GetPortalContext`/`ExternalReviewSectionDto.IsEditable` | A | Yes (admin) — but see sequencing note below |
| **C — Reviewer UI** | Editable/read-only branching, disabled-Textarea + badge pattern, submit-payload filtering to editable indices, verify flag-highlighting stays unconditional | B | Yes (external reviewer) |
| **D — Submit-side** | Gate 2 tightening (invitation-scoped check), per-section provenance write (stamp inside merge loop) | A | Not directly, but is the security boundary for C |
| **E — Read-side** | `GetState`/DTO addition for per-section list, badge copy, re-translate warning copy + `overwriteWasExternallyReviewed` derivation | A, D | Yes (admin) |
| **F — Second-round guard relaxation** | Add `ThirdPartyReviewed` to `InitiateExternalReview`'s allow-list | A, B | Yes (admin), edge-case path |

**Dependency order:** A → B → {C, D in parallel — both depend only on A/B, no dependency on each other's internals} → E (needs D's writes to have real data to display) → F (logically last; benefits from A-E being stable, and has its own open product question per §6.2 to resolve first).

**Hard sequencing constraint (not just a dependency, a correctness requirement):** **D must not ship after C.** D shipping before or simultaneously with C is safe and inert (nothing sends a scoped invitation yet, so the tightened Gate 2 check never fires — `EditableSectionIndicesJson` is always null until B+C exist). C shipping before D is the exact defect the prior recon's Q10 flagged: a read-only-looking UI with no server-side enforcement behind it — cosmetic scoping only, exploitable via a raw POST to the token-authenticated submit endpoint. Recommend D and C land in the same release even if built as separate PRs, with D merged first or atomically.

### 7.1 Ship-independently point

**Chunk A can ship to Production alone.** It's a new nullable column plus a code-level promotion of an internal class to add optional fields — no behaviour changes to any existing flow, since nothing reads or writes the new column/fields yet. Old JSON blobs deserialize cleanly (missing properties default to `null`/absent under `System.Text.Json`). This is the one genuine "ship it now, dark" chunk.

Everything B through F is coupled to the point of not being independently useful:
- B alone (dialog + backend accepts the field, but C doesn't exist) means admins see a selection UI that appears to work, but the reviewer portal ignores it entirely (pre-existing behaviour: renders and allows editing every section regardless) — a confusing half-feature that contradicts its own UI, should not ship alone.
- C alone (without D) — the security gap named above; must not ship first.
- E alone (without D having ever written real per-section data) — would show "0 of N sections reviewed" forever, a regression in signal quality until D exists.
- F alone — meaningless without A/B (nothing to select a *different* editable set from) and touches a state guard that's easy to get subtly wrong in isolation; should be the last, most carefully tested piece.

---

## 8. Migration shape summary

One migration, two changes:

1. `ExternalParticipantInvitations.EditableSectionIndicesJson` — new nullable `text` column, schema `toolbox_talks`. Standard `AddColumn`/`DropColumn` migration, CLI-generated per Note 28 (`dotnet ef migrations add`, run from `src/QuantumBuild.API` with `--project` pointing at the ToolboxTalks Infrastructure project — verify both `.cs` and `.Designer.cs` are produced).
2. No database schema change for per-section provenance — application-code-only change to the shape serialized into the existing `TranslatedSections` text column.

No FKs, no indexes, no defaults beyond nullability, no data backfill required (new fields are meaningfully `null` for all existing rows — "never reviewed," which is true).

---

## 9. Risks and unknowns

1. **`TranslatedSectionEntry` promotion is the highest-effort, highest-omission-risk item in Chunk A.** This recon identified at least `TranslationWorkflowService.cs` (three sites), `AiSectionGenerationService.cs`, and `GetMyToolboxTalkByIdQueryHandler.cs` as deserializers of `TranslatedSections`, but did **not** exhaustively enumerate every read/write site across the codebase. Implementation must start with a full grep for `TranslatedSections` and `TranslatedSectionEntry`-shaped deserialization before touching the class, or a round-trip will silently drop provenance somewhere unaudited.
2. **`SectionIndex` (positional int) vs. `SectionId` (Guid) — a pre-existing identity mismatch this feature inherits, not introduces.** `TranslatedSectionEntry` is keyed by `SectionId` (Guid); `TranslationValidationResult`, `ExternalReviewEditedSectionDto`, and the new `EditableSectionIndices` are all keyed by `SectionIndex` (positional int). Existing code already does `sections[edit.SectionIndex]` (positional list access), so this already works today for the merge path — but it's fragile if sections are ever reordered between invitation-send-time and reviewer-submit-time. Not a new risk created by this feature, but worth naming since the feature adds a second index-keyed field (`EditableSectionIndices`) that inherits the same fragility as the first (`ExternalReviewEditedSectionDto.SectionIndex`).
3. **Deviation from the prior recon's informal suggestion.** The prior recon (§4 Q10) suggested extending `ContextPayload` for the editable-set storage; this recon recommends a dedicated typed column instead (§2.1). Not a blocking disagreement, but worth flagging explicitly since it's a change from what was previously floated.
4. **§6.2 open question — does second-round relaxation include `Accepted` as a source state, or only `ThirdPartyReviewed`?** Needs an explicit answer before Chunk F is scoped; this recon recommends `ThirdPartyReviewed`-only but the locked design decisions don't address it directly.
5. **Provenance is overwrite-in-place, not append-only/historical.** If the same section is reviewed in two separate rounds, only the most recent `ReviewedAt`/`ReviewedBy` survives per section — consistent with how `Content` itself is already overwritten today (no edit history exists anywhere in this flow), so not a new gap, but worth surfacing in case the boss expects a full audit trail per section (that would be a larger, different design: an append-only provenance log rather than a stamp).
6. **Badge and warning copy are explicitly recommendations, not decisions** (§5.4) — flagged per the task's own instruction that the boss may want a say.
7. **`FlaggedText`'s exact target (original vs. translated text) in every admin call site (`ValidationSectionCard`) was not fully resolved** — the variable name `displayText` was found but not traced to its source in every branch. Low risk (the component itself is reusable regardless of which text it's pointed at), but worth a quick confirm so the reviewer portal's highlighting matches the admin's target exactly, per the "same visual signal" requirement.
8. **`ExternalParticipantInvitation.Status` transition on submit was not independently confirmed** (§6.3) — behaviourally implied (submit requires `Pending` + unexpired) but the exact post-submit `Status` value assignment wasn't traced in this recon. Should be a quick check before relying on it to prevent stale-token reuse across rounds.
9. **Dead `ExternalReviewRejected` event-type mapping** exists in `EventTypeToState` but is never written anywhere in the current codebase — an inherited oddity, not a blocker, but worth knowing about in case future work near this area assumes it's live.
10. **No FluentValidation/CQRS layer for `InitiateExternalReview`** means the new range-validation for `EditableSectionIndices` has no natural home in the validator-testing patterns used elsewhere in the codebase (e.g. `CreateToolboxTalkScheduleCommandValidator`). This recon recommends following the existing inline-guard-clause idiom used by every other check in this same method rather than introducing new architecture — flagged so implementation doesn't over-engineer this one input check relative to the rest of the file's style.
