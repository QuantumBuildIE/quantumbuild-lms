# Certificate Number Race — Recon

**Status:** Read-only recon. No code changed, no certificates generated, no migrations run. Every claim below is anchored to a file:line citation against the current `transval` branch. Direct database inspection (row counts, tenant distribution) was **not available** in this environment — no `psql` binary on this machine — so §C.7 is answered from code/config only, flagged where that matters.

**Incident under investigation:** two certificate inserts, milliseconds apart, both attempted to write `CertificateNumber = 'LRN-2026-000001'`. The second insert was rejected by the unique index `ix_toolbox_talk_certificates_number`, so a real employee's completed training produced no certificate.

---

## A. How the number is generated (the race mechanism)

### A.1 — Exact generation code

`CertificateGenerationService.cs:203-211`:

```csharp
private async Task<string> GenerateCertificateNumber(string prefix, Guid tenantId, CancellationToken ct)
{
    var year = DateTime.UtcNow.Year;
    var pattern = $"{prefix}-{year}-";
    var count = await context.ToolboxTalkCertificates
        .IgnoreQueryFilters()
        .CountAsync(c => c.TenantId == tenantId && c.CertificateNumber.StartsWith(pattern), ct);
    return $"{prefix}-{year}-{(count + 1):D6}";
}
```

This is **count-then-increment, computed entirely in application code, from a plain `SELECT COUNT(*)` with no locking, no transaction isolation bump, and no database sequence.** It is called at `CertificateGenerationService.cs:68` (talk certificates) and `:158` (course certificates) — the only two call sites in the codebase (confirmed by grep for `GenerateCertificateNumber`).

There is a time gap between this read (`CountAsync`) and the eventual write (`context.ToolboxTalkCertificates.Add(certificate)` + `SaveChangesAsync` at `CertificateGenerationService.cs:105-106` / `:195-196`) — in between, the code builds the PDF in memory (`GenerateCertificatePdf`, `:94`/`:184`) and uploads it to R2 (`UploadCertificatePdf`, `:95`/`:185`, which itself makes a network call to Cloudflare R2 — see `R2StorageService.cs:219-256`). That is tens to hundreds of milliseconds of wall-clock time during which the `count` value is stale for any other concurrent caller. **Any two calls to `GenerateCertificateNumber` that overlap in that window and land on the same `(prefix, year, tenantId)` triple will compute an identical `count` and therefore an identical `CertificateNumber` string.** Nothing in the code path prevents or detects this before the `INSERT` is attempted — it is discovered only by the database's unique index rejecting the second `SaveChangesAsync`, which throws a `DbUpdateException` wrapping a unique-violation. That exception is caught by the calling code as a generic exception (§B.4 below) — there is no code that recognises "this specific exception is a certificate-number collision" and retries with a fresh number.

**Verdict on the mechanism:** classic max-plus-one / count-plus-one read-then-increment, computed in application code (not a DB sequence, not a GUID). Concurrent callers absolutely can and do compute the same number — there is no serialisation of any kind around this read+write.

### A.2 — Format structure and scope

