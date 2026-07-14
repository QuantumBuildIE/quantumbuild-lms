# Recon: Low-Score External-Review Feature — Data Model & Infrastructure Audit

**Status:** Read-only recon. No code changed. Compiled 2026-07-14.

**Feature being scoped:** Learnings list page gets (a) a column showing count of
sections failing a validation score threshold, and (b) a "Send for Review"
action (enabled when count > 0) that initiates external review per language,
using a new per-tenant reviewer configuration (language-specific email, or an
"all languages" fallback, or a mix). The existing wizard-based per-section
external review flow must continue to work unmodified, independently.

---

## 1. The threshold field

**There is no threshold field on `ToolboxTalk` or `ToolboxTalkSettings`.** The
threshold lives entirely on the validation-run side:

| Entity | Field | Type | File |
|---|---|---|---|
| `TranslationValidationRun` | `PassThreshold` | `int`, required | `.../Domain/Entities/TranslationValidationRun.cs:18` |
| `TranslationValidationResult` | `EffectiveThreshold` | `int`, required — `PassThreshold` + `SafetyCriticalBump` when the section is safety-critical | `.../Domain/Entities/TranslationValidationResult.cs:44` |
| `ContentCreationSession` (wizard state) | `PassThreshold` | `int`, default `75` | `.../Domain/Entities/ContentCreationSession.cs:37` |

**Units: confirmed 0–100 integer (percentage scale), not a 0.0–1.0 ratio.**
`LexicalScoringService.Score()` returns `(matchCount / maxCount) * 100.0`;
`ConsensusEngine` rounds this to an int and compares directly against
`threshold` with no scale conversion anywhere in the chain
(`ConsensusEngine.cs:224`, `DetermineOutcome` lines 253–263: Pass if
`finalScore >= threshold`, Review if `>= threshold - 15`, else Fail).

**Default value:** `TranslationValidationSettings.DefaultThreshold = 75`
(appsettings-bound). This is barely consulted at runtime — the live path is a
hardcoded `request.PassThreshold ?? 75` fallback in
`TranslationValidationController.cs:85`. The one real read of
`IOptions<TranslationValidationSettings>.DefaultThreshold` is in
`AuditCorpusService.cs:127` (an unrelated corpus-audit fallback).

**Where it's set:**
- Wizard Input & Config step — a single dropdown value
  (`InputConfigStep.tsx`, both new and legacy wizard), populated from a
  tenant setting `ValidationPassThresholds` (JSON array of selectable values,
  e.g. `[70,75,80,85,90]`, edited at
  `components/settings/pass-threshold-section.tsx`). That tenant setting only
  controls which values are *offered* — it is not itself consulted by scoring
  code.
- The chosen value is stamped identically onto **every target language's
  run** at creation time (`ContentCreationSessionService.cs:664-688`, loop
  over `filteredCodes`). So although the schema supports a distinct
  threshold per language (each `TranslationValidationRun` carries its own
  `PassThreshold` + `LanguageCode`), the wizard UX does not currently let an
  admin set different thresholds per language up front — divergence can only
  happen later via a per-language retry/re-validate call that supplies a
  different value.
- Standalone endpoint `POST /api/toolbox-talks/{talkId}/validation/validate`
  also accepts its own `PassThreshold` per call, one run per one
  `LanguageCode` per call.

**Where it's read:** `ConsensusEngine.EvaluateRound1`/`DetermineOutcome` and
`TranslationValidationService.ValidateSectionAsync` (computes
`effectiveThreshold = passThreshold + SafetyCriticalBump` for safety-critical
sections, persists it to `EffectiveThreshold`).

**Publish gate: none.** `ContentCreationSessionService.PublishAsync` checks
only session status and content completeness (title/sections/output type) —
it never inspects `TranslationValidationRun.OverallOutcome`, `FailedSections`,
or any section's score/outcome. A talk with failing or review-status sections
in one or more languages can be published today with no gate at all. This
means the new "Send for Review" feature would be introducing the first
score-based gate/signal on the Learnings list, not tightening an existing one.

**Scope: per-run, i.e. effectively per (talk, language).** Not a single
global per-talk value, and (per above) not truly independently configurable
per language in the current wizard UX even though the schema supports it. The
composite index `{TenantId, ToolboxTalkId, LanguageCode}` on
`TranslationValidationRun` confirms the data model is keyed per language.

