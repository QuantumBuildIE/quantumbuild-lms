# Recon: Safe Removal of the Legacy Create Wizard and `UseNewWizard` Toggle

**Date:** 2026-07-24
**Scope:** Read-only investigation. No code changed.
**Purpose:** Determine what depends on what before (1) deleting the legacy create wizard
(`create-wizard/` + `ContentCreationSession` infrastructure), (2) deleting the paired
legacy edit page (if any), (3) removing the `UseNewWizard` tenant setting, and
(4) migrating all tenants to the new wizard implicitly.

**Prior docs read in full and treated as verified-current baseline, re-checked by direct
grep/read rather than trusted at face value:**
`docs/wizard-toggle-retirement-recon.md` (2026-07-09), `docs/old-vs-new-course-parity-recon.md`
(2026-07-13), `docs/wizard-defaults-and-autostart-recon.md`, `docs/course-in-new-wizard-scoping-v2.md`,
`docs/course-dead-hooks-and-edit-toggle-recon.md`.

---

## Headline

**The removal is safe in principle and the new wizard is functionally complete, but this
is NOT a small cleanup — it is two separable deletions bundled by the request, one of
which (Course creation) was a hard blocker eleven days ago and has since resolved itself
via unrelated work.** As of `docs/wizard-toggle-retirement-recon.md` (2026-07-09), the
legacy wizard was "not purely dormant, it is the only path for course creation." That
blocker no longer holds: a separate compose-existing Course flow (`CourseForm.tsx`,
gated by its own `UseNewCourseCreation` tenant setting, **defaulting to `true`, with no
admin UI to turn it off**) shipped between 2026-07-09 and 2026-07-13 and was verified
functionally complete and a strict improvement over the legacy course path on
2026-07-13. **This means the request's premise — "delete the legacy wizard entirely" —
is now achievable**, but it retires *two* toggles (`UseNewWizard` and, implicitly,
whatever residual reachability `UseNewCourseCreation=false` still has), not one, and the
backend session infrastructure is shared with one still-active job (`TranslationValidationJob`)
that has explicit `isNewWizard` branches requiring surgical removal, not a delete of the
whole file.

Estimated chunk count: **5** (see Part 8). Highest risk item: the `TranslationValidationJob`
`isNewWizard` branch surgery (Part 9) — a mistake there risks the new wizard's own
translation validation, not just legacy cleanup.

---

## Part 1 — Legacy code surface inventory

### Frontend — `create-wizard/` (26 files, ~8,139 LOC)

All files live under `web/src/features/toolbox-talks/components/create-wizard/`:

| File | Legacy-exclusive? | Notes |
|---|---|---|
| `CreateWizard.tsx` | Yes | Root 7-step orchestrator, rendered at `/admin/toolbox-talks/create` |
| `steps/InputConfigStep.tsx` | Yes | Calls session-scoped hooks only |
| `steps/ParseStep.tsx` | Yes | Session-scoped |
| `steps/QuizStep.tsx` | Yes | Session-scoped, per-section quiz loop (see Part 2) |
| `steps/SettingsStep.tsx` | Yes | Session-scoped |
| `steps/TranslateStep.tsx` | Yes | Session-scoped variant (distinct from shared `learning-wizard/steps/TranslateStep.tsx`) |
| `steps/TranslateValidateStep.tsx` | Yes | Combined variant, appears superseded |
| `steps/ValidateStep.tsx` | Yes | Session-scoped variant (distinct from shared `learning-wizard/steps/ValidateStep.tsx`) |
| `steps/PublishStep.tsx` | Yes | Imports shared `PreviewModal`, `useWizardPreference` |
| `steps/parse/AiSuggestionBanner.tsx`, `OutputTypeSelector.tsx`, `ParseLogPanel.tsx`, `SectionBodyEditor.tsx`, `SectionList.tsx` | Yes | `OutputTypeSelector.tsx` is the Lesson-vs-Course picker — this is the code the Course-creation blocker referenced (see Part 6) |
| `steps/quiz/QuestionCard.tsx`, `QuizSettingsPanel.tsx`, `SectionQuestionGroup.tsx` | Yes | Distinct copies from `learning-wizard/components/` equivalents — not shared despite similar names |
| `steps/settings/*.tsx` (5 files) | Yes | Behaviour/Category/Refresher/Slideshow/TitleDescription sub-panels |
| `steps/validate/ValidationProgressPanel.tsx`, `ValidationSectionCard.tsx` | **No — shared** | `learning-wizard/steps/ValidateStep.tsx` imports these directly |
| `steps/validate/FlaggedText.tsx` | **No — shared, cross-module** | Imported by the public `web/src/app/external-review/[token]/page.tsx` route — **must be relocated before/at deletion or that route's build breaks** |
| `steps/validate/SubtitleProgressPanel.tsx` | Yes | Uses `use-subtitle-hub`, a legacy-exclusive hook |

