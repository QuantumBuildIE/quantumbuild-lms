# Bulk SOP Import Flow — Recon (Bug A, Bug B, and other per-item defects)

**Read-only recon. No code changed.** Branch: `transval`. All claims cited `file:line`, paths
relative to repo root. Facts marked "confirmed" were read directly in this session; facts carried
from the earlier design-phase recon (`docs/bulk-learning-generation-recon.md`,
`docs/regulation-mapping-live-only-recon.md`) are marked as such and were spot-checked against
current code where central to this investigation.

---

## A. The flow end to end

### A.1 Session lifecycle (once per session)

| Step | Where | Per-session or per-item |
|---|---|---|
| Upload ZIP, validate archive (size/entry/PDF-count limits) | `BulkSopImportController.Upload` (`src/QuantumBuild.API/Controllers/BulkSopImportController.cs:106-166`) → `BulkSopImportValidationService.ValidateAsync` (`src/Core/QuantumBuild.Core.Infrastructure/Services/BulkSopImportValidationService.cs:29-130`) | Once |
| ZIP itself re-uploaded to R2 as scratch | `BulkSopImportController.cs:137-139` → `UploadSessionFileAsync(tenantId, sessionId, ..., "upload.zip", ...)` | Once |
| `BulkSopImportSession` row created, `Status = Validated` | `BulkSopImportController.cs:145-158` | Once |
| Confirm → enqueue Hangfire job (concrete class, `[AutomaticRetry(Attempts=0)]`) | `BulkSopImportController.Confirm` (`:176-217`), stuck-session recovery at `:183-201` (30 min threshold) | Once |
| Job loads session, flips `Status → Processing`, deserialises `ValidationResultJson`, downloads the ZIP bytes once | `BulkSopImportJob.ExecuteAsync` (`src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs:62-119`) | Once |
| **Per-PDF loop** | `:114-119`, `foreach (var file in validationResult.Files) → ProcessItemAsync(...)` | **Per item** — see A.2 |
| Build aggregate result, `Status → Completed`, persist `ProcessingResultJson` | `:121-131` | Once |
| **R2 cleanup — deletes the ZIP AND every re-uploaded PDF under the session prefix** | `:133-144`, `DeleteSessionFilesAsync(tenantId, sessionId, ct)` | Once, **after all items are done** |
| On unhandled job-level exception: partial `ProcessingResultJson` saved, `Status → Failed`, ZIP intentionally **not** deleted | `:146-168` | Once |

### A.2 Per-PDF processing (`ProcessItemAsync`, `BulkSopImportJob.cs:175-316`)

Each iteration:
1. Look up the ZIP entry by `EntryName` (`:185-189`).
2. Derive a title from the filename (`DeriveTitle`, `:331-339`).
3. **Fresh DI scope per item** (`:193`, `await using var itemScope = _scopeFactory.CreateAsyncScope();`) — Note 23 pattern, confirmed correct.
4. Set `IJobTenantContextAccessor.TenantId` on that fresh scope (`:205`) — registered `AddScoped` (`src/QuantumBuild.API/Program.cs:141`), so this is genuinely per-item, not shared/leaked across iterations. Confirmed correct; this is the reference-correct instance of the Hangfire/HttpContext tenant-gap pattern per `docs/schedule-job-tenant-context-recon.md:177-193`.
5. Title-uniqueness pre-check via `IgnoreQueryFilters().Where(t => t.TenantId == tenantId && t.Title == title && !t.IsDeleted)` (`:210-214`) — see §D.1, a real per-item edge case.
6. Upload the PDF bytes to R2 **scratch** storage (`:243-244`, see §B).
7. `InitialiseToolboxTalkCommand` → creates the `ToolboxTalk` row, `Status = Draft` (`:251-259`).
8. `ParseToolboxTalkContentCommand` (`:261-271`) — synchronous, extracts sections from the PDF.
9. `GenerateToolboxTalkQuizCommand` (`:273-284`) — synchronous; failure here is recorded as a `Warning`, not a `Failed` outcome (talk still created with sections, no quiz).
10. Flag `BulkTranslationPendingSince = UtcNow` on the created talk (`:289-296`) for `BulkLearningTranslationSweepJob` to pick up later.
11. Return a `Succeeded`/`Failed`/`AlreadyExisted` outcome; the outer loop continues regardless (`:307-316`, per-item `catch`).