---

## 2. Per-section validation score data

**Storage: real relational columns, not JSON.** `TranslationValidationResult`
(`.../Domain/Entities/TranslationValidationResult.cs`) has plain `int`/`int?`
columns for `SectionIndex`, `ScoreA/B/C/D`, `FinalScore`, `RoundsUsed`,
`EffectiveThreshold`, and string-converted enum columns for `Outcome` /
`EngineOutcome` / `ReviewerDecision` (all directly queryable/filterable via
EF — confirmed via `TranslationValidationResultConfiguration.cs`). Only the
diagnostic/audit fields (`ArtefactsJson`, `RegistryViolationsJson`, etc.) are
JSON blobs, and none of those are needed for a fail count.

**Granularity: one result row per section, per run — and one run per (talk,
language).** Confirmed by (a) the unique index
`{ValidationRunId, SectionIndex}` on `TranslationValidationResult`, and (b)
`TranslationValidationRun.LanguageCode` being a required, single (non-list)
field, with run creation looping per language in both the wizard path
(`ContentCreationSessionService.cs:666-688`) and the standalone-validate
endpoint. **A talk validated in 3 languages has 3 separate
`TranslationValidationRun` rows**, each with its own N `TranslationValidationResult` rows.

**Existing denormalized counters — useful, but not sufficient as-is.**
`TranslationValidationRun` already carries `TotalSections`, `PassedSections`,
`ReviewSections`, `FailedSections` (set once at job completion,
`TranslationValidationJob.cs:398`). These are real, cheap-to-read per-run
counters — but they're per-run (i.e. per language, and per historical
re-validation), not rolled up per-talk across languages. **No existing DTO
exposes a per-talk fail count** — grep for `FailCount`/`SectionsBelowThreshold`/
`FailingSections` across `Queries`/`DTOs` returned nothing.

**Computing "N sections below threshold" per talk:** feasible in one query,
but genuinely new — not a reuse of an existing aggregate. It requires:
1. Join `TranslationValidationResult` → `TranslationValidationRun.ToolboxTalkId`
   (`ToolboxTalkId` is nullable on the run, since course-level runs share the
   table).
2. Resolve the **latest run per (talk, language)** — a talk can have several
   historical runs per language from re-validation/retry. This needs a
   group-by/window-function step; there is no existing server-side query that
   does this (the only place "latest run per language" is computed today is
   **client-side in JS**, in the wizard's `ValidateStep.tsx`
   `latestRunByCode` map).
3. Sum (or otherwise combine) `FailedSections` — or count
   `Outcome == Fail` results directly — across whichever runs are selected.

No JSON deserialization is needed (all relevant fields are real columns), so
this is a query-design problem, not a performance/deserialization one.

**Multi-language handling — no precedent for aggregation.** The wizard's
`ValidateStep.tsx` computes pass/review/fail stats **only for the single
active-language tab** (`mergedSections` derived from that language's
`runDetail`); switching tabs recomputes from scratch. There is no code path
anywhere that merges a section's status across languages into one number. A
section scoring 90% in English and 40% in Irish today shows up as two
independent `Fail`/`Pass` entries in two different runs — visible only when
the corresponding language tab is active. **This means "1 section failing in
N languages counts as 1 or N" is an open product decision with no existing
convention to fall back on** (see open questions, §8).

**`Outcome` vs `EngineOutcome`:** `Outcome` reflects the current state and can
be downgraded from Pass to Review by post-hoc artefact/registry scans, or
changed by reviewer Accept/Edit; `EngineOutcome` is the original engine
verdict, frozen at first computation. The fail-count definition needs to pick
one explicitly.

---

## 3. Existing per-tenant reviewer configuration

**Confirmed: none exists.** This is a genuine gap, not something to avoid
duplicating.

- `TenantSettingKeys` (`Core.Application/Features/TenantSettings/TenantSettingKeys.cs`)
  has no key referencing "review" or "reviewer". Full current key list:
  `EmailTeamName`, `TalkCertificatePrefix`, `CourseCertificatePrefix`,
  `SkipValidationStep`, `QrLocationTrainingEnabled`,
  `ExternalParticipantTokenLifetimeDays`, `UseNewWizard`,
  `UseNewCourseCreation`.
