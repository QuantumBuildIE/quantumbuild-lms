# Certificate Generation Recon

**Status:** Read-only recon. No code changed. All claims below are cited to file:line; a few are backed by direct queries against the local Development database (`rascor_stock` on `127.0.0.1:5432`), clearly labelled where used.

**Symptom under investigation:** operator completes an assigned toolbox talk (video, sections, quiz pass, signature, complete) with `GenerateCertificate = true` on the talk, but no `ToolboxTalkCertificate` row is created.

---

## 1. CertificateGenerationService API summary

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CertificateGenerationService.cs`
Interface: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/ICertificateGenerationService.cs`

Registered DI: `services.AddScoped<ICertificateGenerationService, Services.CertificateGenerationService>();` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DependencyInjection.cs:39`. Confirmed present and correctly scoped (not registered via an interface-vs-concrete Hangfire trap — this service is never enqueued as a job, always called in-process — see §2).

### `GenerateTalkCertificateAsync(ScheduledTalk completedTalk, string? signatureDataUrl, CancellationToken ct)`

`CertificateGenerationService.cs:22-111`

Guard clauses, in execution order:

1. **`CertificateGenerationService.cs:28-33`** — `if (completedTalk.CourseAssignmentId.HasValue) return null;` — explicitly skips certificate generation for any `ScheduledTalk` that belongs to a course (course certs are handled by a separate path, §4). Logged at Debug level only — **no visible trace at Info/Warning level that this guard fired**.
2. **`CertificateGenerationService.cs:36-44`** — re-loads the `ToolboxTalk` via `IgnoreQueryFilters()` + `!IsDeleted`; returns `null` with a Warning log if not found.
3. **`CertificateGenerationService.cs:46-50`** — `if (!talk.GenerateCertificate) return null;` — the core gate. Logged at Debug level only.
4. **`CertificateGenerationService.cs:52-63`** — re-loads Employee via a fresh query (`context.ScheduledTalks...Select(st => st.Employee)`); returns `null` with a Warning log if not found.
5. **`CertificateGenerationService.cs:65-91`** — builds the certificate number (`GenerateCertificateNumber`, `CertificateGenerationService.cs:203-211`, reads tenant setting `TalkCertificatePrefix` with a hard-coded fallback default — see §6) and constructs the `ToolboxTalkCertificate` entity (not yet persisted).
6. **`CertificateGenerationService.cs:93-101`** — generates the PDF in-memory (QuestPDF, `GenerateCertificatePdf`, `CertificateGenerationService.cs:228-367`) and uploads to R2 (`UploadCertificatePdf`, `CertificateGenerationService.cs:213-226`, delegates to `IR2StorageService.UploadCertificateAsync`). If the upload fails (`result.Success == false`), logs an Error and **returns `null`** — this is the only place a `null` return does *not* correspond to a business-rule opt-out; it represents a genuine infrastructure failure but is indistinguishable from "opted out" to the caller.
7. **`CertificateGenerationService.cs:103-110`** — sets `PdfStoragePath`, `Add()`s the entity, `SaveChangesAsync`, returns the persisted certificate.

**None of steps 1-7 throw on the "normal" business-rule paths** (course-scoped, flag-off, not-found) — they all return `null` cleanly. The only paths that can genuinely throw are DB/network exceptions in steps 2/4/6/7 (EF query failures, PDF rendering exceptions, R2 client exceptions not caught internally by `R2StorageService`, `SaveChangesAsync` failures). `R2StorageService.UploadCertificateAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Storage/R2StorageService.cs:219-256`) wraps its body in its own try/catch and returns a `FailureResult` rather than throwing, so R2 failures surface as the graceful `null` path in step 6, not as an exception.

### `GenerateCourseCertificateAsync(ToolboxTalkCourseAssignment completedAssignment, string? signatureDataUrl, CancellationToken ct)`

`CertificateGenerationService.cs:113-201` — same shape, gated on **`course.GenerateCertificate`** (`CertificateGenerationService.cs:131-135`, `!course.GenerateCertificate` → `null`, Debug log only), not the talk's own flag. This is the entity referenced in commit `2cc0f42`'s message as `CertificateGenerationService.cs:131` (confirmed).

---

## 2. All call sites with trigger conditions

Exactly three call sites in the whole codebase (grep for `GenerateTalkCertificateAsync`/`GenerateCourseCertificateAsync`/`ICertificateGenerationService` across `src/`):

| # | File:Line | Trigger | Gate at this layer |
|---|---|---|---|
| 1 | `CompleteToolboxTalkCommandHandler.cs:212-234` | Employee-facing `/complete` endpoint, **standalone talk only** | `if (scheduledTalk.CourseAssignmentId.HasValue) { ... } else { ...GenerateTalkCertificateAsync... }` — `CompleteToolboxTalkCommandHandler.cs:200-241`. Only reached when the completed talk is **not** part of a course. |
| 2 | `CourseProgressService.cs:70-96` | `UpdateProgressAsync`, called from `CompleteToolboxTalkCommandHandler.cs:202` when `scheduledTalk.CourseAssignmentId.HasValue` is true | Only fires `if (assignment.Status == CourseAssignmentStatus.Completed)` (`CourseProgressService.cs:65`) — i.e. only when *all* required course items are done, not on every talk completion. |
| 3 | `ToolboxTalksController.cs:2514-2553` (`RegenerateCertificate` endpoint, `Learnings.Admin`) | Manual admin retry for a previously-failed generation | No automatic trigger — admin-initiated only, per the `regenerate-certificate` route added in commit `80bad46`. |

**No quiz-submission handler, no domain event, no MediatR notification, and no Hangfire job calls into certificate generation anywhere.** Grepped `IRequestHandler`/`INotificationHandler` implementations referencing `Certificate` — none exist beyond the two command/service call sites above. Certificate generation is purely synchronous, in-process, called directly inside the completion request.

### Areas explicitly checked per the task brief

- **Quiz submission handler** (`SubmitToolboxTalkQuizCommandHandler`, wired from `MyToolboxTalksController`'s `/quiz/submit`) — does **not** call certificate generation. It only records the `ScheduledTalkQuizAttempt`; the `Complete` action is a separate, later user step. Correct by design — quiz pass alone should not issue a certificate before the employee signs.
- **Talk/ScheduledTalk completion command handler** (`/complete`) — **does** call it, for standalone talks (§1 above, call site #1).
- **Course completion handler** — has its own separate certificate logic via `CourseProgressService`, gated on the course, not the talk (§4).
- **Domain event / notification handler** — none found; MediatR is used for CQRS here but no `INotificationHandler` is registered for completion or certificate concerns.
- **Hangfire jobs** — none of the 13 documented background jobs (`ContentGenerationJob`, `MissingTranslationsJob`, `TranslationValidationJob`, etc.) reference `ICertificateGenerationService`. Certificate generation is never deferred to a background job, so Notes 21-23 (Hangfire enqueue-via-interface, `TenantId` stamping, DbContext scope-per-unit) do **not** apply here — there is no job-context DbContext-scoping risk because the whole flow runs inside one HTTP-request-scoped `DbContext`.

---

## 3. Full completion flow trace (standalone talk)

1. **Frontend** — `web/src/lib/api/toolbox-talks/my-toolbox-talks.ts:300` — POST `/my/toolbox-talks/${scheduledTalkId}/complete`. This is the only frontend call site for talk completion (grepped `/complete` across `web/src`; the only other unrelated hit is the QR public-portal `/qr/session/.../complete` route, a separate feature).
2. **Controller** — `src/QuantumBuild.API/Controllers/MyToolboxTalksController.cs:357-396` (`Complete` action). Builds a `CompleteToolboxTalkCommand` and sends it via MediatR (`MyToolboxTalksController.cs:370-380`).
3. **Handler** — `CompleteToolboxTalkCommandHandler.cs:45-265` (`Handle`):
   - Loads employee, `ScheduledTalk` (with `SectionProgress`, `QuizAttempts`, `Completion`, `ToolboxTalk.Sections`) — `CompleteToolboxTalkCommandHandler.cs:47-74`.
   - Validates: not already completed/cancelled (`:82-96`), all sections read (`:98-108`), quiz passed if `RequiresQuiz` (`:110-130`), video watch percent if required (`:132-152`).
   - Builds and adds `ScheduledTalkCompletion` (`:171-192`), sets `scheduledTalk.Status = Completed` (`:195`), **commits via `SaveChangesAsync` at `:197` unconditionally** — this persists the completion row and status change regardless of what happens next.
   - **`:200-241`** — branch on `scheduledTalk.CourseAssignmentId`:
     - If set → `_courseProgressService.UpdateProgressAsync(...)` (`:202`) — course path, §4.
     - **Else (standalone)** →
       - `:207` — `await _refresherSchedulingService.ScheduleRefresherIfRequired(scheduledTalk, cancellationToken);` — **called outside any try/catch**.
       - `:210-240` — **wrapped in try/catch** — calls `_certificateService.GenerateTalkCertificateAsync(scheduledTalk, request.SignatureData, cancellationToken)`.
         - If certificate non-null → sets `completion.CertificateUrl`, saves (`:217-221`).
         - If certificate is null → logs a Warning ("Certificate generation returned null...") and **sets `completion.CertificateGenerationFailed = true`**, saves (`:222-234`). This is the explicit, visible failure-tracking path added in commit `80bad46`.
         - If an **exception** is thrown by `GenerateTalkCertificateAsync` itself → caught at `:236-239`, logged as an Error, **`CertificateGenerationFailed` is never set** (the catch block only logs; it does not touch the completion entity). This is a genuine, code-verified gap: an exception inside cert generation looks identical, from the DB's perspective, to "nothing was ever attempted" — no `CertificateUrl`, no `CertificateGenerationFailed = true`, only a server log line.
4. Returns `ScheduledTalkCompletionDto` to the controller, which returns `200 OK` — **the employee always sees a successful completion** regardless of which certificate outcome occurred, by design (comment at `:239`: "Don't rethrow — completion should still succeed").

**Important order-of-operations note:** step `:207` (refresher scheduling) runs *before* the try/catch that shields certificate generation, and is itself not wrapped in a try/catch inside `CompleteToolboxTalkCommandHandler`. Looking inside `RefresherSchedulingService.ScheduleRefresherIfRequired(ScheduledTalk, ...)` (`RefresherSchedulingService.cs:16-90`): it returns immediately and harmlessly if `talk.RequiresRefresher` is false (`RefresherSchedulingService.cs:22-23`) — the common case. If `RequiresRefresher` is true, it creates and saves a new `ScheduledTalk` row (`:45-60`) with **no surrounding try/catch** for that portion (only the subsequent email send at `:79-89` is guarded). If that `SaveChangesAsync` at `RefresherSchedulingService.cs:60` throws for any reason (constraint violation, transient DB error), the exception propagates uncaught all the way to `MyToolboxTalksController.cs:391` (`catch (Exception ex)` → `500`), **after** the completion row was already committed at `CompleteToolboxTalkCommandHandler.cs:197`. In that scenario: the completion row exists (Completed), no certificate is attempted at all (execution never reaches `:210`), `CertificateGenerationFailed` stays at its default `false`, and the employee would see a 500 error on the Complete action despite the talk being marked Completed server-side. This is a real, conditional gap — it only manifests when `RequiresRefresher = true` on the talk.

---

## 4. Course completion path (including commit `2cc0f42` analysis)

"Course complete" is defined purely in `CourseProgressService.UpdateProgressAsync` (`CourseProgressService.cs:14-100`):

- Loads the `ToolboxTalkCourseAssignment` with its `ScheduledTalks` and `Course.CourseItems` (`:16-20`).
- Computes `completedRequiredCount` = count of non-deleted, `Completed`-status `ScheduledTalks` whose `ToolboxTalkId` is in the set of `IsRequired` course items (`:32-40`).
- Transitions `Assigned → InProgress` on first completed required item (`:45-51`).
- Transitions **`→ Completed`** when `completedRequiredCount >= requiredTalkIds.Count && requiredTalkIds.Count > 0` (`:54-60`) — i.e. all required items done. This is the sole trigger for the status transition; it is invoked every time any member talk of the course completes (called from `CompleteToolboxTalkCommandHandler.cs:202`).
- **Only when the assignment just transitioned to `Completed`** (`:65`) does it:
  - Schedule a course refresher if required (`:67`, `RefresherSchedulingService.ScheduleRefresherIfRequired(ToolboxTalkCourseAssignment, ...)`, `RefresherSchedulingService.cs:92-185` — this overload's non-email portion is also unguarded, same class of risk as §3, but out of scope for the single-talk symptom).
  - Generate a **course-level** certificate via `certificateService.GenerateCourseCertificateAsync(assignment, signature, cancellationToken)` (`CourseProgressService.cs:70-96`), wrapped in try/catch (`:70-96`) with the same "log and swallow" pattern as the talk path — same exception-swallow gap applies here too.

Certificate generation for a course-item talk is **entirely gated on `ToolboxTalkCourse.GenerateCertificate`** (`CertificateGenerationService.cs:131-135`), not on the member talk's own `GenerateCertificate` flag. Per `CertificateGenerationService.cs:27-33`, the individual talk's certificate path is explicitly skipped for any `ScheduledTalk` with `CourseAssignmentId` set — there is **no per-talk certificate fallback inside a course**, by design (course certs supersede talk certs; note 19 in CLAUDE.md documents that this course/talk certificate separation is intentional and safe on the deletion side too).

### `ToolboxTalkCourse.GenerateCertificate` — default and history

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkCourse.cs:21` — `public bool GenerateCertificate { get; set; } = false;` — **defaults to `false`**, unlike `ToolboxTalk.GenerateCertificate` (`ToolboxTalkCourse... Entities/ToolboxTalk.cs:188` — defaults `true` since commit `80bad46`, "opt-out rather than opt-in").