**The loop never publishes, never sets a category/sector, and never triggers `RequirementMappingJob`.** Confirmed by full read of `BulkSopImportJob.cs` — no call to `PublishToolboxTalkCommand`, `RequirementMappingJob`, or any `Category`/`SectorKey` field anywhere in the file. Every created talk lands in exactly the same state as a wizard user who stopped right after Step 3 (Quiz) — a fact the class-level doc comment (`:23-38`) and the controller's doc comment (`BulkSopImportController.cs:60-62`, *"Translation is out of scope... learnings land as Draft with no target languages"*) both state explicitly as intentional scope, not a bug.

Frontend confirms there is no bulk-publish escape hatch: the results panel's only next action is **"Review Draft Learnings"**, linking to the drafts list (`web/src/features/toolbox-talks/components/bulk-sop-import/BulkSopImportResultsPanel.tsx:228-232`). Each draft must be opened and published individually through the standard wizard (`getStepUrl`, `:62`). No bulk-select/bulk-publish UI exists anywhere in the learnings domain (confirmed absent in the original design recon, `docs/bulk-learning-generation-recon.md:221-225`, §4.5 — re-confirmed here: no such control exists in this results panel or the drafts list).

---

## B. Bug A — PDFs deleted after import (confirmed)

### B.1 Mechanism, confirmed

- **Upload (per item):** `_storageService.UploadSessionFileAsync(tenantId, sessionId, seekableStream, $"{file.ItemIndex:D3}_{file.FileName}", "application/pdf", ct)` (`BulkSopImportJob.cs:243-244`).
- `UploadSessionFileAsync` builds the key as `{tenantId}/sessions/{sessionId}/{itemIndex:D3}_{fileName}` (`R2StorageService.cs:777-778`, `SessionsFolder = "sessions"` at `:765`) and returns a **public URL under that same scratch prefix** (`:795`).
- That URL is passed straight through as `SourceFileUrl` into `InitialiseToolboxTalkCommand` (`BulkSopImportJob.cs:256`), and the handler copies it into **both** `ToolboxTalk.SourceFileUrl` and `ToolboxTalk.PdfUrl` (`InitialiseToolboxTalkCommandHandler.cs:89,97` — `PdfUrl = request.InputMode == InputMode.Pdf ? request.SourceFileUrl : null`). **`PdfUrl` is the field `SlideshowGenerationService.GenerateFromPdfAsync` and the Slideshow toggle read** (comment at `InitialiseToolboxTalkCommandHandler.cs:94-96`) — so any later slideshow generation for a bulk-imported talk reads a URL that is about to be deleted.
- **Cleanup (once per session, after the whole loop):** `await _storageService.DeleteSessionFilesAsync(tenantId, sessionId, ct)` (`BulkSopImportJob.cs:137`). `DeleteSessionFilesAsync` lists and deletes **every object** under prefix `{tenantId}/sessions/{sessionId}/` (`R2StorageService.cs:815,819-842`) — which is exactly the prefix every PDF from step 1 was just uploaded to, plus the ZIP itself (`upload.zip`, uploaded to the same prefix at `BulkSopImportController.cs:138-139`).
- Cleanup is wrapped in try/catch and only reached on the success path (`:135-144`); it is **skipped** on the job-level-exception path (`:156`, comment: *"ZIP is intentionally left in R2 on job-level failure for investigation"*) — but that only protects the ZIP in a crash scenario, not the PDFs in the (overwhelmingly common) success scenario.