- `ExternalParticipantTokenLifetimeDays` is the only tenant setting that
  touches the external-review flow at all, and it only controls invitation
  link expiry (days) — not who receives invitations.
- Codebase-wide grep for `Reviewer|ExternalReviewer|DedicatedReviewer|TenantReviewer`
  as type names found only: `ReviewerType` (enum `Internal`/`External` — a
  classification, not a config table) and `ReviewerDecision` (enum
  `Pending/Accepted/Rejected/Edited` — the per-section audit decision, a
  different concept entirely). Neither stores or resolves a reviewer's
  contact address.
- The only place a reviewer email lives today is
  `ExternalParticipantInvitation.InvitedEmail` — a **per-invitation** value,
  typed fresh by the admin each time (see §4). It is not a stored tenant
  preference.

A new `TenantReviewerConfig`-style table (reviewer email per language, plus an
"all languages" fallback) has no naming or semantic collision with anything
current — clean ground.

---

## 4. Existing wizard-based external review flow

**Command:**
`ITranslationWorkflowService.InitiateExternalReview(Guid talkId, string languageCode, string invitedEmail, List<int>? editableSectionIndices, ...)`
— `Application/Abstractions/Workflows/ITranslationWorkflowService.cs:66`,
implemented in `Infrastructure/Services/Workflows/TranslationWorkflowService.cs:374-476`.

- `invitedEmail` is a **direct string parameter** — no lookup against any
  table or setting. It's used as-is to build the `ExternalParticipantInvitation`.
- State guard: only allowed from `Validated`, `ReviewerAccepted`, or
  `ThirdPartyReviewed` (see §5).
- Creates an `ExternalParticipantInvitation` row (hashed token, expiry from
  `ExternalParticipantTokenLifetimeDays`, default 30 days), records a
  `WorkflowEvent`, then fires
  `IEmailService.SendExternalReviewInvitationEmailAsync(...)`.

**API:** `POST /api/toolbox-talks/{id}/translations/{languageCode}/initiate-external-review`
on `ToolboxTalksController.cs:1901-1940`, policy `Learnings.Manage`. Request:
`{ ReviewerEmail, EditableSectionIndices }`.

> Note: `Learnings.Manage` is a real, wired permission
> (`Core.Infrastructure/Identity/Permissions.cs:15`) but is **not** in
> CLAUDE.md's documented Learnings-module permission table (which lists only
> `Learnings.View`/`Learnings.Schedule`). Doc drift, flagged here — not fixed,
> out of recon scope.

**Frontend:** `components/SendExternalReviewDialog.tsx`, rendered from
`TranslationWorkflowPanel.tsx` on the **talk detail page's translation
workflow panel** (per-language row) — not the validation-run detail page.

- Reviewer email is a plain `<Input type="email">` bound to local state.
- **Confirmed: no default/prefill of any kind.** The dialog's open effect
  explicitly resets the field to `''` every time it opens
  (`SendExternalReviewDialog.tsx:43-51`) — not hardcoded, not a tenant
  setting, not last-used, not the current admin's own email. The admin must
  type the address fresh every send.
- Validation is just "contains `@` and is non-empty" — no format validation,
  no autocomplete/lookup.

**Independence — confirmed safe.** Nothing in this flow reads a tenant-level
reviewer table (none exists), so it has zero indirection for a new table to
collide with or need to intercept. `invitedEmail` is a pure pass-through
parameter. A new per-tenant reviewer config table can be added and consulted
by an entirely separate call site without touching this flow.

**Reusable pieces:** `IEmailService.SendExternalReviewInvitationEmailAsync`
exists but is tightly coupled to the token/portal mechanism (takes a
`portalUrl` built from an `ExternalParticipantInvitation`). It's reusable
as-is *only if* the new feature also routes through the same
invitation/portal mechanism — which would also give it invitation-status
tracking and decline handling for free (see open question in §8). If the new
feature is meant to be lighter-weight, a new `IEmailService` method would be
needed alongside this one, built on the same low-level `IEmailProvider`/
`EmailMessage` abstraction.

---

## 5. Workflow state machine