### Commit `2cc0f42` — `git show 2cc0f42`

Title: `fix(course): sync session behaviour settings to published course entity` (2026-07-15, one day before this recon).

Confirmed diff in `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs`:

```diff
@@ PublishAsCourseAsync course initializer (around line 1851) @@
-            IsActive = true,
+            IsActive = sessionSettings?.IsActiveOnPublish ?? true,
+            GenerateCertificate = sessionSettings?.GenerateCertificate ?? false,
+            AutoAssignToNewEmployees = sessionSettings?.AutoAssign ?? false,
+            AutoAssignDueDays = sessionSettings?.AutoAssignDueDays ?? 14,
             RequireSequentialCompletion = true
```

**Before this commit, `PublishAsCourseAsync` hard-coded `IsActive = true` and never read `GenerateCertificate` (or `AutoAssign*`) from the wizard's session settings at all — every course published through the new wizard prior to 2026-07-15 was published with `GenerateCertificate` stuck at the entity default of `false`, regardless of what the admin selected on the wizard's Settings step.** The commit message itself confirms this was a genuine, previously-unnoticed gap ("Adjacent gap identified during the cover image persistence fix... left GenerateCertificate... at their entity defaults, never reading from courseSessionSettings").

The commit's own message also confirms, by contrast, that **`PublishAsLessonAsync` (the standalone-talk publish path) already synced `GenerateCertificate` correctly** — verified independently at `ContentCreationSessionService.cs:1449` (`draftTalk.GenerateCertificate = courseSessionSettings?.GenerateCertificate ?? false;`, inside the "resume existing draft talk" branch) and `ContentCreationSessionService.cs:1726` (`GenerateCertificate = sessionSettings?.GenerateCertificate ?? false,`, inside the "create new talk" branch). Both branches read the wizard's `SessionSettingsDto.GenerateCertificate` field, and the frontend wizard Settings steps do expose a Generate Certificate toggle for standalone talks (`web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`, `web/src/features/toolbox-talks/components/create-wizard/steps/SettingsStep.tsx`, `settingsSchema.ts`) — so **the standalone-talk sync path was not the bug the commit fixed, and is not implicated for the symptom under investigation if the tested talk truly was a standalone (non-course) talk.**