**Routes:** exactly one — `web/src/app/(authenticated)/admin/toolbox-talks/create/page.tsx`
(renders `<CreateWizard />`, no sub-routes, no per-ID resume URL). Session state is
kept in React state only.

### "Edit Talk" pages — confirmed NOT paired with the legacy wizard

- `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx`
- `web/src/app/(authenticated)/toolbox-talks/talks/[id]/edit/page.tsx`

Both render only `ToolboxTalkForm` + `useToolboxTalk` — zero import of `create-wizard`
or `content-creation`. **There is no legacy "resume create session" edit page to delete.**
The only coupling is visibility, not code sharing: `ToolboxTalkDetail.tsx`'s "Edit"
button only renders when `wizardPreference === 'old'` — a dormant pre-existing UX gap
(the same route is reachable, ungated, from the Learnings list row menu) already
identified in `docs/wizard-toggle-retirement-recon.md` Part 2, unrelated to this
deletion's scope but worth folding in as a one-line fix per that doc's Part 5
recommendation #3.

**Item 2 of the request ("delete the Edit Talk legacy page if paired with the legacy
wizard") is a no-op — no such page exists.**

### Backend — `ContentCreationSession` infrastructure

| Component | File | Legacy-exclusive? |
|---|---|---|
| `ContentCreationSession` entity | `.../ToolboxTalks.Domain/Entities/ContentCreationSession.cs` | Yes |
| `IContentCreationSessionService` / `ContentCreationSessionService` | `.../ToolboxTalks.Application/Abstractions/ContentCreation/`, `.../ToolboxTalks.Infrastructure/Services/ContentCreation/` | Yes (only consumer is the controller below) |
| `ContentCreationController` | `src/QuantumBuild.API/Controllers/ContentCreationController.cs`, route `api/toolbox-talks/create` | Yes — 18 endpoints, all session-scoped, all `Learnings.Manage`/`Learnings.View` |
| `VideoTranscriptionJob.cs` | `.../ToolboxTalks.Infrastructure/Jobs/` | Yes — session-scoped; a separate `VideoTranscriptionJobForTalk.cs` exists for the new wizard |
| `ContentCreationParseJob.cs` | Same dir | Yes — session-scoped; a separate `ContentCreationParseJobForTalk.cs` exists for the new wizard |
| `ExpiredSessionCleanupJob.cs` | Same dir | Yes — daily recurring (`Cron.Daily(3,0)`, `Program.cs:478-481`), purely session-scoped, no new-wizard equivalent needed (new wizard has nothing that expires the same way) |
| `TranslationValidationJob.cs` | Same dir | **No — shared**, see Part 2 |
| `ResetTenantDataCommandHandler.cs:117-122` | `Commands/` | Not wizard code — bulk-deletes `ContentCreationSessions` as step 13 of a full tenant wipe (test/demo utility) |

Legacy uses **no CQRS handlers at all** — every legacy operation lives directly on
`ContentCreationSessionService`, confirmed by an exhaustive grep of `Commands/`/`Queries/`
returning zero hits for `ContentCreationSession` outside the reset handler.

### Test files referencing legacy artifacts

| File | Scope |
|---|---|
| `tests/QuantumBuild.Tests.Integration/ToolboxTalks/ContentCreationSessionPublishTests.cs` | Explicitly legacy-only (own doc comment says so) — delete with the wizard |
| `tests/QuantumBuild.Tests.E2E/tests/toolbox-talks/content-creation.spec.ts` | **Actively exercises the legacy wizard end-to-end**, asserts `toHaveURL(/\/admin\/toolbox-talks\/create/)` — will break the moment the route is deleted; must be deleted/rewritten in the same chunk |
| `tests/QuantumBuild.Tests.E2E/page-objects/toolbox-talks/ContentCreationWizardPage.ts` | Page object modeling the legacy wizard exclusively — delete with the spec above |
| `tests/QuantumBuild.Tests.E2E/fixtures/test-constants.ts:83` | `createTalk: '/admin/toolbox-talks/create'` constant — remove once the spec above is gone |
| `web/src/features/toolbox-talks/components/create-wizard/**` | **Zero** `__tests__`/`.test.tsx` files exist — no frontend unit-test cleanup needed |

**Important scope correction found this session:** a second, independent Playwright
project exists at `tests/QuantumBuild.Tests.E2E/` (its own `package.json`,
`playwright.config.ts`, page-objects) that is **not documented in CLAUDE.md Note 30 at
all** — that note only describes `web/e2e/`. No `.github/workflows/*.yml` exists in the
repo to confirm whether `tests/QuantumBuild.Tests.E2E` runs in CI. **Confirm this before
deleting `content-creation.spec.ts`** — if it's CI-wired, the deletion PR needs to keep
CI green; if it's dead/unused, note that separately (it's a pre-existing gap, not
something this removal should silently paper over).