**Scoped to (ToolboxTalkId, LanguageCode) pairs — not a column on `ToolboxTalk`
and not on `TranslationValidationRun`.** State is derived live from the most
recent `WorkflowEvent` row for `(WorkflowType.Translation, talkId, languageCode)`
— there is no stored state column (`TranslationWorkflowService.GetState`,
lines 65-131).

**`TranslationWorkflowState` enum:** `Initial, AIGenerated, Validated,
ReviewerAccepted, AwaitingThirdParty, ThirdPartyReviewed, Accepted, Stale,
Translating (transient), Validating (transient)`.

**Allowed source states for `InitiateExternalReview`:** `Validated`,
`ReviewerAccepted`, `ThirdPartyReviewed` only — confirmed via the guard clause
quoted in §4. Note `ThirdPartyReviewed` is itself an allowed source, so a
talk/language can be re-sent for another review round after a prior one
completes.

**`WorkflowType` has exactly one member (`Translation = 1`).** This workflow
system is currently scoped entirely to the wizard's translation-review use
case — it is not a generic talk-level workflow mechanism.

**Independent of `ToolboxTalkStatus` (Draft/Processing/ReadyForReview/Published)
and `ValidationRunStatus` (Pending/Running/Completed/Failed/Cancelled).**
Neither is checked by `InitiateExternalReview`'s guard. A `ToolboxTalk` can be
`Published` at the talk level while individual languages sit anywhere in
their own `TranslationWorkflowState`, independently, per language.

**Implication for the Learnings list page:** since eligibility is
per-(talk, language), a single talk row could have some languages eligible
for review (`Validated`+) and others not (still `Translating`, or never
validated / `Initial`). A "Send for Review" action driven by score threshold
either needs to (a) reason per-language and only offer/attempt eligible
languages, explicitly surfacing skipped ones, or (b) deliberately bypass this
state machine and build independent gating — since it's scoped to
`WorkflowType.Translation` specifically, nothing forces the new feature to
reuse it. This is a design decision, not something resolvable from current
code (see §8).

**Typical list-page states for context:** freshly created talks are
`ToolboxTalkStatus.Draft`; fully published are `Published`. Neither of these
correlates directly with `TranslationWorkflowState` — a `Draft` talk with no
translations yet would have every language at `Initial` (not eligible); a
`Published` talk could still have one or more languages stuck below
`Validated` if translation/validation was skipped or failed for that
language.

---

## 6. Learnings list page structure

**File:** `web/src/features/toolbox-talks/components/ToolboxTalkList.tsx`,
rendered by `web/src/app/(authenticated)/admin/toolbox-talks/talks/page.tsx`.

> Note: `/admin/toolbox-talks/learnings` redirects to a **drafts-only**
> list of in-progress wizard sessions — not the full table. The real list
> with search/filter/pagination/actions is at `.../talks`.

**Data flow:** `useToolboxTalks()` →
`GET /api/toolbox-talks` (params: `searchTerm`, `frequency`, `isActive`,
`status`, `pageNumber`, `pageSize`) → `ToolboxTalksController.GetAll` →
`GetToolboxTalksQueryHandler` → `Result<PaginatedList<ToolboxTalkListDto>>`
(envelope pattern — frontend reads `response.data.data`).

**`ToolboxTalkListDto` fields:** `Id, Code, Title, Description, Category,
Frequency, FrequencyDisplay, IsActive, HasVideo, RequiresQuiz, Status,
StatusDisplay, GeneratedFromVideo, GeneratedFromPdf,
AutoAssignToNewEmployees, SectionCount, QuestionCount, CompletionStats,
CreatedAt, CreatedBy, CreatedByName, LastEditedStep`. **No validation-run
data of any kind.** Confirmed by direct read of both the DTO and the query
handler — no join to `TranslationValidationRun`/`Result` anywhere in that
handler.

**Existing columns:** Code, Title (+description), Frequency, Active
(badge), Sections (count), Questions (count, "-" if no quiz), Assignments
(completed/total + overdue badge), Created, Actions.

**Existing Actions dropdown items (all `DropdownMenuItem` onClick handlers):**
1. **View** — always shown, navigates to detail page.
2. **Edit** — gated `Learnings.Manage`, navigates to edit page.
3. **Schedule** — gated `Learnings.Schedule`, `disabled` when `!isActive`,
   opens `ScheduleDialog` via a parent-owned callback prop (dialog-opening
   pattern, not direct mutation).