**Net effect, confirmed:** every successfully-imported talk's `SourceFileUrl`/`PdfUrl` points at an object that is deleted moments later, in the same job run, once ALL items finish. Any later read — `GET .../talks/{id}` PDF preview, `POST /{id}/generate-slides`, `ContentGenerationJob.GenerateSlideshowOnlyAsync` on publish (`ToolboxTalksController.cs:692-704`, fires if `GenerateSlidesFromPdf` is set) — 404s against R2.

This is **not** about the ZIP — the ZIP (`upload.zip`) is correctly scratch (nothing else references it after validation) — it's specifically that PDFs are scratch-uploaded via `UploadSessionFileAsync` instead of permanently via `UploadPdfAsync`.

### B.2 Reference: how the single (wizard) path avoids this

`ToolboxTalkFilesController.UploadPdf` (`src/QuantumBuild.API/Controllers/ToolboxTalkFilesController.cs:127-174`) → `IR2StorageService.UploadPdfAsync` (`R2StorageService.cs:164-213`), key `BuildKey(tenantId, PdfsFolder, fileName)` where `PdfsFolder = "pdfs"` (`:23,184`) — a **talk-ID-derived, permanent** key (`_slugGenerator.GenerateFileName(talkTitle, toolboxTalkId, "pdf")`, `:183`), never touched by any session-cleanup job. `talk.PdfUrl`/`PdfFileName` are set directly from this permanent URL (`ToolboxTalkFilesController.cs:164-165`). There is no code path in the single-talk wizard that ever calls `UploadSessionFileAsync` for a PDF that becomes permanent — `UploadSessionFileAsync` is used elsewhere only for genuinely scratch content-creation-session uploads (`ContentCreationSessionService.cs`), which are correctly wiped by `DeleteSessionFilesAsync`/`ExpiredSessionCleanupJob`.

### B.3 Fix surface (not implemented — for scoping only)

Two options, both localized to `BulkSopImportJob.ProcessItemAsync` (`:238-249`):

- **(a) Upload directly to the permanent location.** Replace the `UploadSessionFileAsync` call with `IR2StorageService.UploadPdfAsync(tenantId, talk.Id, title, seekableStream, file.FileName, ct)` — but `UploadPdfAsync` needs a `toolboxTalkId`, which doesn't exist yet at upload time (upload currently happens *before* `InitialiseToolboxTalkCommand`, `:243-244` precedes `:251-259`). Would require reordering: create the talk first (with a temporary/no `SourceFileUrl`), then upload to `pdfs/{tenantId}/{talkId}...`, then patch `SourceFileUrl`/`PdfUrl` onto the already-created entity — or generate the talk `Guid` client-side in the job (`Guid.NewGuid()`) before calling `InitialiseToolboxTalkCommand`, if the command can accept a pre-assigned ID (currently it always assigns its own, `InitialiseToolboxTalkCommandHandler.cs:77`, `Id = Guid.NewGuid()` — would need a command change to accept an optional pre-generated ID, which is outside this job's file).
- **(b) Upload to scratch as today, then copy to permanent storage after the talk is created, and repoint `SourceFileUrl`/`PdfUrl`.** Fits the current ordering (upload → initialise → parse → quiz) with no reordering and no command signature change: after `InitialiseToolboxTalkCommand` returns `talk.Id` (`:251-259`), copy/re-upload the already-downloaded PDF bytes (`seekableStream` is still in scope) via `UploadPdfAsync(tenantId, talk.Id, title, seekableStream, file.FileName, ct)`, then update `talk.SourceFileUrl`/`talk.PdfUrl` to the new permanent URL via `toolboxTalksDb` (same DbContext already resolved at `:208`, already used to patch `BulkTranslationPendingSince` at `:289-296` — the update could be folded into that same `SaveChangesAsync`). Costs one extra R2 write per item (bytes are already in memory in `seekableStream`, no need to re-download), but requires zero changes to `InitialiseToolboxTalkCommand`'s contract.