**Residual gap after `2cc0f42`:** the fix only changes behaviour for courses published *after* 2026-07-15. **No migration or backfill was run** to correct `GenerateCertificate` on courses that were already published via the new wizard before the fix — those rows remain permanently `false` in the database unless an admin manually re-opens the course's Edit form and flips the toggle (`UpdateToolboxTalkCourseCommandHandler.cs:53`, `course.GenerateCertificate = dto.GenerateCertificate;` — a correct, independent write path that works fine once invoked).

**Local DB check (`rascor_stock`, Development):** `SELECT * FROM toolbox_talks."ToolboxTalkCourses"` currently returns **zero rows**, and `ToolboxTalkCourseAssignments` also has **zero rows**. This local database has never had a course created or assigned, so it cannot corroborate or refute whether the reported test talk was course-scoped — that determination can only be made by inspecting the actual environment where the test was run (see §7 caveats).

---

## 5. Recent git history relevant to the trigger

`git log --oneline --follow -- .../CertificateGenerationService.cs`:
```
fad1b69 feat: add learning code to certificates, filter employee roles, rename Training to Learning
bc8f02a Add tenant settings system (email team name, certificate prefixes)
279ae22 feat: renamed Toolbox Talk(s) to Learning(s) for end user views
9e575bf feat: QuantumBuild LMS - standalone LMS extracted from Rascor (Core + ToolboxTalks)
```

