# Regulatory Ingestion Flow — Plumbing Recon

**Date:** 2026-07-31
**Status:** Read-only recon. Facts only, with file:line references. No code changed, no data changed, no fix proposed.

**Trigger:** Four symptoms reported against the live (demo) DB:
1. Add Sector → Ingest results in `no_active_profiles` skip.
2. `RegulatoryProfiles` contains only seed rows (`CreatedBy=system`, dated 2026-06-18); zero user-created rows exist anywhere, despite Add Sector appearing to succeed in the UI.
3. Each ingest attempt appears to create a new `RegulatoryDocument` row (three near-identical HIQA rows: v1.1, v1.2, plus an earlier upload).
4. The document detail page shows "Ingesting..." indefinitely after the Hangfire job has already reached a terminal DB state (confirmed: job finished ~3.5 min, UI still spinning 20+ min later).

This document traces each symptom to code on the current `transval` HEAD (top commit `4bbceb8`). Two prior recon docs already exist and were read first for context, then re-verified against current code rather than trusted as-is: `docs/regulatory-profile-creation-recon.md` (2026-07-30, written as *pre-implementation* design recon for the same commit that shipped the Add Sector feature — its claim "no UI/API path does this today" is now stale) and `docs/regulatory-ingestion-recon.md` (2026-07-15, predates the entire `LastIngestionStatus`/`Skipped` status model added on 2026-07-30 — its claims about silent, indistinguishable failures are now stale for the general case, though its file-map of the fetch/extract pipeline is still accurate).

**Relevant recent commits** (`git log --oneline`, newest first):
```
4bbceb8 Report all failed principle segments, not just the first
6ed10c1 Add sector-to-document attachment via RegulatoryProfile creation   <- ships CreateProfile / "Add Sector"
d257361 Make silent regulatory extraction failures loud                    <- ships LastIngestionStatus, Skipped, no_active_profiles
```
Both `6ed10c1` and `d257361` landed the same day (2026-07-30), sequentially, by the same author. This sequencing matters for §E below.

---

## A. Add Sector / Profile creation — does it persist, and against what?

### A.1 — Frontend control: binding, payload, document id

**File:** `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx`

- `documentId` is read once from the route param at the top of the page component: `params.documentId as string` (`:613`). Every child component and hook on the page (`DocumentSectorsCard`, `useIngestionStatus`, `useStartIngestion`, etc.) receives this same value as a prop/argument — there is no second, independently-derived document id anywhere on the page.
- The Add Sector control is `DocumentSectorsCard` (`:508-608`), rendered at `:831-834` with `documentId={documentId}` — the exact route-bound id, not a default or a value sourced from list-page state.
- Sector options come from `useAvailableSectors()` (`:515`, imported from `@/lib/api/admin/use-tenant-sectors`), filtered to exclude sectors already present in `sectorKeys` (`:519-521`).
- `handleAddSector` (`:523-548`) guards on `!selectedSectorId` — the button itself is also `disabled={!selectedSectorId || createProfile.isPending}` (`:595`), so there is no path to submit with an empty sector selection.
- `createProfile.mutate({ sectorId: selectedSectorId }, ...)` (`:525-547`) — `createProfile` is `useCreateRegulatoryProfile(documentId)` (`:516`), bound to the same route-derived `documentId`.
- Mutation fn: `web/src/lib/api/admin/regulatory-ingestion.ts:57-64` — `createRegulatoryProfile(documentId, data)` POSTs to `` `/regulatory/documents/${documentId}/profiles` `` (`:61-63`), i.e. the id is interpolated directly into the URL path from the function argument, which traces back unbroken to `params.documentId`.
- **No point in this chain drops or defaults the document id or the selected sector.** The only "silent" behavior is the pre-submit guard (`!selectedSectorId` → no-op), which is visibly enforced by the disabled button, not a hidden failure.

### A.2 — Backend: does `CreateProfileAsync` actually insert and save?

