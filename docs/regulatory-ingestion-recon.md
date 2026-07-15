# Recon: Regulatory Document Upload & Ingestion Flow

Date: 2026-07-15
Scope: Read-only recon. No code changed.

## 1. What the feature is designed to do

The **Regulatory > System Administration** area (`/admin/regulatory/system`, SuperUser-only,
gated by `Tenant.Manage`) lets a SuperUser point a `RegulatoryDocument` record at a
network-accessible document and trigger AI extraction of training/compliance requirements
from it via Claude Sonnet. Extracted items land as `Draft` `RegulatoryRequirement` rows for
review/approval; approved ones become visible to tenants via the separate
`RegulatoryBrowseController` (`Learnings.Admin`-gated, tenant-scoped).

There is **no "create a new regulation" flow anywhere in the product** — this is the first
and most important correction to the task framing. `RegulatoryDocument` rows are exclusively
system-seeded (`RegulatoryProfileSeedData.cs`, 4 rows: HIQA, HSA, FSAI, RSA) with `SourceUrl`
left `null` at seed time. The SuperUser's only available action on an existing seeded document
is to **populate or overwrite its `SourceUrl`** via a plain text `Input` field on the document
detail page, then click **Ingest Requirements**. There is no `POST /api/regulatory/documents`
endpoint, no "New Document" button, and no document-creation UI at all.

## 2. What `SourceUrl` is expected to contain

`SourceUrl` (`RegulatoryDocument.cs:21`, nullable `string`) is expected to be an
**HTTP(S)-fetchable URL** that the Hangfire job can reach with a plain `HttpClient.GetAsync`
call from the API server — either:

- A URL ending in `.pdf` → routed to `IPdfExtractionService.ExtractTextFromUrlAsync`
  (downloads via `HttpClient`, parses with PdfPig), or
- Any other URL → treated as an HTML page, fetched via `HttpClient.GetAsync`, then stripped
  to plain text with a regex-based tag-stripper (`RequirementIngestionJob.StripHtmlToText`).

There is **no file-upload step** anywhere in this flow. `SourceUrl` is not a display-only
field populated by an upload — it is the sole, direct input to the fetch. The field's
placeholder text (`https://example.com/document.pdf`, `system/page.tsx:547`) is the only hint
given to the user about the expected format; there is no format validation, help text, or
example beyond the placeholder.

I checked whether the codebase has an established "upload a file → get back a storage URL →
paste that URL in" pattern that this feature *should* be using but isn't wired to. It does:
`web/src/features/toolbox-talks/components/learning-wizard/hooks/useUploadSourceFile.ts`
implements exactly that pattern for the Learnings content wizard (upload video/PDF → R2 →
URL). The regulatory ingestion flow does not reuse it, and no equivalent hook or backend
endpoint exists for regulatory documents. This looks like a feature gap relative to the
wizard's own established pattern, not an oversight in wiring — it was simply never built.

## 3. What the user's reported case (Windows path) does at each layer

Reported input: a `C:\...\document.pdf` local Windows filesystem path pasted into the Source
URL field.

**Frontend (`system/[documentId]/page.tsx`):**
- The `Input` is a plain text field with no `type="url"`, no Zod/regex validation, and no
  `pattern` attribute. Any non-empty string enables the **Ingest Requirements** button
  (`disabled={... || !effectiveSourceUrl.trim()}` — only checks for non-blank, not shape).
- Clicking the button calls `POST /api/regulatory/documents/{id}/ingest` with
  `{ sourceUrl: "C:\...\document.pdf" }`.

**Backend — `RequirementIngestionController.StartIngestion` →
`RequirementIngestionService.StartIngestionAsync`:**
- Persists the string verbatim into `RegulatoryDocument.SourceUrl` with a
  `string.IsNullOrWhiteSpace` check only — **no URI-shape validation at all**.
- Enqueues `RequirementIngestionJob.ExecuteAsync` via Hangfire (`BackgroundJob.Enqueue<RequirementIngestionJob>` — correctly using the concrete class per Note 21) and returns
  an `IngestionSessionDto` with `Status = "Queued"` immediately. The frontend shows a success
  toast ("Ingestion job queued") and starts 3s polling for up to 120s.

**Backend — `RequirementIngestionJob.ExecuteAsync` (Hangfire worker, async, off the HTTP
request):**
1. Loads the document — found, has a non-blank `SourceUrl` — proceeds.
2. `FetchDocumentTextAsync(sourceUrl)` → since the string ends in `.pdf`, calls
   `IPdfExtractionService.ExtractTextFromUrlAsync("C:\...\document.pdf", ...)`.