`git log --oneline --follow -- .../CompleteToolboxTalkCommandHandler.cs`:
```
80bad46 fix: certificate generation reliability improvements
db61575 refactor: Change Employee.UserId from string? to Guid? to match User.Id
279ae22 feat: renamed Toolbox Talk(s) to Learning(s) for end user views
9e575bf feat: QuantumBuild LMS - standalone LMS extracted from Rascor (Core + ToolboxTalks)
```

**Neither file has been touched since `80bad46` (2026-03-31).** No refactor has silently dropped the certificate-generation call since that reliability fix landed — the standalone-talk wiring reviewed in §1-3 is exactly the code that shipped in that commit, unchanged for ~3.5 months of subsequent history. This rules out "a recent refactor removed the call" as an explanation.

`git show 80bad46` (full commit message, confirmed) — this is the commit that *introduced* the entire failure-tracking mechanism being relied on today:
> "Root cause investigation: standalone talk completions where `GenerateTalkCertificateAsync` returned null were silently accepted with no logging, no user feedback, and no recovery path. R2 upload failures also silently dropped certificates."

Its changes: added the `else` branch + `CertificateGenerationFailed` flag (§3), added the `regenerate-certificate` admin endpoint (§2), fixed `content.Length` being read after stream consumption in `R2StorageService` uploads, flipped `ToolboxTalk.GenerateCertificate` default `false → true`, and set `GenerateCertificate = true` on the 3 talks in `TestTenantSeeder` (an **xUnit integration-test fixture** at `tests/QuantumBuild.Tests.Common/TestTenant/TestTenantSeeder.cs` — not a Development/Demo environment seeder; it does not touch the local `rascor_stock` database used for manual testing).

