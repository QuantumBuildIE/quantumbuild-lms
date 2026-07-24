# Regulation Mapping — "Live Learnings Only" Recon

Read-only recon. Date: 2026-07-24. Branch: `transval`.

Customer ask (Platinum tier): regulation-mapping views should only show mappings tied to "live"
learnings — not Draft, Archived (deactivated), or Deleted content.

---

## 1. BACKLOG entry finding

**Not found.** Searched `BACKLOG.md` (repo root) and the CLAUDE.md Backlog section for `live`,
`Draft`, `Archived`, `non-live`, `regulation.mapping`, `Platinum` (case-insensitive) — zero hits
in either. No TODO/FIXME comments referencing draft/archived/live filtering were found near any
mapping code (`RequirementMappingService.cs`, `RequirementMappingJob.cs`,
`RequirementMappingController.cs`, `RegulatoryBrowseController.cs`). This is a fresh ask, not a
previously-scoped backlog item.

---

## 2. Definition of "live" for this codebase

**ToolboxTalk** (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalk.cs:10-77`)
has **three independent lifecycle signals**, all of which matter:

| Field | Type | Source |
|---|---|---|
| `Status` | `ToolboxTalkStatus` enum: `Draft=1, Processing=2, ReadyForReview=3, Published=4` | `ToolboxTalkStatus.cs:6-27`, `ToolboxTalk.cs:77` |
| `IsActive` | `bool`, default `true` | `ToolboxTalk.cs:72` |
| `IsDeleted` | `bool`, inherited from `BaseEntity` via `TenantEntity` | soft delete |

`Status` and `IsActive` are **orthogonal** — a talk can be `Published` and `IsActive=false`
simultaneously. Confirmed by `ToggleToolboxTalkActiveCommandHandler.cs:16-27`, which flips
`IsActive` with no touch to `Status`, and by `DeleteToolboxTalkCommandHandler.cs:41-44` /
`:56-57`, whose error messages explicitly tell the admin to "deactivate the talk instead" of
deleting when there are active schedules/pending assignments — i.e. deactivation
(`IsActive=false`) is the sanctioned "archive a published talk" mechanism in this codebase, not a
`Status` transition. There is no `Archived` value in `ToolboxTalkStatus` — "archived" in product
terms **is** `IsActive=false` on an otherwise-`Published` talk.

**ToolboxTalkCourse** (`ToolboxTalkCourse.cs:9-30`) has only `IsActive` (bool, default `true`) and
`IsDeleted` — no `Status` enum, no separate published/draft concept for courses.

**Existing "live" convention already used elsewhere in the app:**
- `RequirementMappingService.GetContentOptionsAsync` (`RequirementMappingService.cs:460-472`) —
  talks: `t.Status == ToolboxTalkStatus.Published`; courses: `c.IsActive`. **Talks are not also
  filtered on `IsActive` here** — see gap noted in §5.
- `CreateToolboxTalkScheduleCommandHandler.cs:43` — `if (!toolboxTalk.IsActive)` blocks scheduling
  an inactive talk (no `Status` check alongside it in this handler).
- `AutoAssignmentService.cs:104-111` (standalone talk auto-assign) — `t.Status ==
  ToolboxTalkStatus.Published` only, no `IsActive` check (inconsistent with the course branch two
  lines above it at `:43-46`, which does check `c.IsActive`).
- `DailyTranslationScanJob.cs:81` and `RequirementMappingJob`'s enqueue sites — trigger only on
  the publish transition (see §6), not a general "is live" query.

**Conclusion — recommended "live" predicate for this fix:**
- Talk: `Status == Published && IsActive == true && !IsDeleted`
- Course: `IsActive == true && !IsDeleted`

No single existing query in the codebase combines all three talk conditions today; `Published` and
`IsActive` are checked in different places but never together in one filter.

---

## 3. Inventory of mapping-display surfaces

| Surface | Backend endpoint | Backend method | Frontend page |
|---|---|---|---|
| Pending mappings review | `GET /api/toolbox-talks/requirement-mappings/pending` | `RequirementMappingService.GetPendingMappingsAsync` (`RequirementMappingService.cs:36-65`) | `web/src/app/(authenticated)/admin/regulatory/mappings/page.tsx` |
| Confirm-all suggested | `POST /api/toolbox-talks/requirement-mappings/confirm-all` | `RequirementMappingService.ConfirmAllSuggestedAsync` (`:106-127`) | same page, bulk-action button |
| Compliance checklist | `GET /api/toolbox-talks/requirement-mappings/compliance/{sectorKey}` | `RequirementMappingService.GetComplianceChecklistAsync` (`:147-383`) | `web/src/app/(authenticated)/admin/regulatory/compliance/page.tsx` |
| Inspection/Training Evidence Pack PDF | `POST /api/toolbox-talks/requirement-mappings/compliance/{sectorKey}/generate-report` | `InspectionReportService.GenerateReportAsync` (`InspectionReportService.cs:50-99`) — **reuses `GetComplianceChecklistAsync` directly** (`:58`) | download link on compliance page |
| Manual-mapping content picker | `GET /api/toolbox-talks/requirement-mappings/content-options` | `RequirementMappingService.GetContentOptionsAsync` (`:456-477`) | dropdown on mappings page's "Add manual mapping" dialog |
| Unconfirmed-count badge | `GET /api/toolbox-talks/requirement-mappings/unconfirmed-count` | `RequirementMappingService.GetUnconfirmedCountAsync` (`:129-145`) | warning banner in the talk/course assignment flow (contextual to one already-known talk/course, not a list) |
| Regulatory browse (requirements only, no mapping/talk data) | `GET /api/regulatory/browse` | `IRequirementIngestionService.GetBrowsableRequirementsAsync` via `RegulatoryBrowseController.cs:44-57` | `web/src/app/(authenticated)/admin/regulatory/regulations/page.tsx` — **out of scope**: returns bodies→documents→requirements only, never joins to `RegulatoryRequirementMapping` or any talk/course |
| AI candidate mapping generation | Hangfire, no HTTP endpoint | `RequirementMappingJob.MapRequirementsAsync` (`RequirementMappingJob.cs:61-118`) | none (background) — feeds rows that the above surfaces later display |

Dashboard (`GetToolboxTalkDashboardQueryHandler.cs`) was checked and has **no** regulatory/mapping
tie-in — it only aggregates talk completion KPIs. Not a surface.

---

## 4. Current behaviour per surface — does it leak non-live mappings today?

| Surface | Leaks Draft/inactive/deleted talks or courses? | Evidence |
|---|---|---|
| Pending mappings (`GetPendingMappingsAsync`) | **Yes.** No filter on `ToolboxTalk.Status`/`IsActive` or `Course.IsActive` anywhere in the query. | `RequirementMappingService.cs:44-52` — `.Where(m => m.TenantId == tenantId)` only, then `.Include(m => m.ToolboxTalk).Include(m => m.Course)` with no predicate on those navigations. |
| Confirm-all (`ConfirmAllSuggestedAsync`) | **Yes** — will confirm suggested mappings for Draft/inactive content indiscriminately. | `:110-112` — `.Where(m => m.TenantId == tenantId && m.MappingStatus == RequirementMappingStatus.Suggested)`, no talk/course join at all. |
| Compliance checklist (`GetComplianceChecklistAsync`) | **Yes.** Mapping query at `:219-223` has no talk/course state filter. A `Confirmed` mapping to a Draft or deactivated talk that already has a completed validation run (validation can run pre-publish per the wizard's step order — Translate & Validate is step 5, Publish is step 6) will register as `"Covered"` (`:304-319`). | `:219-223`, coverage-status logic `:303-319`. |
| Inspection Evidence Pack PDF | **Yes**, inherits the leak — it is a pure re-render of `GetComplianceChecklistAsync`'s output. | `InspectionReportService.cs:58` |
| Content-options dropdown (`GetContentOptionsAsync`) | **No for talks** (already `Status == Published`), **partially for talks** — does not also check `IsActive`, so a `Published` but deactivated talk still appears as a manual-mapping target. **No for courses** — filters `IsActive` only (courses have no Status). | `:460-472` |
| Unconfirmed-count | N/A — takes an explicit `toolboxTalkId`/`courseId` from the caller, doesn't enumerate; whatever content the caller already has open. Not a list-leak surface. | `:129-145` |
| AI candidate generation (`RequirementMappingJob`) | **No leak today in practice** — only ever enqueued after a successful publish (see §6) — but the job itself does not re-check status at execution time, so if publish → immediate deactivate happened before the (fire-and-forget) job runs, or if the job is ever invoked outside the publish flow, it would run on non-live content. `BuildContentStringAsync` (`:120-172`) filters only `!IsDeleted`, not `Status`/`IsActive`. Confirmed directly by the integration test suite — `RequirementMappingJobCandidateGatingTests.CreateTalkWithSectionAsync` (`RequirementMappingJobCandidateGatingTests.cs:175-194`) creates talks with `IsActive = true` and **no `Status` set at all**, meaning they default to `Draft`, and the job still successfully processes them. | `RequirementMappingJob.cs:120-172`; test evidence as cited |

---

## 5. Backend query audit

| Handler | File:line | Current filter on talk/course state | Fix shape |
|---|---|---|---|
| `GetPendingMappingsAsync` | `RequirementMappingService.cs:44-52` | None | **Structural** — needs a `Where` clause added after the `Include`s (EF Core can't filter on an `Include`d nav property directly in the same fluent chain the way it's written; would need either a join/`Where` on `m.ToolboxTalk.Status`/`IsActive` and `m.Course.IsActive` with null-coalescing for the "other side is null" case, or restructure as an explicit join). Non-trivial one-line, but contained to a single method. |
| `ConfirmAllSuggestedAsync` | `RequirementMappingService.cs:110-112` | None | **Structural** — currently has no navigation include/join at all; needs `.Include(m => m.ToolboxTalk).Include(m => m.Course)` added plus the same live predicate. Behavioral question, not just mechanical: should "Confirm All" silently skip non-live mappings, or still confirm them but only *display* filters change? (see §9 recommendation). |
| `GetComplianceChecklistAsync` | `RequirementMappingService.cs:219-223` | None | **One-line-ish** — filter can be added directly to the existing `.Where` before/after `.Include`, e.g. `&& (m.ToolboxTalkId.HasValue ? (m.ToolboxTalk!.Status == ToolboxTalkStatus.Published && m.ToolboxTalk.IsActive) : m.Course!.IsActive)`. Contained to one query, but the coverage-status logic downstream (`:303-319`) also needs a decision: does excluding a non-live mapping ever flip a requirement's status from `Covered` to `Gap`/`Pending` if it was the *only* mapping? That's intended and automatic once the mapping query is filtered — no separate change needed there. |
| `GetContentOptionsAsync` | `RequirementMappingService.cs:460-465` | Talks: `Status == Published` only (missing `IsActive`) | **One-line** — add `&& t.IsActive` to the existing `Where`. |
| `RequirementMappingJob.BuildContentStringAsync` | `RequirementMappingJob.cs:125-128` (talk), `145-150` (course) | `!IsDeleted` only | **One-line each**, if the decision is to also gate the AI-suggestion job itself (see §6/§9 — likely unnecessary since it's already publish-triggered). |
| `RequirementMappingJob.LoadApprovedRequirementsAsync` | `:174-201` | N/A — filters `RegulatoryRequirement`/`RegulatoryProfile` state, not talk/course. Not in scope. | — |

**Centralization assessment:** The mapping-fetch logic is **not** centralized in
`ApplicableFrameworksService` (that service only resolves tenant sector/Standard entitlements —
`GetTenantEntitlementsAsync`, `GetApplicableFrameworksAsync` — it never touches
`RegulatoryRequirementMapping` rows). All four mapping-list queries
(`GetPendingMappingsAsync`, `ConfirmAllSuggestedAsync`, `GetComplianceChecklistAsync`,
`GetContentOptionsAsync`) are separate, hand-written LINQ queries inside
`RequirementMappingService`, each duplicating its own `Where`/`Include` shape. There is **no
shared helper** for "load mappings joined to live talks/courses" — a live-only filter would need
to be applied in (at minimum) 3 separate places unless a shared predicate/extension method is
introduced as part of this fix (recommended — see §9).

---

## 6. Downstream implications

| Downstream consumer | Feeds from mapping data? | Needs updating too, or is UI-only filtering sufficient? |
|---|---|---|
| `RequirementMappingJob` (AI suggestion generation) | Writes mappings, doesn't read/display them. Triggered only from publish success paths: `ToolboxTalksController.cs:682` (talk publish), `ContentCreationSessionService.cs:1677/1775/2176` (wizard publish flows for talk/course/slideshow). At trigger time the content is definitionally about to be (or just became) live. | **UI-only filtering is sufficient** for the job itself — it already only fires on publish. No change needed to the job's trigger sites. Optionally harden `BuildContentStringAsync` to bail if the talk/course is not live at execution time (defensive, low-value given current trigger discipline) — see §5 note on fire-and-forget timing. |
| Compliance % / coverage calculation (`CoverageStatus`, `CoveragePercentage`, `CoveredCount`/`PendingCount`/`GapCount`) | Computed **inside** `GetComplianceChecklistAsync` from the same mapping list that would be filtered. | **Automatically correct once the query is fixed** — no separate computation to update; percentages are derived from the filtered `mappings` list in the same method (`:258-260`, `:354-357`). |
| Training Evidence Pack PDF export | Calls `GetComplianceChecklistAsync` directly (`InspectionReportService.cs:58`) | **Automatically correct** once compliance checklist is fixed — single call site, no separate query to touch. |
| Skills Matrix / other reports (`SkillsMatrixDto`, compliance/overdue/completions reports) | Verified — **no** connection to `RegulatoryRequirementMapping` found anywhere in `ToolboxTalksController` reports endpoints or their handlers. Not a downstream consumer. | N/A |
| AI candidate generation prompt content (which requirements get sent to Claude) | `LoadApprovedRequirementsAsync` filters `RegulatoryRequirement`/`RegulatoryProfile` state (already gated by tenant entitlements per commit `f37add8`), unrelated to talk/course liveness. | N/A — different axis of filtering, not affected by this change. |

---

## 7. Data preservation confirmation

**No code path deletes or hard-removes `RegulatoryRequirementMapping` rows on a talk/course status
change.** Evidence:

1. `RegulatoryRequirementMappingConfiguration.cs:65-74` — both FKs (`ToolboxTalkId`, `CourseId`)
   are configured `OnDelete(DeleteBehavior.Restrict)`. Even a hard delete of the parent row (which
   doesn't happen in this codebase — see next point) would be blocked at the DB level, not cascade.
2. `DeleteToolboxTalkCommandHandler.cs:61` — talk "delete" is `toolboxTalk.IsDeleted = true;` (soft
   delete only). `DeleteToolboxTalkCourseCommandHandler.cs:31` — same pattern for courses.
3. `ToggleToolboxTalkActiveCommandHandler.cs:22` — deactivation is `talk.IsActive =
   request.Active;`, a plain field flip, no cascading writes to any mapping table.
4. No grep hit anywhere in the codebase for code that queries and removes/updates
   `RegulatoryRequirementMappings` rows in response to a talk/course status or `IsActive` change
   (searched `RegulatoryRequirementMappings\.` across `src/` — 66 files matched, all either
   migrations, the DbContext registration, the configuration class, `IToolboxTalksDbContext`, or
   the service/job/controller/handler already covered above — none in any talk/course
   status-change handler).

**The filter is purely query-time, not a stored/frozen flag.** `RegulatoryRequirementMapping` has
no snapshot field for talk state (`RegulatoryRequirementMapping` domain entity carries only
`TenantId`, `RegulatoryRequirementId`, `ToolboxTalkId`/`CourseId`, `MappingStatus`,
`ConfidenceScore`, `AiReasoning`, `ReviewedBy/At`, `ReviewNotes`). A live-only `WHERE` clause reads
current `ToolboxTalk.Status`/`IsActive` (or `Course.IsActive`) at query time via the navigation
property — so **reactivating a talk or re-publishing it automatically restores mapping visibility**
with no reconciliation job or backfill required. This confirms the fix is a pure read-time filter,
not a data-migration concern.

---

## 8. Test surface inventory

Grepped `tests/` for `RequirementMapping`, `RegulatoryBrowse`, `ComplianceChecklist`,
`GetPendingMappings`, `GetComplianceChecklist`. Two files matched:

- **`tests/QuantumBuild.Tests.Integration/ToolboxTalks/RequirementMappingJobCandidateGatingTests.cs`**
  — covers `RequirementMappingJob`'s *entitlement*-based candidate gating (Standard subscription
  vs. sector overlap), not talk-liveness. All test talks are created via
  `CreateTalkWithSectionAsync` (`:175-208`) with `IsActive = true` and **`Status` left unset**
  (defaults to `Draft`) — i.e. every existing test in this file runs the job against a
  non-published talk and asserts on prompt content, not on mapping-list display. No test exercises
  Draft/inactive filtering in a *display* query at all.
- **`tests/QuantumBuild.Tests.Integration/ToolboxTalks/ComplianceStandardsDisplayTests.cs`** —
  covers Standard-vs-Regulation display attribution on the compliance checklist
  (`IsCurrentlyApplicable` flag territory per note in `RequirementMappingService.cs:479-484`), not
  talk/course liveness. No `IsActive = false` or explicit `Draft`-vs-`Published` fixture setup
  found in this file.

**No existing test creates a Draft, deactivated, or soft-deleted talk/course specifically to
assert it is excluded from (or leaks into) `GetPendingMappingsAsync`,
`GetComplianceChecklistAsync`, or `GetContentOptionsAsync` output.** This is new test surface for
whichever chunk implements the fix — every mapping-display query would need at least one negative
test case (mapping to a Draft talk, mapping to a deactivated-but-Published talk, mapping to a
soft-deleted talk) plus one positive "reactivation restores visibility" case per §7.

---

## 9. Recommended fix scope

**Chunk size estimate: Small–Medium.**

The change is conceptually simple (a `WHERE`/predicate addition) but touches 3 separate hand-rolled
LINQ queries with no shared abstraction today, plus a genuine product decision to make first (see
below), plus new test coverage that doesn't exist yet. That combination pushes it just past
"small."

**Files that would need to change:**

1. `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Mapping/RequirementMappingService.cs`
   — `GetPendingMappingsAsync` (:44-52), `GetComplianceChecklistAsync` (:219-223),
   `GetContentOptionsAsync` (:460-465, add `IsActive` to the talk branch), and — pending the
   product decision below — `ConfirmAllSuggestedAsync` (:110-112).
2. Possibly a new small shared helper (e.g. a static predicate or extension method
   `IsLiveTalkOrCourse` / a reusable `Where` fragment) introduced in the same file or a shared
   location, to avoid the "3 separate copies" duplication flagged in §5. Not strictly required —
   could ship as 3 independent inline filters — but recommended given this service already has a
   pattern of duplicating entitlement logic that a prior chunk (`ApplicableFrameworksService`) was
   introduced specifically to de-duplicate.
3. Test files: new cases in `RequirementMappingJobCandidateGatingTests.cs` (or a new file, e.g.
   `RequirementMappingLiveFilterTests.cs`) covering Draft/inactive/deleted exclusion and
   reactivation restoration, per §8.
4. No frontend file changes are structurally required — both `mappings/page.tsx` and
   `compliance/page.tsx` are unaware of talk/course state today and simply render whatever the
   backend returns; filtering server-side is sufficient. (Frontend changes only needed if the
   product decision below calls for a badge instead of a hide — see next section.)
5. No migration required — this is a query-time filter with no schema change.

**Open product decision — Compliance tab vs. Mappings tab may legitimately want DIFFERENT
behavior. Do not assume identical treatment:**

- **Compliance tab** (`/admin/regulatory/compliance`) is framed as "what covers me right now" —
  an operational/audit-readiness view, and its output feeds directly into the Training Evidence
  Pack PDF handed to inspectors (§6). Showing a requirement as `"Covered"` on the strength of a
  mapping to a Draft or deactivated talk is arguably worse than a UX inconsistency — it's a
  **compliance-accuracy correctness bug** (evidence that doesn't actually exist for inspection
  purposes). Strong case for **hard filtering** (exclude, not badge) here.
- **Mappings tab** (`/admin/regulatory/mappings`) is an **admin review/curation tool** — an admin
  confirming/rejecting AI suggestions may reasonably want to see (and confirm) a mapping to a talk
  that is currently in Draft but about to be published, or review a mapping to a talk they just
  deactivated to decide whether to re-map it elsewhere. Hard-hiding here could strand orphaned
  `Suggested` mappings that an admin can never see or act on again until the talk becomes live —
  a worse admin experience than showing a "Draft" or "Inactive" badge (the existing chunk-4
  precedent: `IsCurrentlyApplicable` flag + amber-badge pattern, not a hide, per
  `RequirementMappingService.cs:479-484` and the referenced `ValidationSectionCard`
  amber-badge convention elsewhere in the codebase). This mirrors the existing
  "No longer applicable" badge pattern already shipped for unsubscribed-Standard mappings — the
  same visual/UX vocabulary could extend to "Draft" / "Inactive" without hiding the row.
- **Recommendation:** treat these as two separate design questions inside the same chunk — likely
  **hide on Compliance** (and therefore the PDF, since it reuses the same query) but **badge, not
  hide, on Mappings** (and its `content-options`/manual-mapping flows, where an admin actively
  managing content should still see everything). This doubles the surface-specific logic slightly
  (Compliance needs a `Where` exclusion; Mappings needs an `Include` + a computed `IsLive`/`Status`
  field surfaced to the DTO for the frontend to badge) but avoids a data-hiding UX regression for
  the admin review workflow. `ConfirmAllSuggestedAsync` then inherits whatever the Mappings-tab
  decision is (if Mappings shows Draft mappings with a badge, "Confirm All" arguably should skip
  them rather than bulk-confirm mappings to unpublished content — a second small decision to make
  explicit in the fix's spec, not left implicit).