4. **Delete** — gated `Learnings.Manage`, sets local
   `talkToDelete`/`deleteDialogOpen` state → renders
   `DeleteConfirmationDialog` → on confirm, calls `useDeleteToolboxTalk()`
   mutation.

**Backend support for a fail-count column: absent, needs new work.**
- No existing handler joins `ToolboxTalk` to validation entities for list
  purposes. The closest analog, `TranslationValidationController.GetRuns`, is
  scoped to a single `talkId` — not usable as-is across a paginated talk list.
- `TranslationValidationRun` already carries the denormalized counters
  (`TotalSections/PassedSections/ReviewSections/FailedSections`) needed —
  so the new query would NOT need to touch `TranslationValidationResult`
  at all if "latest run per language, summed" is the chosen definition. It
  would need to resolve "latest run(s)" per talk, which has no existing
  precedent server-side (only in wizard client JS).
- Pagination is real (`Skip/Take`, default page size 10) — any new aggregate
  must be batched over just the current page's `talkIds`, following the
  existing pattern already used for `CompletionStats` (a single grouped
  query executed after pagination, `GetToolboxTalksQueryHandler.cs:90-91,
  130-165`) — a good template to copy.
- No dashboard/reports endpoint aggregates validation data across talks
  either — confirmed absent by grepping all consumers of the
  `TranslationValidationRuns` DbSet.

**Modal/preview-before-mutation precedent:** `SendExternalReviewDialog.tsx`
(§4) is the closest analog to the new feature's modal — state-in-parent
("which row/language is this dialog for"), a pure preview/confirm shadcn
`Dialog` fed by already-loaded parent data, `useMutation` + Sonner toast on
confirm. It is currently talk **+ language**-scoped and section-selectable;
adapting it for a talk-scoped list row would need either a language picker
in the new modal or a defined default/all-failing-languages convention — not
resolved by anything in the current code. Also note: the existing pattern for
"is this action available" is to **hide** the action entirely when
ineligible (via a state-predicate helper), not to show it `disabled` — worth
matching for consistency, though the feature spec's "enabled/shown when
count > 0" wording is compatible with either.

---

## 7. Chunk breakdown (directional sizing, not binding)

| # | Chunk | Scope | Rough size |
|---|---|---|---|
| A | **Data model** | New `TenantReviewerConfig`-style entity (tenant + language-specific email, "all languages" fallback row), EF configuration, CLI-generated migration (per Note 28) | Small — 0.5 day |
| B | **Backend query — fail count** | New query resolving "latest run per (talk, language)" per page of talks, then computing/summing a fail count per talk, following the batched-grouped-query pattern already used for `CompletionStats`. Needs the §8 definitional decisions locked first (Outcome vs EngineOutcome, sum-vs-per-language, latest-vs-worst run) | Medium — 1–2 days, mostly in getting the "latest run per language" resolution and multi-run edge cases right |
| C | **Backend query — reviewer resolution** | Given tenant + language, resolve configured reviewer email (language-specific match, else "all languages" fallback, else — TBD, see §8) | Small — bundles naturally with A/B |
| D | **Backend command — trigger review** | Orchestration that, for a talk's failing languages, resolves each language's reviewer and calls the *existing* `ITranslationWorkflowService.InitiateExternalReview` per eligible language (reusing §4/§5 machinery) — must respect the existing state guard and explicitly handle/report languages not in an eligible state | Medium — 1–2 days; complexity is in per-language partial success/failure handling, not new plumbing |
| E | **Frontend — list page** | New column (fail-count badge/dash), new hide-when-ineligible "Send for Review" action, new preview modal (per-language breakdown, resolved reviewer preview, confirm) — reusing `DataTable`/`Dialog` conventions | Medium — 1–2 days |
| F | **Frontend — admin UI** | Tenant reviewer config CRUD (language-specific rows + one "all languages" fallback row), likely under Settings, following the existing `SafetyGlossary` CRUD UI pattern (table + add/edit dialog, same module) | Medium — 1–2 days |
| G | **Tests** | Backend unit tests for fail-count query and reviewer-resolution logic (including multi-run/re-validation edge cases); integration test for tenant isolation of the new reviewer table; existing wizard flow regression check (§4 independence) | Small–medium — 0.5–1 day |