**The wizard cutover (Note 29)** is directly implicated only for the course path, per §4. For standalone talks specifically, both new-wizard branches (`ContentCreationSessionService.cs:1449` and `:1726`) and the legacy wizard (direct `POST /toolbox-talks`, `UpdateToolboxTalkCommandHandler.cs`) all read/write `GenerateCertificate` through explicit, correct fields — no wizard-version-dependent gap was found for standalone talks.

---

## 6. Configuration flag inventory

| Flag / prerequisite | Location | Required for talk certs? | Required for course certs? | Failure mode if unmet |
|---|---|---|---|---|
| `ToolboxTalk.GenerateCertificate` | `ToolboxTalk.cs:188`, default `true` | **Yes** — sole gate (`CertificateGenerationService.cs:46`) | N/A (ignored when `CourseAssignmentId` set) | Graceful `null`, Debug log only |
| `ToolboxTalkCourse.GenerateCertificate` | `ToolboxTalkCourse.cs:21`, default `false` | N/A | **Yes** — sole gate (`CertificateGenerationService.cs:131`) | Graceful `null`, Debug log only |
| `ScheduledTalk.CourseAssignmentId` (implicit routing flag, not a config toggle) | `ScheduledTalk` entity | Determines *which* of the two flags above applies | — | If set, talk-level flag is irrelevant even if `true` |
| Tenant setting `TalkCertificatePrefix` / `CourseCertificatePrefix` | `TenantSettingsService.GetSettingAsync`, `TenantSettingsService.cs:9-16` | No — has a hard-coded default via `TenantSettingKeys.Defaults.*`, and `GetSettingAsync` returns the default gracefully via `setting?.Value ?? defaultValue` (`:15`) if the row is missing | Same | None — verified missing-row-safe. Local DB confirms `public."TenantSettings"` has **zero rows** and the system still functions via defaults |
| R2 storage configuration (`R2StorageSettings`) | `R2StorageService.cs:30-49` | Indirectly — a broken R2 client would make step 6 in §1 fail, surfacing as `CertificateGenerationFailed = true` (not a silent skip) | Same | Visible via `CertificateGenerationFailed = true` + Error log, **not** silent |
| Signature / PDF template requirements | None found — `SignatureDataUrl` is nullable throughout (`CertificateGenerationService.cs:90`, `312-332` renders blank space if absent or fails to decode) | No | No | N/A — signature is optional for cert generation |