- `LRN` — the talk-certificate prefix. It is a **tenant setting**, not hardcoded: `TenantSettingKeys.TalkCertificatePrefix` (`TenantSettingKeys.cs:8`), default value `"LRN"` (`TenantSettingKeys.cs:19`, referenced as `TenantSettingKeys.Defaults.TalkCertificatePrefix`). Course certificates use a separate setting, `CourseCertificatePrefix`, default `"TBC"` (`TenantSettingKeys.cs:9,20`). The prefix is read per-request via `tenantSettingsService.GetSettingAsync(tenantId, ...)` at `CertificateGenerationService.cs:66-67` (talk) / `:156-157` (course) and falls back to the hardcoded default if the tenant has no override row (`TenantSettingsService.cs:9-16`, `setting?.Value ?? defaultValue`). The literal `'LRN-2026-000001'` in the incident report is the **default** talk-certificate prefix — i.e. either the tenant never customised it, or explicitly set it to `LRN`.
- `2026` — `DateTime.UtcNow.Year` (`CertificateGenerationService.cs:205`), i.e. the calendar year at the moment of generation (UTC). The sequence resets to 1 on 1 January UTC each year, because the counting query filters on `CertificateNumber.StartsWith($"{prefix}-{year}-")` — a certificate issued in a different year never contributes to the count for the current year.
- `000001` — a `D6`-formatted (6-digit, zero-padded) running count, computed as `count + 1` where `count` is the number of existing rows for that `(TenantId, prefix, year)` combination.
- **Scope of the running number itself:** per-tenant, per-prefix (talk vs. course), per-calendar-year — established by the `WHERE c.TenantId == tenantId && c.CertificateNumber.StartsWith(pattern)` predicate (`:209`). It resets to `000001` for each tenant independently, and again for each new calendar year, and talk certificates and course certificates count separately (different prefixes, `LRN` vs `TBC`, so their `StartsWith` patterns never overlap).
- **Scope of the uniqueness constraint that actually rejected the second insert — this does NOT match the scope above.** `ToolboxTalkCertificateConfiguration.cs:111-113`:
  ```csharp
  builder.HasIndex(c => c.CertificateNumber)
      .IsUnique()
      .HasDatabaseName("ix_toolbox_talk_certificates_number");
  ```
  This is a **single-column unique index on `CertificateNumber` alone** — no `TenantId` in the index. Confirmed at the SQL level in the original migration, `20260211111901_AddToolboxTalkCertificates.cs:74-79` (`CreateIndex(name: "ix_toolbox_talk_certificates_number", column: "CertificateNumber", unique: true)`), and confirmed unmodified since — no later migration alters or drops this index (grepped every migration `.cs` file that is not a `.Designer.cs` snapshot for `ix_toolbox_talk_certificates_number`; only the original creation migration touches it).

  **This is a structural mismatch, not just a timing race:** the *generation* logic scopes its counting per-tenant, but the *uniqueness constraint* enforces global (cross-tenant) uniqueness. Two different tenants that both use the default `LRN` prefix will, independently and *deterministically* (not merely probabilistically), both compute `count = 0` for their respective first talk certificate of the year and both attempt `LRN-2026-000001` — with no timing coincidence required at all, only that both tenants happen to issue their Nth certificate of the year at similar real-world cadence. If their two *Nth* certificates of the year also happen to fire within the same request-handling window (which "milliseconds apart" in the incident description is consistent with), that is exactly the observed symptom. This mismatch means the incident may not require same-tenant concurrency to reproduce — cross-tenant collision is a second, independent way to hit the same unique-index violation, and is arguably the more likely one for a "day-one" collision on `000001` specifically (two tenants' respective *first* certificates of the year, at any two moments close together) versus two same-tenant employees finishing training in the same instant.

### A.3 — Where allocation happens: synchronous, in-request, no locking

Certificate generation is **entirely synchronous, in-process, inside the HTTP request that completes the training** — confirmed already in the companion recon `docs/certificate-generation-recon.md` §2 (three call sites total: `CompleteToolboxTalkCommandHandler.cs:215` for standalone talks, `CourseProgressService.cs` for course completions, and the admin `RegenerateCertificate` retry endpoint). No Hangfire job is involved anywhere in this flow (re-confirmed here: `GenerateCertificateNumber`/`GenerateTalkCertificateAsync`/`GenerateCourseCertificateAsync` are called only from those three sites, none of which are job classes).

There is **no explicit database transaction wrapping the count-read and the certificate-insert together**, no `SERIALIZABLE`/`REPEATABLE READ` isolation-level override, no `SELECT ... FOR UPDATE`-style row lock, and no application-level lock (semaphore, distributed lock, advisory lock) anywhere in `CertificateGenerationService.cs` or its call sites. A codebase-wide grep for `BeginTransactionAsync`, `SERIALIZABLE`, `FOR UPDATE`, and `ExecutionStrategy` inside `src/` returns only three unrelated hits (`Program.cs`, `ResetTenantDataCommandHandler.cs`, `LessonParserInfrastructureExtensions.cs` — none touch certificates). PostgreSQL's default isolation level (READ COMMITTED) does nothing to prevent this specific count-then-insert race — a `COUNT(*)` under READ COMMITTED simply reflects whatever is committed at the instant it runs; it does not lock or reserve the count for the caller.

**What "protection" does exist today is purely reactive:** the unique index on `CertificateNumber` is the only thing that ever stops a collision — and it does so by throwing after the fact (rejecting the second `INSERT`), not by preventing the two callers from computing the same number in the first place. That rejection is not translated into "retry with a fresh number" anywhere; it surfaces as an ordinary unhandled/caught exception (§B.4).

---

## B. What kind of collision this is

### B.4 — Two different completions racing on the count, vs. the same completion generated twice

These are two structurally distinct failure modes in this codebase, and the evidence supports **the first being the active mechanism, with the second existing as a separate, independently-real gap that is not what caused this specific number collision:**

**(a) Pure numbering race between two different completions — supported as the primary mechanism.**
`ScheduledTalkCompletion` (the entity created when an employee finishes a *standalone* talk) has a **unique index on `ScheduledTalkId`**: `ScheduledTalkCompletionConfiguration.cs:90-92` (`ix_scheduled_talk_completions_talk`, `IsUnique()`, backed by a one-to-one FK relationship at `:84-87`). This means the *same* `ScheduledTalk` cannot produce two `ScheduledTalkCompletion` rows — a second concurrent `/complete` call for the identical scheduled talk would fail at the `SaveChangesAsync` in `CompleteToolboxTalkCommandHandler.cs:200` (which is **not** wrapped in a try/catch — only the later certificate-generation block at `:213-262` is guarded) with a unique-constraint violation, and would never reach certificate generation at all. So a true double-submit (double-click, client retry) *for the same scheduled talk* is already blocked before it can register two certificate attempts.

What is **not** blocked, and is exactly what the incident describes, is two *different* `ScheduledTalk` completions (different employees, and/or different talks, within the same tenant, or across two different tenants per §A.2's cross-tenant finding) both calling `GenerateTalkCertificateAsync` → `GenerateCertificateNumber` in an overlapping window. Each has its own valid, distinct `ScheduledTalkId`/`ScheduledTalkCompletion` row — there is nothing wrong with either completion — but both compute the same `CertificateNumber` because the count they both read was taken before either had committed its insert. **This is the numbering race, and the evidence (unique-completion-index existing, no unique-certificate-per-completion index existing — see (b)) points to this being what actually happened: two legitimate, independent completions, one of which lost the number race and therefore lost its certificate.**

**(b) Same completion generated twice (idempotency gap) — real, but separate, and not the likely cause of *this* specific symptom.**
There is **no unique constraint anywhere preventing two `ToolboxTalkCertificate` rows for the same `ScheduledTalkId`, `CourseAssignmentId`, or `EmployeeId`+talk combination.** The only indexes on `ToolboxTalkCertificateConfiguration.cs:110-123` are: unique on `CertificateNumber` alone (§A.2), plus non-unique indexes on `TenantId`, `EmployeeId`, and the composite `{TenantId, EmployeeId}`. `ScheduledTalkId` and `CourseAssignmentId` are indexed nowhere. This means:
  - The admin `RegenerateCertificate` endpoint (`ToolboxTalksController.cs:2529-2560`) does **not** check whether a certificate already exists for the completion before calling `GenerateTalkCertificateAsync` again (confirmed by reading the full method body, `:2536-2560` — it loads the `ScheduledTalkCompletion`, then calls generation unconditionally). Invoking it twice (e.g. two admin clicks, or a client-side double-submit on the regenerate button) would create a **second, distinct, correctly-numbered** certificate row for the same completion — a duplicate-certificate bug, but not a *number collision* bug, since each call independently runs `GenerateCertificateNumber` and gets a fresh (higher) count each time it actually completes successfully.
  - If a genuinely *identical* generation call were somehow issued twice in true overlap (which requires bypassing the `ScheduledTalkCompletion` unique-index guard described in (a) — not possible via the normal `/complete` endpoint, but the `RegenerateCertificate` endpoint has no such guard at all and could be raced by an admin double-click), you would get two *certificate* rows for the same completion, each independently going through `GenerateCertificateNumber` — and if both overlap in time, that degenerates back into scenario (a), a numbering race, not a "duplicate of the same operation" collision in the sense of two identical, redundant business events being recorded once each successfully.

**Conclusion for B:** this is a **numbering race** (a), not a double-generation/idempotency defect in the sense of the same business event firing twice. The idempotency gap in (b) is real and independently worth fixing (nothing stops a second certificate for one completion), but it is a distinct defect from — and not required to explain — the specific "two inserts racing for the same number" symptom reported.

---

## C. What the fix must preserve

### C.6 — Downstream usage and the constraints that follow from it

`CertificateNumber` is read in the following places (exhaustive, via grep across `src/` and `web/src/` for `CertificateNumber`):

- **Certificate PDF** — printed in the footer of every generated certificate: `CertificateGenerationService.cs:345` (`$"Certificate No: {cert.CertificateNumber}"`), inside `GenerateCertificatePdf` (`:228-367`), used for both talk and course certificates.
- **Downloaded filename** — `CertificateDownloadDto.cs:7`: `public string FileName => $"Certificate-{CertificateNumber}.pdf";` — used by the employee-facing and admin download endpoints (`GetCertificateDownloadQueryHandler.cs:27`, `GetAdminCertificateDownloadQueryHandler.cs:26`).
- **Certificate list/report DTOs surfaced to the UI** — `CertificateDto.cs:6`, `CertificateReportDto.cs:20`, populated by `GetMyCertificatesQueryHandler.cs:27,41`, `GetEmployeeCertificatesQueryHandler.cs:27,40`, `GetCertificateReportQueryHandler.cs:79` — i.e. the admin certificate report, the employee's own certificate list, and the employee-detail certificate list all display this string directly to end users.
- **Storage key** — `UploadCertificatePdf` passes `cert.CertificateNumber` as the object key/identifier to `storageService.UploadCertificateAsync(cert.TenantId, cert.CertificateNumber, ...)` (`CertificateGenerationService.cs:216`) — R2 storage path construction depends on it, so whatever the fix produces must remain a value safe to embed in an R2 object path (the existing format already is, since it's alphanumeric-plus-hyphens).

**No email template, export, or audit/inspection report was found that treats `CertificateNumber` as anything other than a plain display string** — it is not parsed back apart into its year/sequence components anywhere in the reviewed code (`GenerateCertificateNumber` is a pure producer; every consumer treats the whole string as an opaque display value).

**Constraints a fix must satisfy, derived from the above:**
- **Must be unique** — obviously; this is the entire point of the incident, and it is also relied on implicitly wherever the number is shown to a human as "the" certificate identifier (a duplicate would be confusing/non-compliant even setting aside the DB constraint).
- **Uniqueness scope must be decided deliberately, not left mismatched.** Today the *generation* logic scopes per-tenant, but the *constraint* scopes globally (§A.2) — that mismatch is itself part of the bug. Any fix must pick one scope and make the DB constraint match it exactly (most natural: keep per-tenant numbering, but scope the unique index to `{TenantId, CertificateNumber}` rather than `CertificateNumber` alone — though this recon does not propose the fix, only notes the two scopes currently disagree and must be reconciled).
- **No evidence found that the sequence is required to be gap-free.** Nothing in the PDF, the DTOs, the reports, or the storage path treats missing numbers as meaningful — there is no "certificate #4 must exist if #5 exists" check anywhere. The one place a gap could theoretically matter — a compliance audit expecting a contiguous sequence — was not found as an actual implemented check; it would be an assumption imposed from outside the code, not something the current system enforces or relies on. (This recon does not have access to any client-facing compliance documentation that might impose gap-free numbering as an external contractual requirement — that is outside what code inspection can answer, and should be confirmed with whoever owns the compliance relationship before assuming gaps are safe.)
- **Format** (`{prefix}-{year}-{6-digit sequence}`) is a tenant-configurable prefix plus a fixed year/sequence shape baked into `GenerateCertificateNumber`'s string interpolation, the PDF footer, and the downloaded filename. Nothing downstream parses the format back apart, so the *shape* itself is not intrinsically load-bearing beyond "looks like the existing pattern" — but changing it changes what appears on every future certificate PDF and filename, which is a user/compliance-visible change independent of the race fix itself.

### C.7 — Existing certificate volume and downstream dependency on current numbering

**Not directly queryable in this environment** — no `psql`/database client is available on this machine, so this recon cannot report the live count of `ToolboxTalkCertificates` rows, their tenant distribution, or how many already share a prefix/year. (The companion recon `docs/certificate-generation-recon.md` §4 notes the *local* Development database had zero course rows at the time it was written, and Development is a different environment from Production where the incident occurred — so even a local-DB query, had it been possible, would not answer the Production question.)

What can be established from code alone:
- **Certificates are immutable snapshots** — every field on `ToolboxTalkCertificate` other than `CertificateNumber`/`PdfStoragePath`/`CertificateEmailFailed` is a point-in-time snapshot (`EmployeeName`, `TrainingTitle`, `LearningCode`, etc. — see `ToolboxTalkCertificate.cs:72-108` comment "Snapshot of data at time of issue"). No code path re-derives or re-validates a certificate's number against live data after issuance, so a fix that only changes *future* number generation cannot retroactively corrupt any existing certificate's number, PDF, or storage path — those are already-baked byte streams and DB rows, untouched by a forward-only fix.
- **A fix does not need to renumber existing rows** to be correct — since nothing parses gaps or re-derives sequence membership from anything but a fresh `COUNT`/`MAX` query at generation time (§A.1), whatever fix replaces `GenerateCertificateNumber` only needs to continue producing numbers that (a) don't collide with any number already in the table for that scope, and (b) satisfy whatever new/corrected uniqueness constraint is put in place. If the constraint's scope changes (e.g. becomes `{TenantId, CertificateNumber}`), that migration must run cleanly against however many existing rows exist in Production today — a fact this recon could not verify from here and should be checked directly against Production (or a Production snapshot) before writing that migration.

---

## D. Existing patterns — anything already solving this safely?

**No.** Every other "human-readable reference number" generator found in the codebase uses the same unsafe, application-level count-or-max-then-increment pattern, with no locking, no sequence, no transaction:

| Generator | File:Line | Pattern | Uniqueness constraint scope |
|---|---|---|---|
| `ToolboxTalkCertificate.CertificateNumber` | `CertificateGenerationService.cs:203-211` | `COUNT` within `{TenantId, prefix, year}`, then `+1` | **Global** on `CertificateNumber` alone (`ToolboxTalkCertificateConfiguration.cs:111-113`) — **mismatched**, see §A.2 |
| `Employee.EmployeeCode` | `EmployeeService.cs:551-580ish` (`GenerateEmployeeCodeAsync`) | Loads **all** existing tenant codes, regex-parses the numeric suffix of `EMP\d+`-shaped codes, takes `max+1` | Composite `{TenantId, EmployeeCode}`, unique (`EmployeeConfiguration.cs:70-72`) — **correctly scoped**, but the generation itself is still susceptible to the identical TOCTOU race (two concurrent employee creates in the same tenant could compute the same max) |
| `ToolboxTalk.Code` | `CreateToolboxTalkCommandHandler.cs:176-217` (`GenerateCodeAsync`) | Loads existing tenant codes matching a title-derived prefix, parses numeric suffix, takes `max+1` | Per CLAUDE.md, tenant-scoped `IX_ToolboxTalks_TenantId_Code` — same TOCTOU exposure as `EmployeeCode` |

No `HasSequence`/database-sequence usage, no `RowVersion`/optimistic-concurrency token usage for this class of problem, and no distributed-lock or advisory-lock helper exists anywhere in the codebase (confirmed by the same grep as §A.3 plus a separate grep for `HasSequence` and `RowVersion`/`ConcurrencyCheck`, both empty for these three entities). **There is no safe pattern anywhere in this codebase to mirror — all three "reference number" generators share the same class of defect.** The certificate case is simply the one that got exercised into a visible production failure first, helped by its additional constraint-scope mismatch (§A.2) that the other two do not have.

---

## Summary — direct answers to the brief

**(a) How the number is generated, and why concurrent callers collide:** `GenerateCertificateNumber` (`CertificateGenerationService.cs:203-211`) runs a plain `COUNT(*)` scoped to `{TenantId, prefix, year}`, then formats `count+1` as a 6-digit suffix — entirely in application code, no DB sequence, no lock, no shared transaction with the subsequent insert. Any two calls whose count-read and insert-write windows overlap for the same `{TenantId, prefix, year}` (same tenant) — or, independently, any two *different* tenants sharing the default `LRN`/`TBC` prefix computing their respective first-of-the-year counts — will produce the identical string. The only thing that ever catches this is the DB's unique index, after the fact, as an insert failure.

**(b) Numbering race vs. double-generation, or both:** This is a **numbering race between two different, legitimate completions** (§B.4a) — not the same completion/business-event firing twice. A separate, real idempotency gap exists (no unique constraint on `ToolboxTalkCertificate` per `ScheduledTalkId`/`CourseAssignmentId`, and the admin `RegenerateCertificate` endpoint has no existing-certificate check), but that gap produces *duplicate certificates*, not *colliding numbers*, and is not what this incident's symptom (`ix_toolbox_talk_certificates_number` rejecting a second insert with the identical string) indicates.

**(c) What the numbering must preserve:** global uniqueness (yes, always); a scope for the running sequence that is currently per-tenant/per-prefix/per-year in the generator but is enforced globally by the DB constraint — this mismatch must be resolved as part of any fix, one way or the other, not left as-is; no evidence of a gap-free requirement anywhere in the code (though this should be confirmed against any external compliance commitments, which are outside what code inspection can see); the format string is consumed as an opaque display value everywhere (PDF, filename, DTOs, storage key) and is not parsed back apart, so the shape itself has display/branding weight but no structural downstream dependency.

**(d) Existing safe pattern to mirror:** none exists. `EmployeeCode` and `ToolboxTalkCode` use the same unsafe max-then-increment approach; `EmployeeCode`'s uniqueness constraint is at least correctly tenant-scoped (unlike certificates'), but its generation is equally exposed to the same class of race — it has simply not yet produced a visible incident.

---

## Appendix: files referenced

- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CertificateGenerationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkCertificate.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ToolboxTalkCertificateConfiguration.cs`
- `src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260211111901_AddToolboxTalkCertificates.cs`
- `src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingKeys.cs`
- `src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingsService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CompleteToolboxTalk/CompleteToolboxTalkCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ScheduledTalkCompletionConfiguration.cs`
- `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` (`RegenerateCertificate`, `:2529-2560`)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/Certificates/DTOs/CertificateDto.cs`, `CertificateReportDto.cs`, `CertificateDownloadDto.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/Certificates/Queries/GetMyCertificatesQueryHandler.cs`, `GetEmployeeCertificatesQueryHandler.cs`, `GetCertificateReportQueryHandler.cs`, `GetCertificateDownloadQueryHandler.cs`, `GetAdminCertificateDownloadQueryHandler.cs`
- `src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs` (`GenerateEmployeeCodeAsync`)
- `src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/EmployeeConfiguration.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalk/CreateToolboxTalkCommandHandler.cs` (`GenerateCodeAsync`)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Storage/R2StorageService.cs`
- `docs/certificate-generation-recon.md` (prior recon, cross-referenced for call-site and flow facts not re-derived here)