**Rough total: ~1–1.5 weeks**, dominated by the definitional decisions in §8
(which determine chunk B and D's actual shape) rather than raw implementation
effort. Locking those first will tighten these estimates significantly.

---

## 8. Product questions requiring a decision before implementation

1. **"Failing" definition — which outcome bucket?** Does "sections failing
   the threshold" mean `Outcome == Fail` only (the engine's fail bucket,
   score < threshold − 15), or does it also include `Review`
   (score < threshold but ≥ threshold − 15)? These are materially different
   counts. (§1, §2)

2. **`Outcome` vs `EngineOutcome`?** `Outcome` can be downgraded post-hoc by
   artefact/registry scans or changed by reviewer Accept/Edit;
   `EngineOutcome` is frozen at first computation. Which should drive the
   count? (§2)

3. **Multi-language aggregation — sum or per-language?** A section scoring
   badly in 2 of 5 target languages: does the list column show "1 section
   failing" (talk-level, unified) or a number that reflects 2
   language-section pairs? No existing precedent in the codebase answers
   this — it's a genuinely new design decision. (§2)

4. **Latest run only, or across history?** Talks can have multiple
   historical validation runs per language (re-validations, retries). Should
   the count reflect only the most recent run per language, or something
   else (e.g. worst-ever)? Latest-only is the natural default but needs
   explicit confirmation. (§2)

5. **Per-language "Send for Review" scope.** Does clicking "Send for Review"
   on a list row open one modal covering *all* currently-failing languages
   for that talk (fanning out to multiple `InitiateExternalReview` calls with
   per-language resolved reviewers), or does it require picking one language
   at a time? (§4, §5)

6. **Ineligible-language handling.** For a language with failing sections but
   sitting outside `Validated`/`ReviewerAccepted`/`ThirdPartyReviewed` (e.g.
   still `Translating`, or never validated / `Initial`), what should the new
   flow do — silently skip it, block the whole send, or surface a warning
   with partial send? (§5)

7. **Reuse the existing invitation/portal mechanism, or build lighter-weight
   notification?** Routing through `ExternalParticipantInvitation` +
   `ITranslationWorkflowService` gives token-based portal access, invitation
   tracking, and decline handling for free, but ties the new feature to the
   same state machine and its per-(talk,language) granularity. A separate,
   lighter mechanism avoids that coupling but duplicates
   invitation/email/tracking plumbing. (§4, §5)

8. **"All languages" fallback semantics.** Is it a true fallback (used only
   when no language-specific reviewer is configured for that language), or
   should it also act as a mandatory CC/notify regardless of a
   language-specific match? (§3)

9. **Which threshold value drives the fail count?** `PassThreshold` lives on
   the run, not the talk, and — per §1 — can differ across languages/reruns
   for the same talk. Should the fail count use each run's own
   `PassThreshold`/`EffectiveThreshold` as recorded, or a fresh tenant-wide
   default applied uniformly for display purposes? (§1, §2)

10. **Zero-vs-never-validated distinction in the column.** Should a talk with
    zero failing sections (validated, all passing) render differently from a
    talk that has never been validated at all (no runs exist)? Both would
    naturally compute to a count of 0/blank under a naive query. (§6)

11. **Permission for the new action.** The existing analogous action
    (`InitiateExternalReview`) is gated by `Learnings.Manage` — a permission
    that is wired but **not documented** in CLAUDE.md's permission table
    (only `Learnings.View`/`Learnings.Schedule` are listed there). Should the
    new list-page action reuse `Learnings.Manage`, or a different/new
    permission (e.g. `Learnings.Admin`, used for related review-adjacent
    admin actions elsewhere)? Worth resolving alongside a CLAUDE.md doc
    correction, though the doc fix itself is out of this recon's scope. (§4, §6)

12. **Publish-gate scope creep (flag only, not proposed).** Publishing
    currently has zero validation-outcome gate (§1). This recon surfaced that
    fact because it's adjacent, not because it's in scope — worth a
    deliberate "not now" decision so it doesn't get silently bundled in.