**Route:** `POST api/regulatory/documents/{documentId:guid}/profiles` — `RegulatoryIngestionController.cs:141-167` (`[HttpPost("documents/{documentId:guid}/profiles")]`, class-level `[Authorize(Policy = "Tenant.Manage")]` at `:17`, same SuperUser-only gate as every other action in this controller).

```csharp
// RegulatoryIngestionController.cs:151-160
try
{
    var result = await _ingestionService.CreateProfileAsync(documentId, request.SectorId, cancellationToken);
    return Ok(result);
}
catch (InvalidOperationException ex) { ... return BadRequest(...); }
catch (Exception ex) { ... return StatusCode(500, ...); }
```

**Service:** `RequirementIngestionService.CreateProfileAsync` — `RequirementIngestionService.cs:555-620` (verified by direct read; commit `6ed10c1` diff also captured in full).

Traced line by line:
- `:557-559` loads the `RegulatoryDocument` by id (with `Include(d => d.RegulatoryBody)`); throws `InvalidOperationException` if not found — no silent fallback.
- `:561-563` loads the `Sector` by id; throws `InvalidOperationException` if not found.
- `:567-571` queries `RegulatoryProfiles.IgnoreQueryFilters()` for an existing `{RegulatoryDocumentId, SectorId}` row (covers the soft-delete-collision edge case documented in the pre-implementation recon, §6 of `regulatory-profile-creation-recon.md`).
- **New-pair branch** (`:588-604`, the relevant path for a document with zero prior profiles): constructs `new Domain.Entities.RegulatoryProfile { RegulatoryDocumentId, SectorId, SectorKey, ScoreLabel, ExportLabel }`, calls `_dbContext.RegulatoryProfiles.Add(profile)` then `await _dbContext.SaveChangesAsync(cancellationToken)` — a plain EF add+save with no swallowed exception, no early return before the save, no feature flag or conditional skipping the write.
- **Existing-live-pair branch** (`:575-578`): throws `InvalidOperationException("A profile already exists for this document and sector...")` — surfaces as `400` to the frontend (caught by `createProfile`'s `onError`, toasted, §A.1), not a false "success".
- **Existing-soft-deleted-pair branch** (`:580-586`): restores the row (`existing.IsDeleted = false`) and saves — also a real, verifiable write.
- Return value in every success branch is a `RegulatoryProfileDto` built from the now-persisted `profile` object's own properties (`:606-617`), not a static/cached echo of the request.

**No swallowed exception, unpersisted context, or validation early-return-with-success exists in this method.** A `DbUpdateException` from `SaveChangesAsync` (e.g. an FK violation) is not caught by the `InvalidOperationException` clause in the controller and would fall to the generic `catch (Exception ex)` → `500` (`RegulatoryIngestionController.cs:162-166`) — which the frontend surfaces as an error toast (`page.tsx:532-545`), not a success toast. This matters directly for symptom 2 (§E below): **the frontend can only show "Sector attached" (`page.tsx:529`) if the mutation's promise resolved without throwing, which only happens on HTTP 200, which — per this trace — only happens after `SaveChangesAsync` has already completed without error.**

**Integration test confirms persistence against a real (test) DB, not just in-memory correctness:** `tests/QuantumBuild.Tests.Integration/ToolboxTalks/RegulatoryProfileCreateTests.cs:68-95`, `CreateProfile_ValidDocumentAndSector_Succeeds` — POSTs, asserts `200`, then opens a **second, fresh `DbContext`** (`:90`, `GetDbContext()`) and reads the row back (`:91-94`) to prove the write survived past the original context, not merely that the in-memory tracked entity looks right. A second test (`:97-109+`, `CreateProfile_DuplicateLivePair_Returns400_DoesNotCreateSecondRow`) pins the duplicate-rejection path.

**Conclusion for A.2: Add Sector does persist a `RegulatoryProfile` row, against the exact document id bound to the page the user is viewing, as written in the current repository code.** This is a code-level conclusion — see §E for why this appears to conflict with the observed zero-rows-in-the-live-DB fact, which is a deployment question, not a code defect found in this trace.

### A.3 — ScoreLabel/ExportLabel: identical formula, or drifted?

**Seed data** (`RegulatoryProfileSeedData.cs:191-196`), for HIQA/homecare:
```
ScoreLabel:  "HIQA Regulatory Score"
ExportLabel: "HIQA Inspection Export"
```
(body code `"HIQA"` established at `RegulatoryProfileSeedData.cs:69`).

**CreateProfileAsync** (`RequirementIngestionService.cs:592-593`):
```csharp
ScoreLabel = $"{document.RegulatoryBody.Code} Regulatory Score",
ExportLabel = $"{document.RegulatoryBody.Code} Inspection Export",
```
For a HIQA document this evaluates to `"HIQA Regulatory Score"` / `"HIQA Inspection Export"` — **character-for-character identical** to the seeded string. Confirmed further by the integration test's own assertion: `RegulatoryProfileCreateTests.cs:86-87` — `dto.ScoreLabel.Should().Be($"{body.Code} Regulatory Score")`. **No drift for these two fields.**

**Fields that do differ from the seeder** (not asked about directly, but a hard fact worth recording since it bears on whether a create-path profile is a full functional equivalent of a seeded one):
- `Description` — seeder hand-authors a real sentence per sector (e.g. `"Safeguarding, medication, mandatory reporting, EVV compliance"`, `RegulatoryProfileSeedData.cs:195`); `CreateProfileAsync` never sets `Description`, so it lands as the C# default `string.Empty` (`RegulatoryProfile.cs:38` — property default). This satisfies the EF `IsRequired()` constraint (empty string ≠ null) so it does not block the insert, per the existing recon's note on this same non-null-vs-non-empty distinction (`regulatory-profile-creation-recon.md:164`).
- `CategoryWeightsJson` — seeder hand-authors a real weighted-category JSON array (`RegulatoryProfileSeedData.cs:196`); `CreateProfileAsync` never sets it either, so it lands as the entity default `"[]"` (`RegulatoryProfile.cs:39`).
- Neither omission blocks `RequirementIngestionJob`'s active-profile check (§B below), which only reads `p.IsActive` (`RequirementIngestionJob.cs:172`) — a create-path profile is `IsActive = true` by entity default and is fully eligible for ingestion despite the blank `Description`/`CategoryWeightsJson`. This would only matter to `RegulatoryScoreService` (not investigated — explicitly non-scope, extraction/scoring quality).

---

## B. Document creation — why a new row per attempt?

### B.4 — Is duplicate-document creation a code behavior or a UI-flow behavior?

**`CreateDocumentAsync`** (`RequirementIngestionService.cs:497-553`) validates `Title` (required, ≤500 chars, `:501-505`), `Version` (required, ≤50 chars, `:507-511`), that the `RegulatoryBodyId` resolves (`:513-515`), and the `SourceUrl` shape if provided (`:517-521`) — **there is no uniqueness or duplicate-title check anywhere in this method**, unlike the seeder's own idempotency guard for the same conceptual operation (`RegulatoryProfileSeedData.cs:143`: `if (existingDocTitles.Any(d => d.RegulatoryBodyId == bodyId && d.Title == title)) continue;`). Every call unconditionally inserts a new row (`:531-532`).

**The dialog that calls it**, `web/src/components/admin/create-regulatory-document-dialog.tsx`, is a plain create form (`title`, `version`, `regulatoryBodyId`, optional `sourceUrl`) with Zod validation (`:52-63`) covering only field shape (non-blank, length, URL format) — **no "does this document already exist?" lookup step, no autocomplete against existing titles, nothing that would surface "you already have a document called this."** On success it calls `onCreated(created.id)` (`:109`).

**Where `onCreated` routes:** the list page, `web/src/app/(authenticated)/admin/regulatory/system/page.tsx:170-172` — `onCreated={(documentId) => router.push(`/admin/regulatory/system/${documentId}`)}` — navigates straight to the new document's detail page. **The only way back to a previously created document is the "Manage" link on the list page's Documents table** (`system/page.tsx:230-234`, confirmed by the prior recon and unchanged), which routes to `/admin/regulatory/system/${doc.id}` — i.e. the same detail-page route, keyed by the document's actual id.

**Conclusion for B.4: creating a new document is entirely a user-flow action, not something any code path performs automatically.** The system has one create button, no re-run-safe upsert, and no in-dialog signal that a similarly-titled document already exists. A user who cannot tell whether their previous attempt "worked" (§A/§C/§E) has no system-provided way to distinguish "click Manage on my existing document" from "click Add Document again" — both are equally available actions, and only the former returns them to the row they already touched.

### B.5 — Do the near-duplicate documents link to each other?

`RegulatoryDocument` (`RegulatoryDocument.cs:10-56`) has no self-referencing field, no `SupersedesDocumentId`/`PreviousVersionId`/similar, and no unique constraint on `{RegulatoryBodyId, Title}` or `{RegulatoryBodyId, Title, Version}` at the EF-configuration or migration level (not present in `RequirementIngestionService.cs`'s validation, and no such index was found when reading the entity). **Each created document is a fully independent, unlinked row.** The only thing tying the three reported HIQA rows together conceptually is a human reading the `Title`/`Version` text — the system has no representation of "these are the same document, evolving."

---

## C. Ingest status display — why perpetual "Ingesting..."?

### C.6 — How status is determined and displayed; why a terminal DB state doesn't reach the screen

**Backend status source of truth:** `RegulatoryDocument.LastIngestionStatus` (`RegulatoryDocument.cs:37`), a `RegulatoryIngestionStatus` enum (`RegulatoryIngestionStatus.cs:8-40`): `Idle=1, Ingesting=2, Success=3, Failed=4, Skipped=5`. Serialized to the frontend as a plain string via `.ToString()` in `BuildIngestionSessionDto` (`RequirementIngestionService.cs:649`: `Status = document.LastIngestionStatus.ToString()`) — so the wire value for a skipped run is literally the string `"Skipped"`.

**Job lifecycle writes to this field at exactly three points**, all confirmed by direct read of `RequirementIngestionJob.cs`:
1. **Start:** `:83` — `document.LastIngestionStatus = RegulatoryIngestionStatus.Ingesting;` then `SaveChangesAsync` (`:86`) — this happens immediately, before the (potentially multi-minute) extraction work.
2. **Terminal — Skipped:** `MarkSkippedAsync` (`:264-279`) sets `LastIngestedAt = DateTimeOffset.UtcNow`, `LastIngestionStatus = Skipped`, error code/message, then `SaveChangesAsync` (`:274`). Called from the no-active-profiles branch at `:178-183`, which is reached only **after** the full per-principle Claude extraction loop has already run (`:127-152`) — i.e. the "no active profile" check is the last gate before persistence, not the first thing the job does. This is why the job can legitimately take ~3.5 minutes before reaching this terminal write: it pays for the full multi-call extraction (segmented per regulatory "principle", `:113-143`) before ever checking whether there's anywhere to persist the results.
3. **Terminal — Failed / Success:** analogous `MarkFailedAsync` (`:231-245`) / `MarkSucceededAsync` (`:250-257`), same pattern — both `SaveChangesAsync` before returning.

**All three terminal writes complete and commit well inside the job's own runtime.** There is no code path in `RequirementIngestionJob.ExecuteAsync` that leaves `LastIngestionStatus` stuck at `Ingesting` — every `return` in the method is preceded by one of the three `Mark*Async` calls (confirmed by reading the full method body). **The DB-level "stuck spinning" is not a backend bug; the backend correctly reaches `Skipped` and stops.**

**Frontend polling/display mechanism** — `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx`:

- Two hooks share the identical TanStack Query key `regulatoryKeys.ingestionStatus(documentId)` (`use-regulatory-ingestion.ts:31-32`):
  - `useIngestionStatus(documentId, enabled)` (`:48-55`) — `refetchInterval: false`, `enabled: !!documentId && enabled`. Used at `page.tsx:619-622` as `useIngestionStatus(documentId, !isPolling)` — i.e. **enabled only while NOT polling**, and even when enabled, never auto-refetches on a timer.
  - `useIngestionStatusPolling(documentId, isPolling)` (`:57-67`) — `refetchInterval: isPolling ? 3000 : false`, `enabled: !!documentId && isPolling`. Used at `page.tsx:624-627`. **This is the only one of the two that ever refetches on its own**, and only while `isPolling === true`.
  - `currentStatus = isPolling ? pollingStatus : status` (`page.tsx:629`).
- **`isPolling` is set to `true`** in `handleStartIngestion`'s `onSuccess` (`:649-655`), alongside a hard-coded safety net: `setTimeout(() => setIsPolling(false), 120000)` (`:654`) — **anchored to the moment the "start ingestion" POST resolves, not to the job's actual completion.**
- **The only other place `isPolling` is set to `false`** is a `useEffect` (`:665-670`):
  ```tsx
  useEffect(() => {
    if (!isPolling) return;
    if (currentStatus?.status === "Success" || currentStatus?.status === "Failed") {
      setIsPolling(false);
    }
  }, [isPolling, currentStatus?.status]);
  ```
  **This checks for exactly two of the three terminal status strings — `"Success"` and `"Failed"` — and omits `"Skipped"`.** A run that ends in `Skipped` never triggers this early-stop path; polling can only be turned off for a Skipped run by the 120-second `setTimeout`.
- **`StatusDisplay`** (`:135-167`) renders the spinner whenever `status === "Ingesting" || isPolling` (`:158`) — note this is an **OR**, so the spinner shows whenever the local `isPolling` flag is true **regardless of what the actual backend status string is**.

**Putting the timing together for the reported case (job runtime ~3.5 min = 210s, safety net = 120s):**
1. `t=0`: ingestion starts, `isPolling=true`, both the 3s-interval polling query and the 120s timeout are armed.
2. `t=3,6,9...117s`: polling query refetches every 3s; backend status is still `"Ingesting"` (job hasn't finished — extraction is still running per-principle Claude calls, `:127-143`).
3. `t=120s`: the safety-net `setTimeout` fires — `setIsPolling(false)`. At this exact moment the job is **still running** (won't reach `MarkSkippedAsync` until ~t=210s). The last value written into the shared query-cache entry (same key for both hooks, so no separate "stale" copy) is `"Ingesting"`.
4. From `t=120s` onward: `isPolling=false`, so the polling hook (`refetchInterval: 3000`) stops firing, and control passes to `useIngestionStatus(documentId, true)` — but that hook has `refetchInterval: false` and nothing else on the page triggers a manual refetch or cache invalidation for this query key after this point (no `setInterval`, no SignalR subscription — confirmed no SignalR/real-time hub exists anywhere in the regulatory ingestion feature, unlike the unrelated TransVal module's `TranslationValidationHub`; grep for a hub/SignalR reference in the regulatory ingestion files returns nothing).
5. `t=210s`: the job actually reaches `MarkSkippedAsync` and writes `Skipped` to the DB — but nothing on the already-rendered page is listening for that write. The cached query value from step 3 (`"Ingesting"`) is now permanently stale.
6. Because `isPolling` is `false` but the last-known `status` is still `"Ingesting"`, `StatusDisplay`'s condition `status === "Ingesting" || isPolling` (`:158`) still matches on `status === "Ingesting"` alone — **the spinner keeps rendering indefinitely**, sourced from a query that will never fetch again on its own.

**What would have to change for the UI to reflect terminal state (current-wiring facts only):** as currently wired, only two things cause this query key to refetch after the 120s mark — (a) a full page reload/remount (fresh `useIngestionStatus` mount fetches once, would show the true terminal status if reloaded after `t=210s`), or (b) a mutation elsewhere on the page whose `onSuccess` explicitly calls `queryClient.invalidateQueries({ queryKey: regulatoryKeys.ingestionStatus(documentId) })` — which several other mutations already do (`useApproveRequirement`/`useRejectRequirement`/`useApproveAllDrafts`/`useCreateRegulatoryProfile`, all in `use-regulatory-ingestion.ts`) but none of which the user necessarily triggers while just watching the ingest button. Neither exists automatically once the 120s timeout has fired and the job is still in flight.

**Two independent, compounding wiring facts produce this symptom:**
- (i) `Skipped` is absent from the early-stop terminal-status check (`page.tsx:667`), so a Skipped run can never stop polling on its own merit — only the fixed timeout can.
- (ii) The fixed timeout (`120000` ms) is anchored to job-start, not job-completion, and this specific job's real runtime (~3.5 min / 210s, driven by the per-principle Claude extraction loop) regularly exceeds it — so the timeout doesn't just "miss Skipped," it fires while **any** long-running job (Success or Failed too, not just Skipped) is still mid-run, landing the page on a frozen, non-refetching query showing a stale `"Ingesting"` value regardless of what status the job eventually reaches.

---

## D. Interaction / sequencing

### D.7 — Causal map: which symptoms are independent, which cause which

**Symptom 1 (`no_active_profiles` skip)** is the backend correctly reporting a true fact about the document it was given: at job-start time (`RequirementIngestionJob.cs:72-75`, the one query that loads `document.Profiles`), that specific `RegulatoryDocumentId` had zero active `RegulatoryProfile` rows. This is not itself a bug — it is `RequirementIngestionJob` behaving exactly as designed since `d257361` (§B/§C of `docs/regulatory-extraction-rebuild-recon.md`, independently confirmed here at `RequirementIngestionJob.cs:172-184`). It is a **downstream consequence** of symptom 2.

**Symptom 2 (zero user-created `RegulatoryProfile` rows in the live DB)** is the crux. Per the trace in §A.2, `CreateProfileAsync` as written in the current repository — reachable via the exact endpoint the frontend calls, bound to the exact document id the user is viewing — performs a normal, non-conditional EF `Add` + `SaveChangesAsync`, has no code path that returns HTTP 200 without that save having already succeeded, and this exact behavior is pinned by an integration test that reads the row back from a fresh `DbContext`. **This recon cannot find a code-level defect that would produce "toast says success, but zero rows exist."** Given the stated fact that the observed toast said success (or at least "appeared to succeed") and yet zero non-seed rows exist anywhere in the table (not attached to the "wrong" document either — the report is *zero* user-created rows, full stop), the internally consistent explanation is that **the code path traced in §A never actually ran against the environment where the symptom was observed** — i.e., a deployment/version gap, not an application-logic bug. Supporting circumstantial evidence, not independently re-verified in this recon:
  - `CLAUDE.md`'s own documented, standing known issue: *"Railway auto-deploy does not always trigger on push"* (Known Issues §2), with an established manual workaround (empty commit) — this is a pre-existing, acknowledged gap between "pushed to `transval`" and "live on whichever Railway service is being tested," independent of anything in this recon.
  - The BACKLOG's Demo-instance entry states the decoupled demo Railway instance is currently disconnected/blocked pending a separate CLI/account-scoping fix, and is not on an auto-deploy cadence from `transval` the way Development is.
  - `6ed10c1` (ships `CreateProfileAsync`/"Add Sector") and `d257361` (ships the `Skipped`/`no_active_profiles` status model that symptom 1's exact error code depends on) are sequential commits from the same day. **The fact that the reported symptom text uses the precise post-`d257361` vocabulary (`no_active_profiles`) means whatever environment was tested has at least `d257361`.** Whether it also has `6ed10c1` (the very next commit, containing the Add Sector feature itself) is exactly the open question this recon cannot resolve from source code alone — it requires checking which commit SHA is actually deployed to the environment where the symptom was observed, which is outside this recon's read-only, code-only scope.
  - This recon cannot rule out an application-level cause with certainty using code inspection alone (e.g., a data-layer difference specific to the live DB that doesn't exist in the test DB) — but no such cause was found despite reading `CreateProfileAsync` fully, its controller wrapper, and its dedicated test suite. The deployment-gap explanation is the only one that reconciles *all* of: (a) a clean, tested, exception-free code path in the repo, (b) a UI success toast (which per §A.2 cannot fire without a 200 response), and (c) truly zero rows, on the document actually being worked on or on any other.

**Symptom 3 (accumulating near-duplicate documents)** is a **consequence of symptom 2, not an independent defect.** Per §B, document creation has no automatic-duplication code path — every new row traces to a deliberate "Add Document" submission. A user who attaches a sector, sees a success toast, but then finds ingestion still fails with "no active profiles" (because, per symptom 2's likely cause, the attach never actually reached the live DB) has no diagnostic information distinguishing "the sector attach silently didn't work" from "I must have created the document wrong — let me start over." The system offers exactly one recovery affordance from the create dialog (create a new document, §B.4) and no signal to prefer "go find my existing document and retry Add Sector on it." Each retry-by-recreating attempt is a rational-looking response to an opaque failure, and produces one more independent, unlinked row (§B.5).

**Symptom 4 (perpetual "Ingesting...")** is **fully independent of symptoms 1-3** — it is a frontend polling/state-management defect (§C) that would reproduce for *any* sufficiently long-running ingestion job regardless of its eventual outcome (Success, Failed, or Skipped all take the same ~3.5-minute extraction path before reaching their respective terminal write, per `RequirementIngestionJob.cs:113-184`). It happens to have been observed on a run that *also* skipped due to symptom 1/2, but the UI-freeze mechanism itself has nothing to do with which terminal status was reached — it is purely a race between the 120-second client-side timeout and the job's actual runtime, compounded by `Skipped` being missing from the terminal-status check. **This symptom would need to be fixed independently of whatever resolves symptoms 1-3**, and fixing symptoms 1-3 would not incidentally fix it (a `Success` outcome reached after 120s would freeze the UI on a stale `"Ingesting"` display exactly the same way).

### Suggested sequencing (a fact about dependency order, not a design proposal)

Because symptom 3 is a *behavioral consequence* of users being unable to trust the outcome of symptom 2, and symptom 1 is a *downstream readout* of symptom 2, the only two independent root causes surfaced by this recon are:
- **Symptom 2's root cause** (most likely a deployment/version gap per the evidence above, not a code defect in the traced path) — resolving this would, by the causal chain above, also resolve symptom 1 (a document that actually has an attached profile will not hit `no_active_profiles`) and remove the *proximate trigger* for symptom 3 (though it would not retroactively fix or link the already-created duplicate rows — see §B.5, no linkage field exists).
- **Symptom 4's root cause** (`page.tsx:665-670` missing `"Skipped"`, and the `:654` timeout being anchored to start-time rather than completion) — a self-contained frontend defect, unrelated to whether symptom 2 is ever fixed.

---

## Non-scope confirmation

No fix was designed or written. No `RegulatoryProfile`, `RegulatoryDocument`, or any other row was created, modified, or deleted. The extraction count/quality question (48-vs-41 requirements) was not investigated — that is explicitly out of scope per the request and belongs to a separate recon. The learning-generation pipeline was not touched or read.