**Given the test scenario as described (operator completes an assigned talk, `GenerateCertificate = true` confirmed on the talk):** all prerequisites for the **talk-level** path in §6 would be satisfied *unless* that `ScheduledTalk` actually has `CourseAssignmentId` set (i.e., the talk was assigned as part of a course, even if it displays to the employee like an individual learning item) — in which case the talk's own `true` flag is silently irrelevant and the course's flag (default `false`, and confirmed broken-by-omission pre-`2cc0f42` for any course published via the new wizard before 2026-07-15) is what actually governs. This is the single largest scenario disambiguator that could not be settled from the code alone — see §7.

---

## 7. Diagnosis — ranked candidates

### 1. (Most likely, if the tested talk was course-assigned) — Course-level `GenerateCertificate` silently defaulted `false` for any course published via the new wizard before 2026-07-15, and remains so with no backfill

- **Evidence:** `ToolboxTalkCourse.GenerateCertificate` defaults `false` (`ToolboxTalkCourse.cs:21`); `PublishAsCourseAsync` never read the wizard's setting before commit `2cc0f42` (confirmed via `git show 2cc0f42` diff, §4); the fix has no accompanying data migration/backfill (confirmed — the commit only touches `ContentCreationSessionService.cs` and a test file, no migration in the diff stat); `CertificateGenerationService.cs:27-33` explicitly and correctly refuses to generate a talk-level certificate for any course-scoped `ScheduledTalk`, deferring entirely to the course flag (`:131-135`).
- **Why plausible for "zero certificates ever":** if most or all of the tenant's assigned learning content is delivered via courses (the docs' Course Workflow section suggests this is a first-class, heavily-used pattern) rather than standalone talk assignment, then *every* course published before yesterday would silently never issue a certificate no matter how many member talks the employee completes, and the talk's own `GenerateCertificate = true` — which is what gets shown/checked when inspecting "the talk" — is a red herring, because it is never consulted for a course-scoped completion.
- **Confidence caveat:** this cannot be confirmed or refuted from the local `rascor_stock` database, which currently has zero `ToolboxTalkCourses` and zero `ToolboxTalkCourseAssignments` rows (queried directly, §4) — the reported test almost certainly happened against a different environment (Railway Development, per the branch/deploy notes in CLAUDE.md). **The single fastest way to confirm or rule this out is to check whether the specific completed `ScheduledTalk.CourseAssignmentId` is non-null, and if so, check that course's `GenerateCertificate` value.**

### 2. Exception thrown inside `GenerateTalkCertificateAsync` is swallowed with no failure marker