`TestTenantSeeder.cs` (lines 88, 1141, 1380-1410) seeds/cleans 2 sample
`ContentCreationSession` rows as **shared test fixture infrastructure** — do not delete
this without confirming no other integration test (new-wizard or otherwise) depends on
those seeded rows existing. `FakeContentCreationServices.cs` similarly wraps shared
`IContentParserService`/`IDocxExtractionService` fakes used by both wizards' tests — its
name is misleading; it is not legacy-only.

---

## Part 2 — Shared infrastructure map

### Genuinely shared — must remain

| Component | Why |
|---|---|
| `IContentParserService`, `IAiQuizGenerationService`, `IDocxExtractionService`, R2 storage services, translation/validation services | Both wizards call the same underlying AI/storage service interfaces (with different orchestration around them) |
| `ToolboxTalk`, `ToolboxTalkSection`, `ToolboxTalkQuestion`, `ToolboxTalkCourse`, `RegulatoryProfile`, etc. — all core domain entities | Obviously shared; the new wizard writes directly to these, legacy writes to them only at Publish |
| `use-validation-hub` hook | Used by both `create-wizard/steps/{Translate,Validate}Step.tsx` (3 files) and `learning-wizard/hooks/WorkflowSubscriber.tsx` + `learning-wizard/steps/ValidateStep.tsx` (2 files) |
| `useWizardPreference` hook | The cutover flag mechanism itself — used by `ToolboxTalkList.tsx`, `ToolboxTalkDetail.tsx`, legacy `PublishStep.tsx` |
| `learning-wizard/steps/TranslateStep.tsx`, `ValidateStep.tsx` | Directly imported by **legacy** `ToolboxTalkDetail.tsx` for its Translations/Validation tabs — these are new-wizard-authored components that are already shared, not new-wizard-exclusive |
| `create-wizard/steps/validate/ValidationProgressPanel.tsx`, `ValidationSectionCard.tsx`, `FlaggedText.tsx` | Imported by `learning-wizard/steps/ValidateStep.tsx` (first two) and the public `external-review/[token]` route (`FlaggedText`) despite physically living in the `create-wizard/` directory tree — **directory location does not equal ownership here; verify each file's actual importers before deleting anything under `create-wizard/steps/validate/`** |
| `content-creation.ts` / `use-content-creation.ts` (the API client files) | **Not wholly legacy** — house both session-scoped legacy functions (`createSession`, `parseSessionContent`, etc.) AND generic talk/course validation-run + regulatory-score functions (`useValidationRuns`, `useValidationRun`, `useSectionDecision`, `useRegulatoryApplicability`, `contentCreationKeys`) consumed by `ValidationHistoryTab.tsx`, `ValidationRunDetailView.tsx`, `RegulatoryScorePanel.tsx`, `ReviewScreen.tsx`, and `learning-wizard/steps/{PublishStep,ValidateStep,TranslateStep}.tsx`. **Must be split, not deleted wholesale.** |
| `InitialiseToolboxTalkCommand`/Handler | New-wizard-only in practice (legacy never calls it — legacy creates its `ToolboxTalk` row at Publish time via `ContentCreationSessionService.PublishAsync`), but it is CQRS-layer shared infra unrelated to the session concept — leave untouched |

### Legacy-adjacent — looks shared, only legacy uses it (candidates for removal)

| Component | Evidence |
|---|---|
| `use-subtitle-hub` hook | Exactly 1 importer: `create-wizard/steps/validate/SubtitleProgressPanel.tsx` |
| Session-scoped functions/hooks in `content-creation.ts`/`use-content-creation.ts` (full list in Part 5 of the frontend recon: `createSession`, `getSession`, `uploadSessionFile`, `updateSessionSource`, `parseSessionContent`, `updateSessionSections`, `startTranslateValidate`, `publishSession`, `abandonSession`, `generateSessionQuiz`, `getSessionQuizData`, `updateSessionQuestions`, `updateSessionQuizSettings`, `getSessionSettings`, `updateSessionSettings`, `uploadSessionCoverImage`, `checkSessionTitle`, `getUploadUrl`/`confirmUpload` session variants, `getSessionValidationRun`, `acceptSessionSection`, `editSessionSection`, `retrySessionSection`, and their wrapping hooks) | Each has exactly one external caller file, all inside `create-wizard/` |
| `rejectSessionSection` | 0 callers anywhere — dead even within legacy |
| `ContentCreationSessionService.GenerateQuizAsync`'s per-section loop pattern | Only exists inside this one method; the new wizard's `GenerateToolboxTalkQuizCommandHandler` uses a fundamentally different single-call-on-combined-content algorithm — confirmed by direct code comparison, not just naming |

### New-wizard-adjacent — only new wizard uses it

All 45 files under `learning-wizard/` not otherwise flagged as shared above (see Part 1
of the frontend-new-wizard recon for the full file list) — components, hooks, `lib/`,
`schemas/`. None of these were ever called by legacy at any point; legacy predates the
new wizard entirely.