**(b) is the cleaner fit given the current code shape** — it keeps `InitialiseToolboxTalkCommand` untouched (that command is shared with the live wizard, per the class doc comment's stated goal of producing "identical" content to a wizard user) and only adds a second small write plus one extra DB patch inside `BulkSopImportJob` itself, which already patches the entity post-creation for `BulkTranslationPendingSince`.

Either option still requires `DeleteSessionFilesAsync` to keep wiping the scratch prefix — that part of the cleanup is correct and should not change; only the PDF's *permanent* home needs to move outside that prefix before cleanup runs.

---

## C. Bug B — Regulatory mapping shows only the first imported learning

### C.1 What actually gates "available for regulatory mapping" — confirmed, current code

The UI surface matching the report ("an option for regulatory mapping") is the **manual-mapping content picker** — `AddMappingDialog.tsx` (`web/src/features/toolbox-talks/components/AddMappingDialog.tsx:43,46-47`), which calls `useContentOptions()` → `GET /api/toolbox-talks/requirement-mappings/content-options` → `RequirementMappingService.GetContentOptionsAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Mapping/RequirementMappingService.cs:473-494`):

```csharp
var talks = await _dbContext.ToolboxTalks
    .Where(t => t.TenantId == tenantId)
    .Where(Domain.Entities.ToolboxTalk.IsLiveExpression)   // :479
    ...
```