- **Evidence:** `CompleteToolboxTalkCommandHandler.cs:236-239` — the outer `catch (Exception ex)` around the certificate call only logs; it never sets `CertificateGenerationFailed = true`, unlike the explicit `else` branch for a clean `null` return (`:222-234`). This means a *thrown* exception (as opposed to a business-rule `null`) inside `GenerateTalkCertificateAsync` produces a `ScheduledTalkCompletion` row that is indistinguishable from "the trigger was never called at all" — no `CertificateUrl`, no `CertificateGenerationFailed`, only a server-side log line.
- **Why plausible:** matches "zero certificates ever, and no failure flag either" if there is *some* environment-specific exception source (a DB constraint, a missing/misconfigured R2 credential path not caught by `R2StorageService`'s own try/catch, a PDF-rendering exception from unusual data). Weaker than candidate 1 because `R2StorageService.UploadCertificateAsync` (the most likely external-dependency failure point) already catches its own exceptions and returns a `null`-producing failure result rather than throwing (`R2StorageService.cs:219-256`), so this candidate requires a *less obvious* throw site (e.g. `GenerateCertificateNumber`'s DB query, or `tenantSettingsService.GetSettingAsync`, both verified not to throw on missing data in §6) — no concrete throw site was identified during this recon, only the structural gap that such a throw would go unmarked.

### 3. Refresher-scheduling exception, thrown before the certificate try/catch is even reached (standalone talks with `RequiresRefresher = true` only)

- **Evidence:** `CompleteToolboxTalkCommandHandler.cs:207` calls `ScheduleRefresherIfRequired` with no surrounding try/catch, ahead of the certificate try/catch at `:210`. Inside, `RefresherSchedulingService.cs:45-60` (creating + saving the refresher `ScheduledTalk`) has no try/catch of its own — only the email send later in the same method is guarded (`:79-89`).
- **Why plausible:** would produce exactly the observed shape (completion row exists and is Completed, no certificate, no failure flag) plus a visible 500 on the Complete request. Narrower than candidates 1-2 because it requires `talk.RequiresRefresher == true`, which is not confirmed either way for the test scenario.

### 4. Trigger never wired / DI misconfiguration — **ruled out**

`ICertificateGenerationService` is correctly registered (`DependencyInjection.cs:39`), correctly constructor-injected into both `CompleteToolboxTalkCommandHandler` and `CourseProgressService`, and both call sites were confirmed present and reachable by direct code read (§1-3). This is not a Hangfire-interface-vs-concrete-type situation (Note 21) — the service is never enqueued as a job; it is a normal scoped DI service called synchronously within the same request. No evidence supports "never wired."

### Unresolved

If the tested talk is confirmed **not** to be course-scoped (`CourseAssignmentId` is null) and `RequiresRefresher` is confirmed `false`, then based on this recon alone the standalone-talk code path is correctly wired, correctly gated, and should succeed — in which case the actual cause is most likely candidate 2 (a swallowed exception with an as-yet-unidentified throw site) and would require server logs from the actual failing environment (not available to this recon) to pinpoint. This recon could not fully rule in or out candidate 2 without such logs, and is flagging it explicitly rather than forcing a tidier conclusion.

---

## 8. Recommended fix scope (description only, no code written)

Two independent, small-to-medium fixes, addressable separately:

1. **Course backfill (if candidate 1 is confirmed):** a one-time data migration/script to set `GenerateCertificate` correctly on any pre-existing `ToolboxTalkCourse` rows published via the new wizard before `2cc0f42` landed, using each course's original `ContentCreationSession.SettingsJson` as the source of truth where it still exists (or defaulting to an admin-reviewed value where the session has expired/been purged). Small, one-off, no ongoing code change beyond the migration script itself — `2cc0f42` already fixed the forward-going behaviour.
2. **Close the exception-swallow gap (candidate 2):** in `CompleteToolboxTalkCommandHandler.cs:236-239` (and the equivalent in `CourseProgressService.cs:92-96`), the catch block should also set `completion.CertificateGenerationFailed = true` (and persist it) on the exception path, not just on the clean-`null` path — so a thrown exception becomes visible and retryable via the existing `RegenerateCertificate` admin endpoint instead of silently indistinguishable from "never attempted." Small, localized change to two catch blocks plus their tests.
3. **Optional hardening (candidate 3):** wrap the non-email portion of `RefresherSchedulingService.ScheduleRefresherIfRequired` (both overloads) in its own try/catch, consistent with the existing pattern already used for the email-send portion in the same methods, so a refresher-scheduling failure can't abort the whole completion request after the completion row has already been committed. Small, localized, mirrors an existing pattern in the same file.

None of these require touching the core trigger wiring itself (§1-3), which this recon found to be intact for standalone talks.