### ContentCreationSession specifically

**The new wizard does not use it in any way.** Confirmed by exhaustive grep inside
`learning-wizard/` for `ContentCreationSession`, `contentCreationSession`, `session/` —
the only hits are two unrelated hooks (`useValidationRuns`, `useRegulatoryApplicability`)
imported from the same *file* as the session functions but calling entirely different,
non-session endpoints (`GET /toolbox-talks/{talkId}/validation/runs`, `GET
/regulatory/applicability`). Backend confirms the same split: `InitialiseToolboxTalkCommandHandler`
creates the `ToolboxTalk` row immediately at Step 1 with `LastEditedStep = 1` set; legacy
never creates a `ToolboxTalk` row until `PublishAsync`, and leaves `LastEditedStep = null`
forever — this is the exact discriminator the drafts list already relies on.

**The entire session concept can go**, with one genuine complication: `TranslationValidationJob.cs`
is the one place session logic and new-wizard logic coexist in the same class, gated by
an `isNewWizard`/`run.IsNewWizard` boolean:

1. `GenerateTranslationForSectionsAsync` — legacy-only branch (`if (!isNewWizard)`) queries
   `ContentCreationSessions` as a "relevance guard" before inserting a translation row
   (~lines 956-998).
2. `TryUpdateSessionStatusAsync` — called unconditionally at the end of every run
   (line 457), but is a harmless no-op for new-wizard runs (finds no session referencing
   that run id). For legacy runs it updates the session's status.

Deleting `ContentCreationSession` requires removing these two branches from
`TranslationValidationJob` — **surgically, not by deleting the file** — since the job
itself remains the new wizard's active translation-validation orchestrator.

---

## Part 3 — `UseNewWizard` toggle usage

Fully re-verified against current source, not just cited from the 2026-07-09 doc:

| Layer | Reference | Behavior |
|---|---|---|
| Backend | `TenantSettingKeys.cs:13` (declares key), `TenantSettingsService.cs:56` (`= "false"` default) | **Zero backend behavior branches on this key** — its only server-side presence is supplying a default value string to the frontend. No controller, Hangfire job, or command/query handler reads it (confirmed by grep — exactly these 2 backend hits exist). |
| Storage | `TenantSettings` table (generic key-value, `{TenantId, Module, Key}` unique) | No DB column, no entity field. **0 of 7 local tenants have ever written a row** — every tenant rides the in-code default today. |
| Frontend hook | `useWizardPreference.ts:22-30` | Resolution order: `?wizard=new/old` URL override (one-shot) → `settings?.['UseNewWizard'] === 'true'` → default `'old'` |
| Frontend UI toggle | `wizard-toggle-section.tsx`, mounted unconditionally in `admin/toolbox-talks/settings/page.tsx` General tab | Only write call site anywhere in the codebase for this key |
| Consumers | `ToolboxTalkList.tsx` (Create New button target), `create-wizard/steps/PublishStep.tsx` ("Create Another" button), `ToolboxTalkDetail.tsx` (Edit button visibility — the dormant UX gap noted in Part 1) | All null-safe (optional chaining + strict `===`) |
| Hangfire jobs | None found reading `UseNewWizard` | Confirmed — this is purely a frontend routing decision |

**Safety of hardcoding to "always new wizard":** confirmed safe by the 2026-07-09 recon
and re-confirmed here — because no tenant has an explicit `false` row, flipping the
in-code default to `"true"` (or, for the more thorough removal this request describes,
deleting the key/branches entirely) changes behavior identically and safely for all 7
local tenants. Whether any Railway (Development/Production) tenant has explicitly set
`false` **could not be checked this session or the 2026-07-09 session** (Railway CLI
unauthenticated both times) — **this is the one open item that must be checked with
live DB access before deleting the toggle for real**, not assumed from local-only data.

### Course creation's separate, once-blocking toggle — now resolved

The 2026-07-09 recon flagged Course creation as a hard blocker: "the legacy wizard's
code must stay fully functional after this fix regardless of the toggle default — it is
not purely dormant, it is the only path for course creation." **This is no longer true.**
Between 2026-07-09 and 2026-07-13, a separate compose-existing Course flow shipped
(`CourseForm.tsx`, `CreateToolboxTalkCourseCommandHandler`), gated by its own,
differently-polarized toggle:

- `TenantSettingKeys.UseNewCourseCreation`, default `"true"` (`TenantSettingKeys.cs:21`,
  `TenantSettingsService.cs:57`)
- Resolved by `useCoursePreference.ts:32` — `settings?.['UseNewCourseCreation'] !== 'false'`
  (opposite polarity from `useWizardPreference` — defaults *on*)