3. Inside `PdfExtractionService.ExtractTextFromUrlAsync`
   (`Services/Pdf/PdfExtractionService.cs:113`): `await _httpClient.GetAsync(pdfUrl, ...)`.
   `HttpClient.GetAsync(string)` builds a `Uri` internally with `UriKind.RelativeOrAbsolute`.
   .NET's `Uri` class has a long-standing quirk where a drive-letter path like `C:\foo\bar.pdf`
   parses successfully as an **absolute `file://` URI**, not as a malformed string. So this
   does not throw a `UriFormatException` at construction time. Instead, `HttpClient` then
   attempts to send the request over the `file` scheme, which it does not support, and throws
   `NotSupportedException` ("The requested URI scheme (file) is not supported.") when the
   returned `Task` is awaited.
4. `PdfExtractionService.ExtractTextFromUrlAsync`'s `try/catch` only catches
   `HttpRequestException` and `TaskCanceledException` — **not** `NotSupportedException`, so the
   exception is not handled here and propagates out uncaught.
5. It is caught one level up, in `RequirementIngestionJob.FetchDocumentTextAsync`'s
   general `catch (Exception ex)` block (`Jobs/RequirementIngestionJob.cs:174-178`), which
   logs `"Failed to fetch document from {Url}"` at `LogError` and returns `null`.
6. Back in `ExecuteAsync`, `extractedText` is `null` →
   `_logger.LogError("Failed to extract text from document {DocumentId}", ...)` and the method
   **returns immediately** (`Jobs/RequirementIngestionJob.cs:88-92`) — **without setting
   `document.LastIngestedAt`** and without writing anything else to the database.

**Result:**
- `LastIngestedAt` stays `null` forever (nothing set it before, nothing sets it now).
- `GetIngestionStatusAsync` derives `Status` purely from `document.LastIngestedAt.HasValue`
  (`Idle` if `null`, `Completed` if set) — so status remains **`Idle`** indefinitely.
- Zero `RegulatoryRequirement` rows are created → **"No Requirements"** stays showing.
- The only trace of the failure is a server-side log line. `IngestionSessionDto` has **no
  error/failure field at all** (`IngestionDtos.cs:6-16`) — there is structurally no channel
  for the backend to tell the frontend "this failed and here's why," even if it wanted to.
- The frontend's 120-second polling window expires, `isPolling` flips back to `false`, and the
  UI silently reverts to showing `Idle` / `0 drafts` with no toast, no error banner, nothing.

This exactly reproduces the reported symptom.

## 4. Bug or expected behaviour?

**Both** — two distinct problems compound to produce the observed silent failure:

1. **UX/validation gap (the proximate trigger):** The Source URL field accepts and persists
   any non-blank string with zero format validation, front or back end. A local filesystem
   path being an "obviously wrong" input for a server-side fetch is true, but the system gives
   the user no signal that it's wrong — not at entry, not at submit, not after failure. This is
   squarely a UX/validation bug, not "the user misused an otherwise self-explanatory field":
   the field's only guidance is a placeholder showing an `https://` example, which the user's
   value visibly didn't match, and nothing enforced or warned about that mismatch.

2. **Backend error-handling bug (the reason the failure is invisible):** Independent of *why*
   the fetch failed, `RequirementIngestionJob.ExecuteAsync` has no failure path that's
   distinguishable from "hasn't run yet." Any fetch failure — unreachable host, 404, timeout,
   malformed URL, blocked scheme, whatever — collapses to the same silent no-op: log line,
   early return, `LastIngestedAt` untouched, `Idle` forever. This would reproduce identically
   for e.g. a broken but well-formed `https://` URL, a URL requiring auth, or a transient
   network blip — none of which are user error. There is no `IngestionStatus` enum, no
   `FailedAt`/`ErrorMessage` column on `RegulatoryDocument`, and no DTO field to carry a
   failure reason to the frontend even if the job wrote one.

So: the specific *Windows-path* input is user error in the sense that it was never going to
work, but the *silent, indistinguishable-from-not-started failure mode* is a genuine defect
that would affect legitimate URLs too, and is the more serious of the two issues.

## 5. Recommended fix scope (not prompts — just shape of the work)

Roughly independent, combinable pieces:

- **Backend URI validation at submit time** — `RequirementIngestionService.StartIngestionAsync`
  should reject non-`http`/`https` schemes (e.g. `Uri.TryCreate` + scheme check) before
  enqueueing, returning a 400 the frontend can toast immediately instead of a fake "Queued"
  success.
- **Frontend format guidance** — `type="url"` / lightweight client-side validation on the
  Source URL input, disabling Ingest until it looks like an `http(s)://...` URL, mirroring the
  placeholder's implicit contract.
- **Job-level failure surfacing** — give `RegulatoryDocument` (or a new lightweight status
  table, consistent with how `SubtitleProcessingJob`/`ContentCreationSession` already model
  job status) an explicit failure state distinct from "never run": e.g.
  `LastIngestionStatus` (Idle/Running/Succeeded/Failed) + `LastIngestionError`, set in every
  code path of `ExecuteAsync` including the early-return branches, and surfaced through
  `IngestionSessionDto` so the frontend can show a real error instead of reverting to `Idle`.
