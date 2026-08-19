# Validation Pipeline Recon

Read-only investigation into whether FluentValidation is actually enforced anywhere in the
.NET backend, a full inventory of every validator that exists, and — for the ones judged
risky — whether current real traffic (frontend + internal callers) would violate their
rules if a MediatR `ValidationBehavior` were added.

**No code was changed to produce this document.** Every claim below is backed by a
`file:line` citation. Where something could not be verified it is marked `unverified` /
`not found` rather than guessed.

---

## A. Confirm the gap and how validation is (not) wired

### A.1 — FluentValidation DI registration

Validators are registered from two assemblies only, both via `AddValidatorsFromAssembly`:

- `src\Core\QuantumBuild.Core.Application\DependencyInjection.cs:29`
  ```csharp
  services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
  ```
- `src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Application\DependencyInjection.cs:27`
  ```csharp
  services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
  ```

This registers every `AbstractValidator<T>` in the Core Application and ToolboxTalks
Application assemblies as `IValidator<T>` in DI — so `IValidator<T>` **can** be injected
anywhere. It says nothing about whether anything calls `.ValidateAsync()` on it.

The `LessonParser.Application` assembly's two validators (`SubmitTextRequestValidator`,
`SubmitUrlRequestValidator`) are **not** registered via `AddValidatorsFromAssembly` at all —
no `services.AddValidatorsFromAssembly(...)` call exists anywhere under
`src\Modules\LessonParser\` (confirmed by grep — only the `.csproj` package reference and
the two validator files themselves reference `FluentValidation`). They are only ever
instantiated manually with `new` (see A.4).

### A.2 — MediatR pipeline behavior registration

Only one `AddMediatR` call exists in the entire solution:

- `src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Application\DependencyInjection.cs:24`
  ```csharp
  services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
  ```

Grepping the entire `src\` tree for `IPipelineBehavior`, `AddOpenBehavior`, and
`ValidationBehavior`/`LoggingBehavior`/`*Behavior` (as a class-name pattern) returns **only
that one line** — the `AddMediatR` call itself, with no `.AddOpenBehavior(...)` chained and
no `IPipelineBehavior<,>` implementation anywhere in the codebase. **Confirmed absent**:
there is no logging behavior, no exception behavior, no validation behavior — no pipeline
behaviors of any kind are registered. `IRequestHandler<,>` classes run with nothing wrapped
around them except MediatR's own dispatch.

Core Application module (`src\Core\QuantumBuild.Core.Application\DependencyInjection.cs`)
does not call `AddMediatR` at all — its `Features/*Service` classes (Sites, Employees, Users,
etc.) are plain injectable services called directly from controllers, not MediatR handlers.
Only the ToolboxTalks and (partially) LessonParser modules use MediatR/CQRS.

### A.3 — ASP.NET Core auto-validation wiring

Grep for `AddFluentValidationAutoValidation`, `ApiBehaviorOptions`,
`InvalidModelStateResponseFactory`, `SuppressModelStateInvalidFilter` across all of `src\`:
**no matches**. `Program.cs:177` calls plain `builder.Services.AddControllers()` (with only
JSON camelCase/enum-string options chained) — no FluentValidation ASP.NET integration package
is used, and `ModelState`-based auto-validation is not customized to look at
FluentValidation results. Confirmed absent.

### A.4 — Manual `validator.ValidateAsync(...)` call sites

Grep for `.Validate(` / `.ValidateAsync(` in files that also reference `IValidator` (or a
custom validation service) found 7 files. Splitting them:

**Real FluentValidation `IValidator<T>` manual calls (these validators DO run today):**

| File:line | Validator invoked | How |
|---|---|---|
| `src\QuantumBuild.API\Controllers\TenantReviewerConfigurationsController.cs:40` | `IValidator<CreateTenantReviewerConfigurationRequest>` | Injected via constructor (`TenantReviewerConfigurationsController.cs:19`), called manually in the `Create` action before calling the service |
| `src\QuantumBuild.API\Controllers\TenantReviewerConfigurationsController.cs:71` | `IValidator<UpdateTenantReviewerConfigurationRequest>` | Same pattern, `Update` action |
| `src\QuantumBuild.API\Controllers\TenantSectorsController.cs:54` | `IValidator<AssignTenantSectorRequest>` | Injected (`TenantSectorsController.cs:15`), called manually in `AssignSector` |
| `src\QuantumBuild.API\Controllers\LessonParserController.cs:225` | `SubmitUrlRequestValidator` | **`new SubmitUrlRequestValidator()`** — instantiated directly, bypassing DI entirely (line 224) |
| `src\QuantumBuild.API\Controllers\LessonParserController.cs:266` | `SubmitTextRequestValidator` | Same pattern, `new SubmitTextRequestValidator()` (line 265) |
| `src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Application\Commands\UpdateToolboxTalkTenantDefaults\UpdateToolboxTalkTenantDefaultsCommandHandler.cs:32` | `IValidator<UpdateToolboxTalkTenantDefaultsCommand>` | Injected into the MediatR handler itself (constructor at line 22), called as the first line of `Handle()` |
| `src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Application\Commands\UpdateToolboxTalkNotificationSettings\UpdateToolboxTalkNotificationSettingsCommandHandler.cs:32` | `IValidator<UpdateToolboxTalkNotificationSettingsCommand>` | Same pattern |

These 7 rule-sets (5 distinct validator classes, since Tenant Reviewer has Create+Update) are
**out of scope** for "never enforced" — they already run on every request today, entirely
independently of any pipeline wiring, via hand-rolled calls at the top of the
controller/handler method.

**Not FluentValidation (custom domain validation services, different concept entirely):**

| File:line | What it is |
|---|---|
| `src\QuantumBuild.API\Controllers\BulkSopImportController.cs:130` | `IBulkSopImportValidationService.ValidateAsync(zipStream, ct)` — validates a ZIP file's structure, unrelated to `AbstractValidator<T>` |
| `src\QuantumBuild.API\Controllers\BulkEmployeeImportController.cs:152` | `IBulkEmployeeImportValidationService.ValidateAsync(csvStream, ct)` — validates a CSV file's structure, same story |

### A.5 — What a MediatR `ValidationBehavior<TRequest,TResponse>` would look like

The standard pattern (not present in this codebase) is an `IPipelineBehavior<TRequest,
TResponse>` registered via `.AddOpenBehavior(typeof(ValidationBehavior<,>))` that runs before
every handler: it resolves `IEnumerable<IValidator<TRequest>>` for the incoming request type,
runs all of them, aggregates failures, and either throws `FluentValidation.ValidationException`
or short-circuits the pipeline with a failure result — before `Handle()` on the actual command
handler ever executes. Because MediatR wraps every `IMediator.Send(...)` call, adding this
behavior would retroactively activate **every validator registered in DI** (i.e. every
`AbstractValidator<T>` found in section B) for **every command/request MediatR dispatches**,
all at once, with no per-command opt-in — which is exactly why the blast radius in section C
matters before flipping this on.

---

## B. Inventory of validators (31 found)

Found via `grep -r ": AbstractValidator<"` across `src\`. Manually-enforced ones (see A.4)
are marked accordingly; everything else has genuinely never run.

| # | File | Validates | Enforced today? |
|---|---|---|---|
| 1 | `Commands\CreateToolboxTalkSchedule\CreateToolboxTalkScheduleCommandValidator.cs` | `CreateToolboxTalkScheduleCommand` | Never |
| 2 | `Commands\UpdateToolboxTalkSchedule\UpdateToolboxTalkScheduleCommandValidator.cs` | `UpdateToolboxTalkScheduleCommand` | Never |
| 3 | `Core.Application\Features\Sites\CreateSiteValidator.cs` | `CreateSiteDto` | Never |
| 4 | `Core.Application\Features\Sites\UpdateSiteValidator.cs` | `UpdateSiteDto` | Never |
| 5 | `Core.Application\Features\Departments\CreateDepartmentValidator.cs` | `CreateDepartmentDto` | Never |
| 6 | `Core.Application\Features\Departments\UpdateDepartmentValidator.cs` | `UpdateDepartmentDto` | Never |
| 7 | `DTOs\Reviewers\CreateTenantReviewerConfigurationRequestValidator.cs` | `CreateTenantReviewerConfigurationRequest` | **Manual** (A.4) |
| 8 | `DTOs\Reviewers\UpdateTenantReviewerConfigurationRequestValidator.cs` | `UpdateTenantReviewerConfigurationRequest` | **Manual** (A.4) |
| 9 | `Commands\UpdateToolboxTalkTenantDefaults\UpdateToolboxTalkTenantDefaultsCommandValidator.cs` | `UpdateToolboxTalkTenantDefaultsCommand` | **Manual** (A.4) |
| 10 | `Commands\UpdateToolboxTalkSettings\UpdateToolboxTalkSettingsCommandValidator.cs` | `UpdateToolboxTalkSettingsCommand` | Never (handler has no `IValidator` injected — confirmed by grep on the handler file) |
| 11 | `Commands\UpdateToolboxTalkNotificationSettings\UpdateToolboxTalkNotificationSettingsCommandValidator.cs` | `UpdateToolboxTalkNotificationSettingsCommand` | **Manual** (A.4) |
| 12 | `Commands\StartTalkTranslation\StartTalkTranslationCommandValidator.cs` | `StartTalkTranslationCommand` | Never (handler has no `IValidator` injected) |
| 13 | `Commands\InitialiseToolboxTalk\InitialiseToolboxTalkCommandValidator.cs` | `InitialiseToolboxTalkCommand` | Never |
| 14 | `Core.Application\Features\Users\CreateUserValidator.cs` | `CreateUserDto` | Never |
| 15 | `DTOs\GenerateInspectionReportRequestValidator.cs` | `GenerateInspectionReportRequest` | Never (`RequirementMappingController.cs:251` binds it via `[FromBody]` with no validator call) |
| 16 | `DTOs\Sectors\AssignTenantSectorRequestValidator.cs` | `AssignTenantSectorRequest` | **Manual** (A.4) |
| 17 | `LessonParser.Application\Validators\SubmitTextRequestValidator.cs` | `SubmitTextRequest` | **Manual, but via `new`, bypassing DI** (A.4) |
| 18 | `LessonParser.Application\Validators\SubmitUrlRequestValidator.cs` | `SubmitUrlRequest` | **Manual, but via `new`, bypassing DI** (A.4) |
| 19 | `Commands\UpdateToolboxTalk\UpdateToolboxTalkCommandValidator.cs` | `UpdateToolboxTalkCommand` | Never |
| 20 | `Commands\CreateToolboxTalk\CreateToolboxTalkCommandValidator.cs` | `CreateToolboxTalkCommand` | Never |
| 21 | `Commands\GenerateContentTranslations\GenerateContentTranslationsCommandValidator.cs` | `GenerateContentTranslationsCommand` | Never |
| 22 | `Commands\CompleteToolboxTalk\CompleteToolboxTalkCommandValidator.cs` | `CompleteToolboxTalkCommand` | Never |
| 23 | `Commands\UpdateVideoProgress\UpdateVideoProgressCommandValidator.cs` | `UpdateVideoProgressCommand` | Never |
| 24 | `Commands\SubmitQuizAnswers\SubmitQuizAnswersCommandValidator.cs` | `SubmitQuizAnswersCommand` | Never |
| 25 | `Commands\MarkSectionRead\MarkSectionReadCommandValidator.cs` | `MarkSectionReadCommand` | Never |
| 26 | `Commands\DeleteToolboxTalk\DeleteToolboxTalkCommandValidator.cs` | `DeleteToolboxTalkCommand` | Never |
| 27 | `Core.Application\Features\Users\UpdateUserValidator.cs` | `UpdateUserDto` | Never |
| 28 | `Core.Application\Features\Users\ResetPasswordValidator.cs` | `ResetPasswordDto` | Never |
| 29 | `Core.Application\Features\Users\ChangePasswordValidator.cs` | `ChangePasswordDto` | Never |
| 30 | `Core.Application\Features\Employees\UpdateEmployeeValidator.cs` | `UpdateEmployeeDto` | Never |
| 31 | `Core.Application\Features\Employees\CreateEmployeeValidator.cs` | `CreateEmployeeDto` | Never |

All paths above are relative to `src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Application\`
unless prefixed `Core.Application\` (→ `src\Core\QuantumBuild.Core.Application\`) or
`LessonParser.Application\` (→ `src\Modules\LessonParser\QuantumBuild.Modules.LessonParser.Application\`).

### Rule-by-rule detail (never-enforced validators only; the 7 manually-enforced rule sets are excluded since they already run and are not part of the "turning on validation" risk)

**#1 `CreateToolboxTalkScheduleCommandValidator`** (file `Commands\CreateToolboxTalkSchedule\CreateToolboxTalkScheduleCommandValidator.cs`)
- `:10` TenantId NotEmpty — LIKELY SAFE
- `:14` ToolboxTalkId NotEmpty — LIKELY SAFE
- `:18-22` ScheduledDate NotEmpty + `date.Date >= DateTime.UtcNow.Date` — see C
- `:24-26` Frequency IsInEnum — LIKELY SAFE
- `:29-32` EndDate > ScheduledDate when EndDate has value — LIKELY SAFE
- `:38-43` `AssignToAllEmployees || EmployeeIds.Any() || TargetDepartmentIds.Any() || TargetSiteIds.Any()` — see C
- `:46-49` EmployeeIds must be empty when AssignToAllEmployees — see C (the "known risk" rule)
- `:51-54` TargetDepartmentIds must be empty when AssignToAllEmployees — see C
- `:56-59` TargetSiteIds must be empty when AssignToAllEmployees — see C
- `:62-65` each EmployeeId NotEmpty when not AssignToAllEmployees — LIKELY SAFE
- `:67-70` each TargetDepartmentId NotEmpty when not AssignToAllEmployees — LIKELY SAFE
- `:72-75` each TargetSiteId NotEmpty when not AssignToAllEmployees — LIKELY SAFE
- `:78-81` Notes MaxLength(1000) — LIKELY SAFE (frontend caps at 500, see C)

**#2 `UpdateToolboxTalkScheduleCommandValidator`** (file `Commands\UpdateToolboxTalkSchedule\UpdateToolboxTalkScheduleCommandValidator.cs`) — identical rule shape to #1 plus:
- `:10-12` TenantId NotEmpty, `:14-16` Id NotEmpty — LIKELY SAFE
- `:18-22` ScheduledDate must be `>= today` — **POTENTIALLY BREAKING, confirmed VIOLATES on edit of any existing schedule whose original date has passed** (see C)
- Remaining rules mirror #1 (AssignToAllEmployees/EmployeeIds/TargetDepartmentIds/TargetSiteIds pattern) — see C

**#3/#4 `CreateSiteValidator` / `UpdateSiteValidator`** — SiteCode/SiteName/Address/City/PostalCode/Phone/Email/Notes MaxLength + SiteName NotEmpty + Email format when present. All LIKELY SAFE — these are DB column-width-matching length caps and a required-name check; the admin site form is the only caller (`web\src\components\admin\site-form.tsx` — not independently re-verified line-by-line, but the fields are plain always-populated form inputs, not conditional/cross-field rules).

**#5/#6 `CreateDepartmentValidator` / `UpdateDepartmentValidator`** — Name NotEmpty+MaxLength(100), Code MaxLength(20) when present. LIKELY SAFE.

**#10 `UpdateToolboxTalkSettingsCommandValidator`** (`Commands\UpdateToolboxTalkSettings\UpdateToolboxTalkSettingsCommandValidator.cs`)
- `:11-12` Title NotEmpty+MaxLength(200) — LIKELY SAFE
- `:14-16` Description MaxLength(2000) — LIKELY SAFE
- `:18-20` MinimumVideoWatchPercent InclusiveBetween(50,100) — LIKELY SAFE (matches documented default 90%, UI is presumably a bounded slider — not independently re-verified)
- `:22-24` AutoAssignDueDays InclusiveBetween(1,90) — LIKELY SAFE
- `:26-27` RefresherFrequency IsInEnum — LIKELY SAFE

**#12 `StartTalkTranslationCommandValidator`** — TalkId/TenantId NotEmpty, LanguageCode NotEmpty+MaxLength(10). LIKELY SAFE.

**#13 `InitialiseToolboxTalkCommandValidator`** (`Commands\InitialiseToolboxTalk\InitialiseToolboxTalkCommandValidator.cs`) — 13 rules, see D for a gap (Docx input mode has no required-field rule at all). Individually:
- `:13-15` TenantId NotEmpty — LIKELY SAFE
- `:17-21` Title NotEmpty+MaxLength(200) — LIKELY SAFE
- `:23-26` Code MaxLength(20) when present — LIKELY SAFE
- `:28-31` Description MaxLength(2000) when present — LIKELY SAFE
- `:34-37` SourceText NotEmpty when `InputMode==Text && SourceFileUrl empty` — LIKELY SAFE (mirrors what the wizard collects for text mode)
- `:40-43` SourceFileUrl NotEmpty when `InputMode==Pdf` — LIKELY SAFE
- `:46-49` VideoUrl or SourceFileUrl required when `InputMode==Video` — LIKELY SAFE
- `:51-53` AudienceRole must be one of Operator/Supervisor/Auditor — confirmed matches frontend `AUDIENCE_ROLES` exactly (`web\src\features\toolbox-talks\components\learning-wizard\schemas\inputConfigSchema.ts:6`) — LIKELY SAFE, not stale (see D for why this looked suspicious but isn't)
- `:55-77` ReviewerName/ReviewerOrg/ReviewerRole/DocumentRef/ClientName/AuditPurpose MaxLength when present — LIKELY SAFE

**#14 `CreateUserValidator`** — Email NotEmpty+EmailAddress+MaxLength(256), FirstName/LastName NotEmpty+MaxLength(100), RoleIds NotNull. LIKELY SAFE — `web\src\components\admin\user-form.tsx:45` already requires `roleIds.min(1)` client-side (stricter than the backend's mere NotNull), and all other fields are plain required text inputs.

**#15 `GenerateInspectionReportRequestValidator`** — ResponsiblePersonName/ResponsiblePersonRole NotEmpty+MaxLength(200), AuditPurpose MaxLength(500) when present. LIKELY SAFE (not independently traced to a frontend form field-by-field, but shape is a simple required-name/role pair).

**#16... already covered as manual.**

**#19/#20 `UpdateToolboxTalkCommandValidator` / `CreateToolboxTalkCommandValidator`** — near-identical ~20-rule sets (Id/TenantId NotEmpty, Title NotEmpty+MaxLength(200), VideoSource/Frequency IsInEnum, MinimumVideoWatchPercent 50-100, PassingScore required+50-100 when RequiresQuiz, **Sections NotEmpty when no VideoUrl**, **Questions NotEmpty when RequiresQuiz**, per-section Title/Content/SectionNumber rules, per-question Text/Type/CorrectAnswer/Options/Points rules). See C — confirmed SATISFIES, frontend (`ToolboxTalkForm.tsx`) explicitly mirrors these two conditional rules with a code comment saying so.

**#21 `GenerateContentTranslationsCommandValidator`** — ToolboxTalkId/TenantId NotEmpty, TargetLanguages NotEmpty, each TargetLanguage NotEmpty. See C — confirmed SATISFIES for the one internal caller traced (`MissingTranslationsJob`).

**#22 `CompleteToolboxTalkCommandValidator`** — ScheduledTalkId NotEmpty, SignatureData NotEmpty, SignedByName NotEmpty+MaxLength(200). LIKELY SAFE — completion is gated behind a signature-capture UI step per the documented completion flow; a talk cannot reach the complete API call without a captured signature.

**#23 `UpdateVideoProgressCommandValidator`** — ScheduledTalkId NotEmpty, WatchPercent InclusiveBetween(0,100). LIKELY SAFE.

**#24 `SubmitQuizAnswersCommandValidator`** — ScheduledTalkId NotEmpty, Answers NotNull+NotEmpty, each answer key/value NotEmpty. LIKELY SAFE — the quiz UI requires an answer selection per question before submit is enabled (not independently re-verified against the quiz component).

**#25 `MarkSectionReadCommandValidator`** — ScheduledTalkId/SectionId NotEmpty, TimeSpentSeconds >= 0 when present. LIKELY SAFE.

**#26 `DeleteToolboxTalkCommandValidator`** — Id/TenantId NotEmpty. LIKELY SAFE.

**#27 `UpdateUserValidator`** — FirstName/LastName NotEmpty+MaxLength(100), RoleIds NotNull. LIKELY SAFE (same reasoning as #14).

**#28 `ResetPasswordValidator`** — NewPassword NotEmpty+MinLength(6)+MaxLength(100), ConfirmPassword NotEmpty+Equal(NewPassword). LIKELY SAFE if frontend enforces >=6 chars too — **not independently re-verified against the reset-password form's own schema**, flagged `unverified`.

**#29 `ChangePasswordValidator`** — CurrentPassword NotEmpty, NewPassword NotEmpty+MinLength(6)+MaxLength(100), ConfirmPassword NotEmpty+Equal(NewPassword). Same caveat as #28 — `unverified` against the frontend's own min-length.

**#30/#31 `UpdateEmployeeValidator` / `CreateEmployeeValidator`** — EmployeeCode NotEmpty+MaxLength(50), FirstName/LastName NotEmpty+MaxLength(100), Email MaxLength(200)+format when present, Phone/Mobile MaxLength(50) when present, JobTitle MaxLength(100) when present, **Department MaxLength(100) when present**, Notes MaxLength(2000) when present, EndDate > StartDate when both present. See D — `Department` here is the legacy free-text field; `DepartmentId` (the current canonical field per the in-progress Staff Department formalisation project) has no corresponding rule. LIKELY SAFE in the sense that no current traffic would be rejected (the legacy field is optional and length-capped generously), but see D for the completeness gap.

---

## C. Blast radius — what currently violates

This section traces the rules flagged above as needing scrutiny to their actual callers.

### C.1 — Schedule `AssignToAllEmployees` / `EmployeeIds` / `TargetDepartmentIds` / `TargetSiteIds` pattern — **SATISFIES**

This is the pattern flagged as a known risk. Both `CreateToolboxTalkScheduleCommandValidator`
and `UpdateToolboxTalkScheduleCommandValidator` require that when `AssignToAllEmployees ==
true`, `EmployeeIds`/`TargetDepartmentIds`/`TargetSiteIds` must all be empty
(`CreateToolboxTalkScheduleCommandValidator.cs:46-59`,
`UpdateToolboxTalkScheduleCommandValidator.cs:43-56`), and conversely that at least one of
the three must be non-empty when `AssignToAllEmployees == false`
(`CreateToolboxTalkScheduleCommandValidator.cs:38-43`,
`UpdateToolboxTalkScheduleCommandValidator.cs:35-40`).

The only caller of either command is `POST /api/toolbox-talks/schedules` and
`PUT /api/toolbox-talks/schedules/{id}` in
`src\QuantumBuild.API\Controllers\ToolboxTalkSchedulesController.cs:124` /
`:172` — both bind `[FromBody]` directly to the command record with no intermediate DTO.
Grep for `new CreateToolboxTalkScheduleCommand` / `new UpdateToolboxTalkScheduleCommand`
across `src\` found no other construction sites — no Hangfire job, seeder, or other handler
builds these commands.

The frontend caller is `web\src\features\toolbox-talks\components\ScheduleDialog.tsx`. Its
`onSubmit` (lines 210-235) builds the payload as:

```ts
// web\src\features\toolbox-talks\components\ScheduleDialog.tsx:230-233
assignToAllEmployees: values.assignToAllEmployees,
employeeIds: values.assignToAllEmployees ? undefined : values.employeeIds,
targetDepartmentIds: values.assignToAllEmployees ? undefined : values.targetDepartmentIds,
targetSiteIds: values.assignToAllEmployees ? undefined : values.targetSiteIds,
```

`undefined` fields are dropped by `JSON.stringify` (axios's default serializer), so when
`assignToAllEmployees` is `true` the three list fields are omitted from the request body
entirely, letting the C# record defaults (`= new()`, i.e. empty lists — see
`CreateToolboxTalkScheduleCommand.cs:45,51,57`) take over — which satisfies the "must be
empty" rule. When `assignToAllEmployees` is `false`, the frontend already client-side
enforces "at least one of employee/department/site" at lines 212-218 before the request is
even sent. **Conclusion: SATISFIES.** This specific rule pattern — despite being called out
as a known risk — is currently safe to enable, because this one and only caller was already
written to respect it (likely intentionally, given the validator and the frontend logic read
as if authored together).

### C.2 — Schedule `ScheduledDate` must be today-or-future — **CONFIRMED VIOLATES on Update**

Both validators carry:
```csharp
// CreateToolboxTalkScheduleCommandValidator.cs:18-22 and
// UpdateToolboxTalkScheduleCommandValidator.cs:18-22
RuleFor(x => x.ScheduledDate)
    .NotEmpty()
    .Must(date => date.Date >= DateTime.UtcNow.Date)
    .WithMessage("ScheduledDate must be today or in the future.");
```

On **Create**, this is fine — the date picker defaults to `new Date()` (today) and there is
no legitimate reason to create a new schedule dated in the past.

On **Update**, `ScheduleDialog.tsx` pre-populates the date picker with the schedule's
*existing* `ScheduledDate` when editing:

```ts
// web\src\features\toolbox-talks\components\ScheduleDialog.tsx:171 and :190 (form.reset)
scheduledDate: schedule ? new Date(schedule.scheduledDate) : new Date(),
```

and the submit payload sends that value back unchanged unless the user manually repicks a
date (`ScheduleDialog.tsx:227`, `scheduledDate: values.scheduledDate.toISOString()`). There
is no client-side minimum-date constraint on the zod schema either
(`scheduleFormSchema.scheduledDate: z.date({ message: 'Scheduled date is required' })` at
`ScheduleDialog.tsx:67` — no `.min()`).

Any recurring or one-time schedule whose `ScheduledDate` has already passed (which is the
normal state of an active recurring schedule some time after it started, or any schedule an
admin wants to edit — e.g. to add a department, change notes, or extend the end date —
without touching the original start date) would be **rejected** by
`UpdateToolboxTalkScheduleCommandValidator` the moment a `ValidationBehavior` is added,
purely because the unrelated `ScheduledDate` field carries a historical value. The only
caller of the Update command is this same dialog
(`ToolboxTalkSchedulesController.cs:172`, confirmed sole caller via the same grep as C.1).

**If ValidationBehavior is added today, `UpdateToolboxTalkScheduleCommand` would start being
rejected whenever an admin edits any schedule whose original `ScheduledDate` is in the past
and does not also repick the date, because `ScheduleDialog.tsx:171/190` submits the
unmodified historical date and `UpdateToolboxTalkScheduleCommandValidator.cs:18-22` rejects
any date before today.**

### C.3 — `CreateToolboxTalkCommand` / `UpdateToolboxTalkCommand` conditional Sections/Questions rules — **SATISFIES**

```csharp
// CreateToolboxTalkCommandValidator.cs:70-73 / UpdateToolboxTalkCommandValidator.cs:74-77
RuleFor(x => x.Sections).NotEmpty().When(x => string.IsNullOrEmpty(x.VideoUrl))
// CreateToolboxTalkCommandValidator.cs:76-79 / UpdateToolboxTalkCommandValidator.cs:80-83
RuleFor(x => x.Questions).NotEmpty().When(x => x.RequiresQuiz)
```

The only frontend caller of `POST /api/toolbox-talks` and `PUT /api/toolbox-talks/{id}` is
`web\src\features\toolbox-talks\components\ToolboxTalkForm.tsx`. Its own zod schema encodes
the identical condition, with a comment acknowledging it:

```ts
// web\src\features\toolbox-talks\components\ToolboxTalkForm.tsx:104-106
// Sections are required only if no video is provided (matches backend validation)
if (!hasVideo && data.sections.length === 0) { ... }
```

and quiz questions are separately guarded at submit time:
```ts
// ToolboxTalkForm.tsx:241-242
if (!values.questions || values.questions.length === 0) {
  form.setError('questions', { message: 'At least one question is required when quiz is enabled' });
```

Note the two AI-generation wizards (legacy `/admin/toolbox-talks/create` and the new
`/admin/toolbox-talks/learnings/**`) do **not** call `CreateToolboxTalkCommand` for their
initial draft — they call `POST /api/toolbox-talks/initialise`
(`InitialiseToolboxTalkCommand`, `ToolboxTalksController.cs:368`), which has no
Sections/Questions requirement at all (see B, validator #13). `CreateToolboxTalkCommand` is
only reached through the legacy manual-entry form. **Conclusion: SATISFIES**, for the one
caller that exists.

### C.4 — `GenerateContentTranslationsCommand.TargetLanguages` NotEmpty — **SATISFIES**

Two internal (non-frontend) callers construct this command:
`src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Infrastructure\Jobs\MissingTranslationsJob.cs`
and `ContentGenerationJob.cs`, plus the controller path from
`ToolboxTalksController.cs` (`POST /{id}/translations/generate`).

Traced `MissingTranslationsJob`: it only calls
`GenerateMissingContentTranslationsAsync(...)` (which builds and dispatches the command) from
inside an `else` branch guarded by `if (languagesNeedingTranslation.Count == 0) { ...log... }
else { ...call... }` (`MissingTranslationsJob.cs:188-203`) — the command is never dispatched
with an empty language list from this path. `ContentGenerationJob.cs` and the controller path
were not traced line-by-line to the same depth; flagged `unverified` for those two but no
evidence of a violation was found.

### C.5 — Remaining rules

No other rule in the inventory was found to have a plausible current-traffic violation.
Rules classified LIKELY SAFE in section B were spot-checked against their sole known
frontend caller where a caller was easy to identify (schedule, toolbox talk, employee, user
forms) and found to either already mirror the backend rule client-side, or to be a plain
required-field/length cap that any populated form field would satisfy. The password
validators (#28/#29) and a handful of DTO validators without an obviously-singular frontend
caller (Site, Department, GenerateInspectionReportRequest) were not traced field-by-field to
their form schemas — see the `unverified` notes inline in section B — a future chunk should
do a full field-by-field diff against each form's zod schema before flipping validation on
for those command types, even though nothing found here suggests they are currently at risk.

---

## D. Stale/wrong validators

### D.1 — `InitialiseToolboxTalkCommandValidator` has no rule for `InputMode.Docx`

The domain enum `InputMode` (`src\Modules\ToolboxTalks\QuantumBuild.Modules.ToolboxTalks.Domain\Enums\InputMode.cs:6-12`)
has four members: `Text = 1, Pdf = 2, Video = 3, Docx = 4`. The frontend wizard schema lists
all four (`web\src\features\toolbox-talks\components\learning-wizard\schemas\inputConfigSchema.ts:3`,
`INPUT_MODES = ['Text', 'Pdf', 'Video', 'Docx']`), meaning `Docx` is a live, selectable input
mode today. `InitialiseToolboxTalkCommandValidator.cs:34-49` has conditional required-field
rules for `Text`, `Pdf`, and `Video` modes but **none for `Docx`** — no rule requires
`SourceFileUrl` (or anything else) when `InputMode == Docx`. This isn't a "would break
current traffic" issue (a missing rule can only ever be more permissive, never reject valid
current traffic), but it is a gap: enabling validation would not catch a malformed/empty Docx
submission the way it catches the other three modes. Worth closing in the same pass that
enables validation, not urgent on its own.

### D.2 — `CreateEmployeeValidator` / `UpdateEmployeeValidator` validate the legacy `Department` field only, not `DepartmentId`

Per the in-progress Staff Department formalisation project (see user memory
`project_staff_department_formalisation.md`, chunk 1 complete), `Department` is now
documented as the legacy free-text field and `DepartmentId` (a `Guid?` FK) is the canonical
one:

```csharp
// src\Core\QuantumBuild.Core.Application\Features\Employees\DTOs\CreateEmployeeDto.cs:16
/// Legacy free-text department, kept during the transition to <see cref="DepartmentId"/>.
```
```csharp
// src\Core\QuantumBuild.Core.Application\Features\Employees\DTOs\UpdateEmployeeDto.cs:14
/// <see cref="DepartmentId"/>, so existing per-employee free text values are never
```

`CreateEmployeeValidator.cs:50-53` / `UpdateEmployeeValidator.cs:50-53` still only validate
`Department` (`MaximumLength(100)` when present) — there is no rule at all for `DepartmentId`
(e.g. no check that it's a non-empty GUID when supplied, though arguably that's
appropriately left to the service/FK layer rather than FluentValidation). This is not a
"wrong" validator and enabling it would not reject any current traffic, but it is an
incompleteness that reflects the validator not having been updated alongside the department
formalisation work. Low priority.

### D.3 — Two apparent mismatches investigated and found NOT stale (documented so they aren't re-flagged later)

- **`InitialiseToolboxTalkCommandValidator.cs:51-53`** — `AudienceRole` must be one of
  `Operator, Supervisor, Auditor`. This looks like it might be confusing "audience role" with
  the system `Role` entity (`SuperUser/Admin/Operator/Supervisor` per CLAUDE.md) — it isn't.
  It's a separate, intentional generation-tone enum matching
  `AUDIENCE_ROLES = ['Operator', 'Supervisor', 'Auditor']` in
  `web\src\features\toolbox-talks\components\learning-wizard\schemas\inputConfigSchema.ts:6`
  exactly. Not stale.
- **`UpdateToolboxTalkTenantDefaultsCommandValidator.cs:8`** — `ValidFrequencies =
  ["Once", "Monthly", "Quarterly", "Annually"]` looks like it might be out of sync with the
  documented `ToolboxTalkFrequency` enum (`Once, Weekly, Monthly, Annually` per CLAUDE.md).
  It isn't — `DefaultRefresherFrequency` is a plain `string` field
  (`UpdateToolboxTalkTenantDefaultsCommand.cs:17`) deliberately using a different,
  wizard-specific value set than the schedule-frequency enum, and it matches the frontend's
  own zod enum exactly:
  `web\src\features\toolbox-talks\components\settings\wizard-defaults-section.tsx:39`,
  `z.enum(['Once', 'Monthly', 'Quarterly', 'Annually'])`. Not stale. (This validator is also
  one of the 7 manually-enforced ones per A.4, so it is out of scope for the "never enforced"
  risk anyway.)

---

## E. Error surfacing

### E.1 — Current `Result<T>` / `BadRequest` pattern

`src\Core\QuantumBuild.Core.Application\Models\Result.cs:1-81` defines:
```csharp
public class Result
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public FailureCode? ErrorCode { get; set; }
    ...
```
serialized camelCase (`Program.cs:180`, `JsonNamingPolicy.CamelCase`) → JSON shape
`{ success, message, errors: string[], errorCode, data }`. Dozens of controllers return this
directly on failure, e.g. `src\QuantumBuild.API\Controllers\CompaniesController.cs:31,61,98`
and `ContactsController.cs:32,79,169,199,236`: `return BadRequest(result);`.

### E.2 — Frontend error extraction (`getApiErrorMessage`)

`web\src\lib\utils.ts:12-33`:
```ts
export function getApiErrorMessage(error: unknown, fallback = "An unexpected error occurred"): string {
  if (error && typeof error === "object" && "response" in error) {
    const axiosError = error as { response?: { data?: { errors?: string[]; message?: string } } };
    const apiErrors = axiosError.response?.data?.errors;
    const apiMessage = axiosError.response?.data?.message;
    if (apiErrors && apiErrors.length > 0) {
      return apiErrors.join(". ");     // <-- assumes string[]
    }
    if (apiMessage) {
      return apiMessage;
    }
  }
  if (error instanceof Error) { return error.message; }
  return fallback;
}
```
It prefers `response.data.errors` (typed and treated as `string[]`, joined with `". "`),
falling back to `response.data.message`, falling back to the JS `Error.message`.

### E.3 — What a `ValidationBehavior` failure would actually look like, and whether it's caught cleanly

Two realistic shapes for a future `ValidationBehavior`, and how each interacts with what
exists today:

**Shape A — throws `FluentValidation.ValidationException`** (the textbook MediatR pattern).
Three controllers already contain a `catch (FluentValidation.ValidationException ex)` block
that is currently dead code (nothing throws this exception today, since nothing invokes these
validators for the commands these controllers dispatch) — found at
`src\QuantumBuild.API\Controllers\ToolboxTalksController.cs:349,376,742`,
`ToolboxTalkSchedulesController.cs:149,189`, and `MyToolboxTalksController.cs:260,303,387`.
They all do the same thing, e.g.:
```csharp
// ToolboxTalkSchedulesController.cs:149-153
catch (FluentValidation.ValidationException ex)
{
    _logger.LogWarning("FluentValidation failed: {Message}", ex.Message);
    return BadRequest(new { message = ex.Message, errors = ex.Errors });
}
```
`ex.Errors` on a `FluentValidation.ValidationException` is `IEnumerable<ValidationFailure>` —
**objects** with `PropertyName`, `ErrorMessage`, `Severity`, etc. — not `string[]`. Once
serialized camelCase and read by `getApiErrorMessage`, `apiErrors` would be an array of
`{propertyName, errorMessage, ...}` objects, and `apiErrors.join(". ")` on an array of
objects produces the literal string `"[object Object]. [object Object]"` (or similar) — a
garbled, meaningless message shown to the user, not the actual validation text. **This is a
concrete defect waiting to activate**: these three controllers' existing dead catch blocks
would come alive the moment a `ValidationBehavior` throws `ValidationException` for any
command they dispatch, and would immediately start showing garbage error text instead of
"Title is required" etc.

For every **other** controller (i.e. anything without one of these `catch` blocks — the
majority, including `CoreApplication`-backed controllers with no MediatR involvement at all,
and any ToolboxTalks controller action not listed above), an uncaught
`FluentValidation.ValidationException` would propagate to the global exception handler:
```csharp
// Program.cs:351-374
app.UseExceptionHandler(errorApp => { errorApp.Run(async context => {
    ...
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new {
        type = "...", title = "An unexpected error occurred", status = 500,
        detail = "An internal error occurred. Please try again shortly."
    });
}); });
```
This returns **HTTP 500** with a fixed generic message that carries zero information about
which field failed or why. `getApiErrorMessage` would fall through to its `Error.message`
branch (typically axios's generic "Request failed with status code 500") — the user would see
a generic failure toast with no actionable detail, and a validation failure would look
indistinguishable from a real server crash.

**Shape B — returns `Result<T>.Fail(...)` instead of throwing.** If the behavior were written
to match the codebase's existing `Result<T>` convention (per project Note 18/25) rather than
throwing, and the controller then did `return BadRequest(result)` per the E.1 pattern, the
JSON shape (`errors: string[]`) **would** be correctly consumed by `getApiErrorMessage` as-is
— this is the only one of the two shapes that plugs cleanly into the existing frontend without
any frontend change.

**FailureCode note:** `src\Core\QuantumBuild.Core.Application\Models\FailureCode.cs:3-17`
currently defines `DuplicateEmail, WorkflowInvitationNotFound, WorkflowTokenInvalid,
WorkflowTokenAlreadyUsed, WorkflowTokenExpired, WorkflowInvalidState,
WorkflowSubmissionInvalid, WorkflowInitiationInvalid, WorkflowConfirmationRequired,
WorkflowReasonRequired, TitleNotUnique, Conflict` — there is **no** `ValidationFailed` (or
similar) member. Per project convention (Note 25, "callers must branch on `Result.ErrorCode`,
not error message text"), a `ValidationBehavior` built on the `Result<T>` shape (Shape B)
would need a new `FailureCode` member added for callers to branch on reliably; today nothing
in the enum represents "a FluentValidation rule failed."

---

## Summary — what to fix before enabling validation

Confirmed-real items from sections C and E only (not speculative "might be an issue" items —
those are called out inline above with `unverified`, not repeated here):

1. **`UpdateToolboxTalkScheduleCommandValidator.cs:18-22`** — `ScheduledDate must be today or
   in the future` rejects editing any existing schedule whose original date has passed,
   because `ScheduleDialog.tsx:171/190` resubmits the historical date unchanged when only
   unrelated fields (notes, department targeting, end date) are being edited. Either drop the
   date-in-past rule from the Update validator specifically (keep it on Create), or have the
   frontend stop resubmitting an unchanged historical `scheduledDate` on edit.

2. **Dead `catch (FluentValidation.ValidationException ex)` blocks in
   `ToolboxTalksController.cs:349,376,742`, `ToolboxTalkSchedulesController.cs:149,189`, and
   `MyToolboxTalksController.cs:260,303,387`** return `errors: ex.Errors` where `ex.Errors` is
   `ValidationFailure[]`, not `string[]` — `getApiErrorMessage`
   (`web\src\lib\utils.ts:19-21`) will render this as `"[object Object]. [object Object]"`
   instead of the actual validation messages. Needs a `.Select(e => e.ErrorMessage)`
   projection (or an equivalent fix) before/if a `ValidationBehavior` ever throws
   `ValidationException` through these three controllers.

3. **No `FailureCode` member represents a validation failure**
   (`src\Core\QuantumBuild.Core.Application\Models\FailureCode.cs`) — if the eventual
   `ValidationBehavior` is built on the `Result<T>` convention (recommended — see E.3 Shape B,
   the only shape that plugs into the existing frontend cleanly with zero frontend changes),
   a new `FailureCode` value should be added so callers can branch on it per Note 25's
   convention rather than parsing message text.

Everything else inventoried in section B was either already-enforced (7 rule sets, A.4), or
checked against its real caller(s) and found to already satisfy the rule (C.1, C.3, C.4), or
is a plain required-field/length-cap rule with no cross-field/conditional logic and no
evidence of being violated by any current flow (the bulk of section B). The two D-section
items (Docx input-mode gap, legacy-Department-only employee validation) are pre-existing
incompleteness, not regressions, and are optional cleanup rather than blockers.