- **No admin UI toggle exists for this setting** (confirmed by grep across
  `web/src/features/toolbox-talks/components/` — zero hits). It can only be set to
  `false` via a direct API call or the `?coursemode=old` URL override; there's no
  Settings-page Switch a tenant could have flipped, unlike `UseNewWizard`.
- `docs/old-vs-new-course-parity-recon.md` (2026-07-13) verified the new course flow
  functionally complete and a strict improvement (it fixes two known legacy bugs: quiz
  question duplication across course items, and course-level settings being silently
  dropped at publish).

**Net effect:** the legacy wizard's `OutputTypeSelector.tsx`/Course-publish code path
(`ContentCreationSessionService.PublishAsCourseAsync`, ~300 lines) is, as of today,
reachable only if a tenant has been explicitly set to `UseNewCourseCreation=false` via
direct API (no UI path exists to have done this) or is smoke-testing with
`?coursemode=old`. This closes the prior hard blocker but does not make it zero-risk —
confirm no tenant has that flag set to `false` (same live-DB caveat as `UseNewWizard`
above) before deleting `PublishAsCourseAsync` and `OutputTypeSelector.tsx`.

---

## Part 4 — Route and navigation impact

| Route | Owner | Notes |
|---|---|---|
| `/admin/toolbox-talks/create` | Legacy — sole entry point, all 7 steps, no sub-routes | To be deleted |
| `/admin/toolbox-talks/learnings/**` (9 files: `new`, `drafts`, `[talkId]/{parse,quiz,settings,translate,validate,publish}`) | New — URL-per-step | Unaffected, becomes the only wizard |
| `/admin/toolbox-talks/talks/[id]/edit`, `/toolbox-talks/talks/[id]/edit` | Shared generic edit form, unrelated to either wizard's creation pipeline | Unaffected |
| `/admin/toolbox-talks/courses/new` | Redirect stub: `useCoursePreference()` true → renders `CourseForm` directly; false → `router.replace('/admin/toolbox-talks/create')` | The `false` branch's redirect target disappears with legacy deletion — **must remove this branch in the same chunk**, not leave a redirect to a 404 |

**Hardcoded frontend link sites to the legacy URL** (all inside/adjacent to the wizard's
own code, in scope for deletion, cross-referenced from `docs/wizard-toggle-retirement-recon.md`
and independently re-confirmed):

- `create-wizard/steps/PublishStep.tsx:228` — "Create Another" button
- `CourseList.tsx:262` — `useNewCourse ? '.../courses/new' : '/admin/toolbox-talks/create'`
- `courses/new/page.tsx:14` — `router.replace('/admin/toolbox-talks/create')`

**External-facing URL references — none found.** Grep for `admin/toolbox-talks/create`
across all of `src/` (backend) returned zero hits. Email templates
(`ToolboxTalkEmailService.cs`) link only to learner-facing `/toolbox-talks/{id}` and
`/toolbox-talks/courses/{id}`. Notification content (`ToolboxTalkNotificationService.cs`)
links only to `/admin/toolbox-talks/talks/{talkId}` (detail/validation pages, not the
wizard). **No email, deep link, or external integration references the legacy wizard
URL** — removing the route has zero external-link blast radius.

`docs/` folder itself contains ~20 internal recon/fix reports mentioning the legacy URL
(prior work-session artifacts, not customer-facing docs) — these will read as historical
once the code is gone; no action required beyond updating `CLAUDE.md` Note 29, which is
the canonical internal definition and will need rewriting post-deletion.

---

## Part 5 — Backend endpoint audit

`ContentCreationController` (`api/toolbox-talks/create`) — all 18 endpoints are
session-scoped and confirmed called only by the legacy frontend's `content-creation.ts`
session functions (Part 1/2). No external consumer, no Hangfire job, no test outside
`ContentCreationSessionPublishTests.cs` calls any of them directly.

| Verb | Route | Removal candidate? |
|---|---|---|
| POST/PUT/GET/DELETE `session/*` (all 18) | Yes — entire controller | All reachable only via legacy frontend, confirmed |

