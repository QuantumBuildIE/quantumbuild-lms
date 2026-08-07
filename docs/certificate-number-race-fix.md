# Certificate Number Race — Fix

Companion to `docs/certificate-number-race-recon.md`. That recon identified two independent bugs
in certificate number generation; this document records what was changed and how to find
learners who were already affected before the fix.

## What was wrong (recap)

1. **Deterministic cross-tenant collision.** `CertificateGenerationService.GenerateCertificateNumber`
   counts per `{TenantId, prefix, year}`, but the old unique index
   `ix_toolbox_talk_certificates_number` was global (on `CertificateNumber` alone). Two different
   tenants on the default `LRN` prefix both computed `LRN-{year}-000001` for their first
   certificate of the year and collided with no timing race required.
2. **Intermittent within-tenant race.** The number was computed via `COUNT(*) + 1` in application
   code, then real elapsed time passed (PDF render + R2 upload) before the row was inserted, with
   no lock or transaction tying allocation to insert. Two completions in the same tenant within
   that window could compute the same number.

## What changed

- **`CertificateNumberCounter`** (new entity/table, `toolbox_talks.CertificateNumberCounters`) —
  one row per `{TenantId, Prefix, Year}`, holding `LastNumber`. Unique index on
  `{TenantId, Prefix, Year}`.
- **`CertificateGenerationService.GenerateCertificateNumber`** now allocates the next number via a
  single atomic `INSERT ... ON CONFLICT (TenantId, Prefix, Year) DO UPDATE SET LastNumber =
  LastNumber + 1 ... RETURNING LastNumber` statement, executed *before* PDF render (the number is
  printed on the certificate and used as the R2 storage key, so it must be reserved up front, not
  assigned after the expensive work). Postgres serializes concurrent upserts to the same
  conflicting key, so two simultaneous callers for the same tenant/prefix/year always get distinct
  numbers — no `COUNT(*)` read-then-write window remains.
- **`ix_toolbox_talk_certificates_number`** on `ToolboxTalkCertificates` changed from a global
  unique index on `CertificateNumber` alone to a composite unique index on
  `{TenantId, CertificateNumber}`, matching the per-tenant scope the generator already used.
  Migration: `20260807093155_AddCertificateNumberCountersAndFixCertificateNumberIndexScope`.

Gaps in the sequence remain acceptable (unchanged from before) — nothing downstream parses the
number, and a certificate that fails to insert after a number was allocated simply leaves a gap.

## Migration safety against existing data

The index change is a **loosening**: global uniqueness on `CertificateNumber` is a strictly
stronger constraint than tenant-scoped uniqueness on `{TenantId, CertificateNumber}` — any row set
that satisfies the old constraint automatically satisfies the new one, because two rows sharing a
`{TenantId, CertificateNumber}` pair would necessarily also share `CertificateNumber` alone, which
the old index already forbids. No existing row can violate the new index. This was not verified
against a live database dump — no `psql`/database client was available in this environment (same
constraint noted in the recon) — but the proof does not depend on the actual data shape; it holds
for any data that was accepted under the prior (strictly stricter) constraint. Before running this
migration against Production, it is still good practice to run
`SELECT "TenantId", "CertificateNumber", COUNT(*) FROM toolbox_talks."ToolboxTalkCertificates" GROUP BY 1, 2 HAVING COUNT(*) > 1;`
first — it should return zero rows, confirming the assumption directly.

## Remediation — finding already-affected completions

Read-only. Finds employees who completed training that should have produced a certificate but
didn't (talk-level and course-level), so they can be identified for manual re-issue via the
existing `RegenerateCertificate` endpoint. Does not modify any data.

```sql
-- Talk-level: completed standalone talks whose talk requires a certificate but has none.
SELECT
    'Talk'                          AS certificate_type,
    st."TenantId"                   AS tenant_id,
    st."EmployeeId"                 AS employee_id,
    st."Id"                         AS scheduled_talk_id,
    NULL::uuid                      AS course_assignment_id,
    stc."CompletedAt"               AS completed_at,
    tt."Title"                      AS training_title,
    tt."Code"                       AS learning_code
FROM toolbox_talks."ScheduledTalkCompletions" stc
JOIN toolbox_talks."ScheduledTalks" st
    ON st."Id" = stc."ScheduledTalkId"
JOIN toolbox_talks."ToolboxTalks" tt
    ON tt."Id" = st."ToolboxTalkId" AND tt."IsDeleted" = false
LEFT JOIN toolbox_talks."ToolboxTalkCertificates" cert
    ON cert."ScheduledTalkId" = st."Id" AND cert."IsDeleted" = false
WHERE st."CourseAssignmentId" IS NULL   -- course-scoped talks handled by the course-level branch below
  AND tt."GenerateCertificate" = true
  AND cert."Id" IS NULL

UNION ALL

-- Course-level: completed course assignments whose course requires a certificate but has none.
SELECT
    'Course'                        AS certificate_type,
    ca."TenantId"                   AS tenant_id,
    ca."EmployeeId"                 AS employee_id,
    NULL::uuid                      AS scheduled_talk_id,
    ca."Id"                         AS course_assignment_id,
    ca."CompletedAt"                AS completed_at,
    tc."Title"                      AS training_title,
    NULL                            AS learning_code
FROM toolbox_talks."ToolboxTalkCourseAssignments" ca
JOIN toolbox_talks."ToolboxTalkCourses" tc
    ON tc."Id" = ca."CourseId" AND tc."IsDeleted" = false
LEFT JOIN toolbox_talks."ToolboxTalkCertificates" cert
    ON cert."CourseAssignmentId" = ca."Id" AND cert."IsDeleted" = false
WHERE ca."Status" = 2                  -- CourseAssignmentStatus.Completed
  AND ca."IsDeleted" = false
  AND tc."GenerateCertificate" = true
  AND cert."Id" IS NULL

ORDER BY tenant_id, completed_at;
```

Each returned row is a learner whose completed training never produced a certificate. Talk-level
rows carry `scheduled_talk_id`; course-level rows carry `course_assignment_id` — one of the two is
always null, matching which branch produced the row. Follow-up re-issue (calling
`RegenerateCertificate` for talk rows, or the equivalent course path) is a separate action, not
performed by this query.

Note: this query does not distinguish *why* a completion has no certificate — the numbering race
is one cause, but so is any other certificate-generation failure (e.g. R2 upload failure, already
covered by the existing `CertificateGenerationFailed` flag surfaced elsewhere). It surfaces the
full "missing certificate" set, which is the correct scope for remediation regardless of root
cause.