- **Broaden the caught exception set** in `PdfExtractionService.ExtractTextFromUrlAsync` (or
  more precisely, let unsupported-scheme/URI errors be caught and turned into a clear
  `PdfExtractionResult.FailureResult` message rather than an uncaught exception that a caller
  three frames up has to catch generically).
- **Optional, larger scope:** an actual file-upload step (reusing the R2 upload pattern from
  `useUploadSourceFile.ts` in the Learnings wizard) so SuperUsers can upload a local PDF
  directly instead of needing a public URL at all. This is a real feature gap, not just an
  error-handling fix, and is a separate, larger piece of work.

## 6. Diagnosis

**Both a UX gap and a backend bug, compounding.** The Windows path is user error given the
field's intent, but the field gives no indication that it's wrong, and — independent of this
specific input — the ingestion job has no way to report failure at all. A legitimate but
broken `https://` URL would produce the exact same silent "stuck on Idle, zero requirements"
symptom. Fixing only the URL-format issue (validation/upload) would still leave future
legitimate-URL failures silent; fixing only the error-surfacing would still let users submit
obviously-wrong input with no warning. Both should be addressed together.

## 7. Supporting evidence

**Entity:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/RegulatoryDocument.cs`
— `SourceUrl` (nullable string), `LastIngestedAt` (nullable, the sole status signal), no
`IsActive`/error fields relevant to ingestion status.

**Seed data (only creation path for `RegulatoryDocument`):**
`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Seed/RegulatoryProfileSeedData.cs:67-102`
— 4 hardcoded documents (HIQA/HSA/FSAI/RSA), `SourceUrl` never set at seed time.

**Frontend list page:** `web/src/app/(authenticated)/admin/regulatory/system/page.tsx` — read-only
table + "Manage" link per row; no create action anywhere.

**Frontend detail page:** `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:412-570`
— plain `Input` for Source URL (no validation), Ingest button gated only on non-blank text.

**Controller:** `src/QuantumBuild.API/Controllers/RegulatoryIngestionController.cs` — class-gated
`Tenant.Manage`; `StartIngestion` action, no request-body validation beyond what the service does.

**Service (persists `SourceUrl`, enqueues job, no format check):**
`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Ingestion/RequirementIngestionService.cs:28-54`

**Job (silent early-return on fetch failure, `LastIngestedAt` untouched):**
`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementIngestionJob.cs:60-179`

**PDF fetch (narrow catch, misses `NotSupportedException` from `file://` scheme):**
`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Pdf/PdfExtractionService.cs:113-161`

**DTO (no error/failure field to carry a reason to the frontend):**
`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DTOs/Validation/IngestionDtos.cs:6-16`

**Polling:** `web/src/lib/api/admin/use-regulatory-ingestion.ts:46-56` — 3s interval, 120s
window set by the page component, no distinct handling for a failure state because none exists
upstream.

**Recent git history on the job file** (`git log -- .../RequirementIngestionJob.cs`):
```
832abde fix(p0): Anthropic model deprecation — multi-provider config unification
2a0f701 feat: add per-tenant AI usage logging across all Claude API call sites
9961317 fix: guard against null principle/section values ... require section/principle fields
4c20b88 fix: standardise section reference format in ingestion prompt and shorten PDF footer
b619356 fix: normalize PrincipleLabel to canonical form and fix Add Mapping dialog
f0ed54d feat: regulatory requirements and ingestion pipeline
```
Confirmed by diff: `832abde` (the main-branch Anthropic model-retirement P0 hotfix referenced
in session context) touched only the Sonnet model-ID source (`SonnetModel` const →
`IOptions<AIProviderOptions>`-driven `_sonnetModel`), i.e. `Jobs/RequirementIngestionJob.cs:242`.
It did **not** touch `FetchDocumentTextAsync`, the PDF/HTML fetch logic, or any error-handling
path. The silent-failure behaviour predates that hotfix and is unrelated to it — it traces back
to the original `f0ed54d feat: regulatory requirements and ingestion pipeline` commit.

**Tests:** No test exercises `StartIngestionAsync`, `RequirementIngestionJob.ExecuteAsync`, or
`PdfExtractionService.ExtractTextFromUrlAsync` against either a valid remote URL, an invalid
URL, or any upload step. The only existing test touching `RegulatoryDocument`
(`tests/QuantumBuild.Tests.Integration/ToolboxTalks/RegulatoryApplicabilityTests.cs`) creates
one directly via `DbContext` as a fixture for an unrelated applicability endpoint — it does not
exercise the ingestion flow at all.