No route requires special handling for external consumers (mobile app, integrations —
none exist for this module per CLAUDE.md's documented API surface). The `Learnings.Manage`/
`Learnings.View` policies used here are shared policy names, not proof of shared
endpoints — verified each route's actual handler is `ContentCreationSessionService`-backed
and nothing else registers against the same route prefix.

---

## Part 6 — Data migration considerations

### In-flight legacy drafts

The legacy wizard has **no persisted per-ID resume state** at the frontend-route level —
session state lives only in React state during an active browser session. This means:

- A legacy session **abandoned mid-wizard (browser closed before Publish)** has no
  `ToolboxTalk` row at all — nothing to migrate, the `ContentCreationSession` row simply
  expires and gets cleaned up by `ExpiredSessionCleanupJob` (24h default, per CLAUDE.md's
  `SessionExpiryHours` config) or is deleted with the table.
- A legacy session **that has already published** produced a real `ToolboxTalk` (or
  `ToolboxTalkCourse`) row with `LastEditedStep = null`. These are NOT drafts — they are
  completed, published entities, fully independent of the session/wizard that created
  them. They need no migration; they already work correctly today when viewed via
  `ToolboxTalkDetail.tsx` (the shared detail page, wizard-agnostic).
- **The only real "in-flight, needs a decision" case:** a legacy talk sitting in Draft
  status where the admin published nothing but a `ContentCreationSession` row exists
  mid-pipeline. Since no `ToolboxTalk` row exists yet for that session, there is nothing
  in the `ToolboxTalks` table to "migrate to the new wizard" — the admin's only option
  post-deletion is to start over in the new wizard. **This should be communicated before
  the deletion ships**, mirroring the same customer-communication pattern
  `docs/old-vs-new-course-parity-recon.md` used for the Course cutover ("existing legacy
  courses are voided at cutover... told before the release").

### Drafts already showing the "Legacy" badge

`learnings/drafts/page.tsx` already discriminates `lastEditedStep === null` → "Legacy"
badge → routes `handleResume` to the talk detail page (not a wizard URL) instead of a
step URL. **These are published or partially-configured `ToolboxTalk` rows, not active
sessions** — they already open correctly in the shared detail page today and require no
migration. This is existing, working behavior; confirm it stays exactly as-is (do not
accidentally change `handleResume`'s legacy branch while removing other legacy code).

### Tenant configuration assuming legacy defaults

No tenant-settings field was found that has a legacy-specific default distinct from the
new wizard's entity defaults — per `docs/wizard-defaults-and-autostart-recon.md`, all
Step 4 settings resolve from the same `ToolboxTalkSettings` tenant table for both
wizards; there is no separate "legacy defaults" config surface to retire.

---

## Part 7 — Test surface

| Test | Action |
|---|---|
| `tests/QuantumBuild.Tests.Integration/ToolboxTalks/ContentCreationSessionPublishTests.cs` | **Delete** — explicitly legacy-only per its own doc comment |
| `tests/QuantumBuild.Tests.E2E/tests/toolbox-talks/content-creation.spec.ts` | **Delete** — walks the legacy wizard exclusively; confirm CI-wiring status first (Part 1) |
| `tests/QuantumBuild.Tests.E2E/page-objects/toolbox-talks/ContentCreationWizardPage.ts` | **Delete** — page object modeling only the legacy wizard |
| `tests/QuantumBuild.Tests.E2E/fixtures/test-constants.ts:83` (`createTalk` constant) | **Update** — remove the now-unused constant, do not touch sibling constants |
| `TestTenantSeeder.cs` (session row seeding, lines 88, 1141, 1380-1410) | **Update, not delete outright** — confirm no other integration test depends on these specific seeded rows before removing; this is shared fixture infrastructure |
| `FakeContentCreationServices.cs` | **Keep** — shared fake for `IContentParserService`/`IDocxExtractionService`, used by both wizards' tests despite the misleading name |
| `web/src/features/toolbox-talks/hooks/__tests__/useWizardPreference.test.ts` | **Delete** — tests the toggle-resolution hook being removed |
| `web/src/features/toolbox-talks/components/learning-wizard/lib/__tests__/stepOrder.test.ts` | **Keep** — new-wizard-only, unaffected |
| `web/src/features/toolbox-talks/hooks/__tests__/useCoursePreference.test.ts` | **Keep** — separate toggle (`UseNewCourseCreation`), out of this request's stated scope even though Part 3 shows it's now the load-bearing one for Course creation |
| `web/e2e/authenticated/learning-wizard-pdf.spec.ts` | **Keep** — new-wizard-only, on-demand, not CI-wired |
| No frontend component tests exist for `create-wizard/` | **Nothing to delete** — zero `__tests__`/`.test.tsx` coverage today |

**Distinguishing note:** several files that *mention* `ContentCreationSession` in
comments only (`TranslatedSectionEntryTests.cs`, `ExternalParticipantInvitationTests.cs`)
have no actual code dependency — leave untouched, they're just analogical doc comments.
`InitialiseToolboxTalkCommandHandlerTests.cs` and `CourseComposeExistingHappyPathTests.cs`
both reference "wizard" generically in comments but test genuinely shared/new-only
infrastructure — do not delete.

---

## Part 8 — Proposed removal sequence (5 chunks)

Sequenced from lowest-risk/most-reversible to highest-risk/least-reversible, per the
project's own "toggle removal is easily reversible, code deletion isn't" principle.

**Chunk 1 — Toggle-only, no code deletion (fully reversible in one revert).**
Hardcode `useWizardPreference()` to always return `'new'` (or delete the hook body and
inline `'new'` at call sites — either way, no file deletion yet). Remove
`<WizardToggleSection />` from the Settings page. Leave `ContentCreationController`,
`create-wizard/`, and all backend session infra fully in place but now unreachable via
normal navigation (still reachable via direct URL/API for one release, as a safety net).
**Before this chunk ships:** confirm via live Railway DB access (blocked this session,
per Part 3) that no Development/Production tenant has `UseNewWizard` or
`UseNewCourseCreation` explicitly set to a value that would make this chunk a surprise
for a real tenant.

**Chunk 2 — Delete legacy frontend.** Delete all 26 files under `create-wizard/`
(after relocating `FlaggedText.tsx` — used by the public `external-review` route — and
confirming `ValidationProgressPanel.tsx`/`ValidationSectionCard.tsx` are copied/moved
to wherever `learning-wizard/steps/ValidateStep.tsx` will import them from post-deletion,
since it currently reaches into `create-wizard/steps/validate/` for these). Delete
`web/src/app/(authenticated)/admin/toolbox-talks/create/page.tsx`. Split
`content-creation.ts`/`use-content-creation.ts` — remove session-scoped exports, keep
`useValidationRuns`/`useValidationRun`/`useSectionDecision`/`useRegulatoryApplicability`/
`contentCreationKeys` (rename the file if desired, but functionally these must survive).
Remove the `useCoursePreference`-false fallback branch in `courses/new/page.tsx` (Part 4)
so it no longer redirects to a deleted route. Delete `use-subtitle-hub.ts`,
`useWizardPreference.ts` (+ its test), `wizard-toggle-section.tsx`. Fix the dormant
`ToolboxTalkDetail.tsx` Edit-button gate (Part 1) as a bundled one-line cleanup.

**Chunk 3 — Delete legacy backend handlers/endpoints.** Delete
`ContentCreationController.cs`, `IContentCreationSessionService`/`ContentCreationSessionService`,
`VideoTranscriptionJob.cs`, `ContentCreationParseJob.cs`, `ExpiredSessionCleanupJob.cs`
(+ its `Program.cs` Hangfire registration), the `ContentCreationSessions` bulk-delete
step in `ResetTenantDataCommandHandler.cs`. **Surgically remove** (not delete-the-file)
the two `isNewWizard`-gated legacy branches from `TranslationValidationJob.cs` (Part 2)
— this is the highest-risk single edit in the whole removal (Part 9).

**Chunk 4 — Delete legacy tests + EF migration for the table.** Delete
`ContentCreationSessionPublishTests.cs`, the E2E spec + page object + constant (Part 7).
Update `TestTenantSeeder.cs` to stop seeding `ContentCreationSession` rows (after
confirming no other test depends on them). Run `dotnet ef migrations add
DropContentCreationSessions` (CLI-generated, both `.cs` + `.Designer.cs` per CLAUDE.md
Note 28) to drop the entity/table, and `dotnet ef migrations add
RemoveUseNewWizardSetting` style cleanup if any residual `TenantSettings` rows with
`Key='UseNewWizard'` should be purged (optional — see Part 3, no DB column exists for
this key, only sparse rows; a data-only cleanup script may suffice instead of a schema
migration).

**Chunk 5 — Update docs.** Rewrite CLAUDE.md Note 29 (currently the canonical
description of the two-wizard cutover) to describe the single-wizard end state; remove
now-stale references to `UseNewWizard`/legacy wizard from other CLAUDE.md sections that
mention it.

**Why 5 and not fewer:** collapsing Chunks 2+3 risks a broken deploy window where the
frontend has no wizard route but the backend controller still exists unreferenced (safe
but wasteful) or vice versa (frontend still linking to a route whose backend just got
deleted — a hard break). Collapsing 1 into 2 removes the reversible safety-net window the
project's own conventions favor for toggle-then-code sequencing. **Why not more:**
splitting Chunk 3 further (e.g., isolating the `TranslationValidationJob` surgery into
its own chunk) is defensible if the team wants extra caution there — see Part 9's
recommendation to treat that edit with its own focused review regardless of chunk
boundaries.

---

## Part 9 — Risk assessment

### Highest risk: `TranslationValidationJob.cs`'s `isNewWizard` branches

This is the single closest point of contact between legacy-only code and the new
wizard's live, active job. A mistake here — e.g., accidentally removing the
`TryUpdateSessionStatusAsync` call entirely instead of just its now-dead internal
session-lookup logic, or misreading which branch is legacy vs. shared — risks breaking
translation validation for every tenant on the new wizard, not just cleaning up legacy
code. **Recommend this specific edit gets its own focused review pass, independent of
the rest of Chunk 3**, even if it ships in the same commit/PR.

### Second highest: shared files living inside `create-wizard/`'s directory tree

`FlaggedText.tsx`, `ValidationProgressPanel.tsx`, `ValidationSectionCard.tsx` physically
live under `create-wizard/steps/validate/` but are imported by code outside the legacy
wizard (the public `external-review` route and `learning-wizard/steps/ValidateStep.tsx`
respectively). **Directory-based deletion (`rm -rf create-wizard/`) would silently break
both**, one of them a public, unauthenticated route — the kind of break that wouldn't
surface in an admin-focused smoke test. These three files must be relocated to a shared
components directory *before* the directory delete, confirmed by re-running a full
`tsc`/build after the move, not just after the eventual deletion.

### Third: `content-creation.ts`/`use-content-creation.ts` split

Wholesale deletion of these two files (an easy mistake if scanning by filename —
"content-creation" reads as unambiguously legacy) would break `ValidationHistoryTab`,
`ValidationRunDetailView`, `RegulatoryScorePanel`, `ReviewScreen`, and three
`learning-wizard` step components. The split must preserve the non-session exports
exactly — a good verification step is a full-repo grep for `from '@/lib/api/toolbox-talks/use-content-creation'`
and `from '@/lib/api/toolbox-talks/content-creation'` after the split, confirming every
surviving import resolves to something still exported.

### Fourth: Course creation's residual reachability

Per Part 3/6, `UseNewCourseCreation=false` (settable only via direct API, no UI) or
`?coursemode=old` are the only ways the legacy wizard's Course-publish path is still
reachable today. If any Development/Production tenant has this set (unconfirmed —
Railway access blocked both this session and the 2026-07-09 session), that tenant would
lose Course creation entirely on deletion with no fallback, since the new course flow
(`CourseForm.tsx`) is a *different* implementation, not a toggle-compatible successor —
a tenant relying on the old flow's document-splitting behavior specifically (rather than
compose-existing) has no equivalent feature to fall back to (per
`docs/old-vs-new-course-parity-recon.md` Part 4, row 3: "AI-assisted document splitting
into a course is not preserved in the new path... deferred"). **This must be confirmed
against live tenant data before Chunk 3 ships**, not assumed safe from local-only
evidence.

### Flagged uncertainties — "probably safe, can't fully prove from static analysis"

1. **Whether `tests/QuantumBuild.Tests.E2E` runs in any CI pipeline.** No workflow files
   exist in the repo to confirm either way. Deleting `content-creation.spec.ts` without
   knowing this risks either an invisible CI break (if it's wired somewhere outside the
   repo, e.g. Railway's own build step or an external CI config) or, if genuinely dead,
   means this recon can't confirm the deletion has zero test-execution consequence.
2. **Whether any Railway tenant has `UseNewWizard=true` or `false` explicitly set**, and
   the parallel question for `UseNewCourseCreation=false`. Both blocked by Railway CLI
   auth failure (`invalid_grant`) in this session and the 2026-07-09 session alike — this
   is a recurring environmental limitation, not a one-off. A human with direct Railway/DB
   access should run the same query `docs/wizard-toggle-retirement-recon.md` Part 1 used
   locally against Development and Production before Chunk 1 ships.
3. **Whether any legacy `ContentCreationSession` row currently sits mid-pipeline** (Draft
   status, no `ToolboxTalk` published yet) on a live tenant, which would mean an admin
   loses in-progress work with no migration path (Part 6). This is knowable only via a
   live DB query (`SELECT * FROM "ContentCreationSessions" WHERE "Status" NOT IN
   ('Completed','Abandoned')`), not from static code analysis.
4. **Whether the `TranslationValidationJob` legacy branches are truly the *only* two
   touchpoints.** The file is large (per the domain entity's audit-trail-heavy design)
   and this recon traced only the `ContentCreationSession`-referencing lines via grep +
   targeted read; a full line-by-line audit of the entire job class was out of scope for
   this recon and should happen as part of Chunk 3's own review, not be assumed complete
   from this document alone.
5. **Whether `ValidationProgressPanel.tsx`/`ValidationSectionCard.tsx`'s relocation
   target should be a new shared directory or moved directly into `learning-wizard/components/`.**
   Static analysis shows *that* they're shared, not the cleanest place to put them —
   that's a design decision for the implementation chunk, not something this recon
   should prescribe.

---

## Summary checklist for the implementer

- [ ] Confirm live-tenant `UseNewWizard` / `UseNewCourseCreation` values on Railway Dev + Prod (blocked here twice — needs real DB/Railway access)
- [ ] Confirm `tests/QuantumBuild.Tests.E2E` CI-wiring status
- [ ] Relocate `FlaggedText.tsx`, `ValidationProgressPanel.tsx`, `ValidationSectionCard.tsx` out of `create-wizard/` before directory deletion
- [ ] Split `content-creation.ts`/`use-content-creation.ts`, verify no broken imports repo-wide
- [ ] Treat the `TranslationValidationJob.cs` `isNewWizard` branch removal as its own focused review, regardless of chunk boundaries
- [ ] Communicate to any tenant with an in-progress legacy session that it cannot be migrated (Part 6)
- [ ] Update CLAUDE.md Note 29 and any other stale references post-deletion