`ToolboxTalk.IsLiveExpression` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalk.cs:361-362`):

```csharp
t => t.Status == ToolboxTalkStatus.Published && t.IsActive && !t.IsDeleted;
```

**A talk must be `Published` to appear in the manual-mapping dropdown at all.** (Courses use `ToolboxTalkCourse.IsLiveExpression`, `ToolboxTalkCourse.cs:38-39` — `IsActive && !IsDeleted`, no `Status` concept for courses.)

The other regulatory-mapping-related surface — the **pending mappings review list** (`GetPendingMappingsAsync`, `RequirementMappingService.cs:36-69`) — shows existing `RegulatoryRequirementMapping` rows, not a talk picker. Those rows are only ever created by `RequirementMappingJob`, which is enqueued **exclusively from the publish success path**: `BackgroundJob.Enqueue<RequirementMappingJob>(...)` fires only inside `PublishByTalkId` after `PublishToolboxTalkCommand` succeeds (`ToolboxTalksController.cs:664-687`). It is never enqueued from `BulkSopImportJob` or from talk creation/parsing/quiz-generation.

### C.2 Comparison — bulk import vs. single (wizard) creation, per item

| Signal the Regulatory section needs | Set by bulk import? | Set by normal wizard flow? |
|---|---|---|
| `ToolboxTalk.Status == Published` | **Never.** `InitialiseToolboxTalkCommandHandler.cs:124` always sets `Status = ToolboxTalkStatus.Draft`; nothing later in `BulkSopImportJob` changes it. | Yes — an explicit, separate `POST /{talkId}/publish` action (`ToolboxTalksController.cs:664-687`) the wizard user takes deliberately, one talk at a time. |
| `RegulatoryRequirementMapping` rows (AI-suggested candidates) | **Never created** — `RequirementMappingJob` is only enqueued on publish success (`ToolboxTalksController.cs:686-687`), and bulk import never publishes. | Created automatically, fire-and-forget, right after that same publish call. |
| `IsActive` | Set from tenant default (`InitialiseToolboxTalkCommandHandler.cs:125`, `tenantSettings?.DefaultIsActive ?? true`) — **identical for every item**, no per-item variance found. | Same tenant-default mechanism. |

### C.3 Conclusion — this is not a per-item loop defect; the "first" is whichever one was manually published

Reading the entire `ProcessItemAsync` loop (`BulkSopImportJob.cs:175-316`) found **no branch, shared mutable object, or "set once outside the loop" value that differs between item 1 and items 2..N with respect to anything the Regulatory section reads.** Every created talk gets: a fresh `Guid` (`InitialiseToolboxTalkCommandHandler.cs:77`), its own derived `Title`/`Code`, `Status = Draft` (always, no exception), the same tenant-sourced `IsActive` default, and no `Category`/sector field at all (not present anywhere in the `InitialiseToolboxTalkCommand` call at `BulkSopImportJob.cs:251-259` — `Category` is simply never set, defaulting to `null` for every item uniformly, not just some).

**Since `IsLiveExpression` requires `Status == Published`, and nothing in the bulk-import job ever publishes anything, the structurally correct behavior today is that *zero* bulk-imported talks appear in the manual-mapping dropdown** — not "all but the first." The reported symptom ("only the first is available") is fully consistent with an admin having manually opened and published exactly one of the N drafts from the "Review Draft Learnings" list (`BulkSopImportResultsPanel.tsx:228-232` → drafts list → wizard → `/publish`) while leaving the rest in Draft — which is the *only* way any bulk-imported talk becomes mapping-eligible today, since no bulk-publish action exists (confirmed absent, §A.2). This reframes Bug B from "the import loop sets something only on item 1" to **"the import flow has no publish step and no bulk-publish escape hatch, so every imported learning requires N separate manual publish actions before it is regulatory-mapping-eligible, and only one had been done at the time of the report."**

If the intent is for bulk-imported SOPs to become mapping-eligible without N manual publishes, the gap is a missing capability (auto-publish at the end of the job, or a new bulk-publish action), not a bug fix inside the existing per-item loop — there is nothing in the loop's current logic to "fix" that would make items 2..N behave differently from item 1, because today they don't differ.

---

## D. Other per-item handling defects found while reading the loop

### D.1 Title-derivation collision across ZIP subfolders (confirmed code path, untested)

`DeriveTitle` (`BulkSopImportJob.cs:331-339`) derives the learning title from **`file.FileName`**, which the validator sets to `entry.Name` — the **basename only**, not the full in-archive path (`BulkSopImportValidationService.cs:98`, `FileName = entry.Name` vs. `EntryName = entry.FullName` at `:97`). If a ZIP contains two PDFs with the same filename in different subfolders (e.g. `SiteA/Manual Handling.pdf` and `SiteB/Manual Handling.pdf` — a realistic structure for a multi-site SOP export), both derive the identical title `"Manual Handling"`. The title-uniqueness check (`:210-214`, `t.Title == title`) then treats the second as a duplicate: on a fresh (non-rerun) session it is recorded `Failed: "A learning titled '...' already exists."` (`:235`); on a rerun session it is recorded `AlreadyExisted`, pointing at the **first** file's `ToolboxTalkId` (`:216-233`) even though it is a genuinely different source document that was silently dropped. There is existing test coverage for the *flat* same-title case (`BulkSopImportJobTests.cs:73-138`, one file deliberately colliding with a pre-existing Draft), but no test exercises the nested-subfolder-same-basename case, and `EntryName` (which *is* unique per the zip) is never considered as a tie-breaker or appended to the title.

### D.2 `Category` and audit/reviewer metadata are never set — uniformly absent, not a per-item variance

`InitialiseToolboxTalkCommand` exposes `Category`, `ReviewerName`, `ReviewerOrg`, `ReviewerRole`, `ClientName`, `AuditPurpose`, `Description` (`InitialiseToolboxTalkCommand.cs:16-42`) — none of these are set by `BulkSopImportJob.cs:251-259`, which only populates `TenantId`, `Title`, `InputMode`, `SourceFileUrl`, `SourceFileName`, `SourceFileType`. This is uniform across every item (not a first-vs-rest asymmetry), but worth flagging since `Category` in particular is used elsewhere for filtering/grouping (skills matrix category filter, dashboard groupings per `CLAUDE.md`'s Skills Matrix section) — every bulk-imported talk will be uncategorized until an admin edits it individually.

### D.3 Quiz-generation failure is swallowed as a `Warning`, not surfaced as a distinct review requirement

`:276-284` — if `GenerateToolboxTalkQuizCommand` fails for a given item, the talk is still recorded `Succeeded` with a `Warning` string. This is consistent with the class doc comment's intent (*"the learning was created with sections but no quiz — quiz can be generated manually"*) and is arguably correct by design, but combined with §C — a talk with no quiz can still be published (the publish gate only checks ≥1 section, per the prior design recon `docs/bulk-learning-generation-recon.md:205-213`, re-confirmed by publish-gate structure in `ToolboxTalksController.cs`/`PublishToolboxTalkCommand`) — so a batch could end up with some published talks lacking any quiz, silently, if an admin publishes without opening each draft. Not re-verified against the live `PublishToolboxTalkCommandHandler` in this session; flagged from the prior recon for awareness only.

### D.4 No shared-object/DbContext leakage found

Checked explicitly and ruled out: the `ZipArchive archive` passed into `ProcessItemAsync` is read-only and shared safely (each item calls `archive.GetEntry(file.EntryName)` independently, `:185`); `IJobTenantContextAccessor` is `AddScoped` and freshly resolved per `itemScope` (`:193,205`), not a job-level singleton; `IToolboxTalksDbContext`/`ISender` are resolved fresh per `itemScope` (`:207-208`), so a failed `SaveChangesAsync` on one item cannot leave a poisoned change-tracker for the next (the Note 23 pattern, applied correctly here — matches the assessment already on file in `docs/schedule-job-tenant-context-recon.md:177-193`). The outer `_context` (`ICoreDbContext`, constructor-injected, not per-item) is used **only** for session-level status writes (`:88-91,121-125,152-165`), never for talk data — no cross-item contamination risk there either.

---

## E. Other artifacts created during import, consumed later

- **PDF** — covered in full in §B. The only cross-phase artifact confirmed to be affected by cleanup.
- **Slideshow** — not generated during bulk import at all (no `GenerateSlidesFromPdf` default forced, no slideshow call in the job). If an admin later enables "Generate Slides from PDF" and publishes, `ContentGenerationJob.GenerateSlideshowOnlyAsync` fires on publish (`ToolboxTalksController.cs:692-704`) and would read the by-then-deleted `PdfUrl` — a second, later-triggered symptom of Bug A, not a separate defect.
- **Translations** — explicitly deferred to `BulkLearningTranslationSweepJob` via the `BulkTranslationPendingSince` flag (`:289-296`); this is an intentional, already-designed decoupling (per the class doc comment) and not a defect in this recon's scope. Not investigated further here.
- **ZIP (`upload.zip`)** — correctly scratch; nothing reads it after validation; correctly wiped by the same `DeleteSessionFilesAsync` call that incorrectly also wipes the PDFs.

---

## F. Test coverage — current state and gaps

### F.1 What exists

`tests/QuantumBuild.Tests.Integration/ToolboxTalks/BulkSopImportJobTests.cs` — one test, `RealZip_EndToEnd_CreatesDraftLearningsAndIsolatesPartialFailure` (`:74-167`):
- Real upload → validate → job execution (job invoked directly, not via Hangfire's background server — background processing is disabled in the test host; same pattern as `MissingTranslationsJobTests`) → status polling, using a **3-item** ZIP (2 genuinely distinct PDFs + 1 deliberately colliding with a pre-seeded Draft talk).
- Asserts: session reaches `Completed`, correct succeeded/failed counts, the failed item's reason mentions "already exists", and — for each succeeded item — the resulting `ToolboxTalk` row exists, is `Status.Draft`, has sections and quiz questions, and has `BulkTranslationPendingSince` set.
- **Uses `FakeR2StorageService`** (`tests/QuantumBuild.Tests.Integration/Setup/Fakes/FakeR2StorageService.cs`), whose `UploadSessionFileAsync`/`DeleteSessionFilesAsync` (`:190-223`) faithfully mirror production's shared-prefix behavior (same `{tenantId}/sessions/{sessionId}/` key shape for both), so this fake is **capable of catching Bug A today** — but the existing test never checks file survival after the job completes.

### F.2 Gaps to close before launch

1. **Bug A regression test.** After a successful multi-item run, resolve `IR2StorageService` (`FakeR2StorageService`) in the test and assert the succeeded items' `talk.PdfUrl`/`SourceFileUrl` keys are **still present** in the fake's file store post-completion. With the current code this test would fail today, proving the bug; it should pass once §B.3's fix lands.
2. **Multi-item (3+) test with no collisions**, asserting all items get **distinct** `ToolboxTalkId`s, distinct `Code`s, and independently-derived `Title`s — to catch any "first vs. rest" divergence directly (the existing test's 2 successful items already partially cover this, but a dedicated 5+ item case with no collisions would more clearly isolate ordering-dependent bugs, e.g. if a future change introduces shared/reused state).
3. **Regulatory-mapping availability test** covering §C directly: create N bulk-imported talks (all Draft), assert `GetContentOptionsAsync` / `GET .../content-options` returns **none** of them; publish one, assert exactly that one now appears; publish a second, assert both appear. This turns the "only first" symptom into an explicit, checkable contract instead of an assumption.
4. **Nested-subfolder same-basename ZIP test** (§D.1): a ZIP with two PDFs sharing a basename in different in-archive folders, asserting current behavior (one `Failed`/`AlreadyExisted`) so any future title-derivation change is a deliberate, visible decision rather than a silent behavior change.
5. **Job-level-failure path**: no existing test forces the outer `catch` (`BulkSopImportJob.cs:146-168`) to fire mid-loop (e.g. after 1 of 3 items succeeds) to verify partial results are preserved (`ProcessingResultJson` written with the completed items) and that the ZIP (but not yet-uploaded PDFs from completed items) survives, per the "left for investigation" comment.

All of the above should use the real Hangfire enqueue-and-poll harness pattern already established for `BulkEmployeeImportJob`/`MissingTranslationsJob` in this test suite, per the existing test's own approach.

---

## Summary

- **Bug A (confirmed):** `BulkSopImportJob` uploads each item's PDF to the same R2 scratch prefix (`{tenantId}/sessions/{sessionId}/...`) that the session-level cleanup step wipes wholesale after the loop finishes, deleting every just-created talk's permanent-looking `SourceFileUrl`/`PdfUrl` in the same job run that created them. The single-talk (wizard) path avoids this entirely by uploading to a talk-ID-keyed permanent `pdfs/` location that no cleanup job ever touches. Cleanest fix given current code shape: upload to scratch as today (unchanged ordering, no command-contract change), then re-upload the already-buffered bytes to the permanent `pdfs/` location once the talk ID exists, and repoint `SourceFileUrl`/`PdfUrl` before the session-level cleanup runs.
- **Bug B (root cause identified, not a per-item loop bug):** the manual regulatory-mapping content picker requires `Status == Published`; `BulkSopImportJob` never publishes anything, uniformly, for every item. No divergent per-item state was found between item 1 and items 2..N with respect to anything the Regulatory section reads. The reported "only the first is available" is best explained by only one draft having been manually published so far — there is no bulk-publish action anywhere in the product, so N imported learnings require N separate manual publish actions before any of them become mapping-eligible.
- **Other per-item defects found:** a real (if narrow) title-collision risk from same-basename files in different ZIP subfolders (§D.1); `Category` and audit metadata are silently never set (§D.2, uniform, not first-vs-rest); no shared-state/DbContext leakage found elsewhere in the loop (§D.4, ruled out explicitly).
- **Test gaps:** no test currently asserts PDF survival post-cleanup (would catch Bug A today), no test asserts mapping-dropdown visibility for bulk-imported content (would make Bug B's actual cause explicit), and no test covers the nested-subfolder title-collision case or the job-level-failure partial-preservation path.
