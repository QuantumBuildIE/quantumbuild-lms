# Ingestion Terminal-State Recon (Chunk C scoping)

**Date:** 2026-07-31
**Status:** Read-only recon. Facts only, file:line references. No code changed, no data changed, no fix proposed, no design proposed.

**Purpose:** Chunk C will guarantee `RequirementIngestionJob` always eventually writes a terminal `LastIngestionStatus` (`Failed`) on `RegulatoryDocument`, even when the job dies, hangs, throws unhandled, or the process recycles. This recon establishes the current failure-handling behavior precisely enough to scope that work.

---

## Definitive answers

**(a) Does an unhandled exception in the job leave the document stuck on `Ingesting`?**

**Mostly no, with one narrow but real gap.** `ExecuteAsync`'s entire body (`RequirementIngestionJob.cs:69-225`) is wrapped in a single `try { ... } catch (Exception ex) { ... }` with no exception-type filter — it catches everything, including `OperationCanceledException`/`TaskCanceledException`. The catch block (`RequirementIngestionJob.cs:212-225`) calls `MarkFailedAsync` **only if `document != null`** (`:221-224`). Since `document` is assigned within the first few lines of the try block (`:72-75`, the initial `FirstOrDefaultAsync` load), essentially the entire method body after that point is covered — an exception thrown anywhere from the source-URL validation, the fetch, any of the up-to-8 Claude segment calls, the persistence loop, or `MarkSucceededAsync` itself, all result in `MarkFailedAsync` writing `Failed` before the method returns.

The gap: if the exception occurs **before** `document` is assigned (i.e. inside the `FirstOrDefaultAsync` call at `:72-75` itself — a DB connectivity blip at that exact moment), `document` stays `null`, `MarkFailedAsync` is skipped (`:221` guard fails), and the method just falls through to its end having logged the error only. This is not actually a "stuck at Ingesting from this run" case, though, because `LastIngestionStatus = Ingesting` is only set **after** this load succeeds (`:83`) — so a failure at this exact point leaves the document at whatever status it already had before this attempt (which could itself be a stale `Ingesting` left over from an earlier, differently-broken run — see (b)).

A second, more consequential gap: **`MarkFailedAsync` itself is not defensively wrapped.** If `_dbContext.SaveChangesAsync` inside `MarkFailedAsync` (`RequirementIngestionJob.cs:240`) throws (e.g. DB connection lost at the exact moment of writing the failure), that exception propagates out of the `catch` block with nothing left to catch it — it becomes a genuine unhandled exception from `ExecuteAsync`'s perspective, Hangfire's `AutomaticRetry(Attempts = 1)` sees a real failure, and the document is left at whatever it last successfully committed, i.e. **`Ingesting`** (set at `:83-86` before the long-running work began). This is the one scenario in this job where a real unhandled exception both reaches Hangfire *and* leaves the document stuck. Compare this to the two sibling jobs read for §Existing patterns below, both of which nest a `try/catch` specifically around the failure-status save so a broken DB write can't cascade.

**(b) Does a Hangfire-level failure or a process kill leave it stuck?**

**Hangfire-level failure: essentially cannot happen for this job under ordinary application exceptions, and that itself is the deeper problem.** `RequirementIngestionJob.ExecuteAsync`'s catch block does **not rethrow** — it logs, optionally calls `MarkFailedAsync`, and returns normally (`RequirementIngestionJob.cs:212-225`, no `throw;` anywhere in the block, confirmed by reading the full method). Hangfire's job-state machine only transitions a job to its own internal `Failed` state (and only then applies `[AutomaticRetry(Attempts = 1)]` at `:59`) when the method **throws** all the way out. Because this job swallows every application-level exception, Hangfire will record the job as **Succeeded** in its own tables in the overwhelming majority of failure scenarios — the `[AutomaticRetry(Attempts = 1)]` attribute is effectively dead code for anything short of the `MarkFailedAsync`-itself-throws edge case in (a) or a truly uncatchable failure (below). No `GlobalJobFilters`, `IElectStateFilter`, `IApplyStateFilter`, or any other Hangfire filter/hook exists anywhere in the codebase (`grep` across `src` for these types returns only the base `AddHangfire`/`AddHangfireServer` registration at `Program.cs:160-170`) — so even in the rare case Hangfire *does* record its own Failed state, there is no code path that would propagate that fact to `RegulatoryDocument.LastIngestionStatus`. The two failure records (Hangfire's internal job state, and the app-level `LastIngestionStatus` column) are entirely independent, with no hook connecting them.

**Process kill: yes, stuck indefinitely, with no code-level backstop in this repo.** If the worker process is terminated (OOM kill, container recycle, deploy restart) mid-job, none of `RequirementIngestionJob.cs`'s code runs — no catch block, no `Mark*Async` call. The document is left at whatever was last committed, which is `Ingesting` (`:83-86`, written immediately at job start, well before the multi-minute extraction work). Recovery from this state depends entirely on Hangfire's own server/job-reclaim mechanism (governed by `Hangfire.PostgreSql`'s package-internal defaults — no `options.ServerTimeout`, `SchedulePollingInterval`, or any invisibility/heartbeat override is configured anywhere in this codebase; `Program.cs:160-170` sets only `Queues`). Whether and how quickly an abandoned job is reclaimed and re-run is not verifiable by reading this application's source alone — it is package-default behavior not overridden here. **If that reclaim never happens, or the reclaimed re-run also dies before reaching a `Mark*Async` call, the document has no other path back to a terminal state** — there is no recurring sweep, watchdog, or age-based cleanup anywhere in the codebase that scans for `RegulatoryDocument` rows stuck in `Ingesting` (confirmed: `RegulatoryIngestionStatus.Ingesting` is referenced in exactly one place outside its own enum definition — the single write site at `RequirementIngestionJob.cs:83`; no other file reads or resets it).

On the idempotency question for a Hangfire-triggered re-run: `ExecuteAsync` does not resume from where a prior attempt left off — it reloads the document, resets to `Ingesting` again (`:83`), and redoes the entire fetch + 4-principle extraction from scratch (no partial-progress checkpointing exists). Persistence itself is reasonably re-run-safe: `PersistDraftRequirementsAsync` (`:758-822`) checks extracted titles against existing (including soft-deleted) `RegulatoryRequirement` titles for the same profile (`:764-770, 781-785`) before inserting, so a second full run against the same document would not duplicate rows for identical extracted titles — it would just repeat the (costly) extraction work.

**(c) Do the extraction HTTP calls have a real per-call timeout?**

**Yes.** The `HttpClient` injected into `RequirementIngestionJob` is registered with an explicit hard timeout: `client.Timeout = TimeSpan.FromMinutes(5);` (`ServiceCollectionExtensions.cs:352-357`, comment: *"5 minutes for document fetch + AI extraction"*). `HttpClient.Timeout` governs each individual call to `SendAsync`/`GetAsync` made through that client instance — both the web-page fetch (`_httpClient.GetAsync(sourceUrl, cancellationToken)`, `RequirementIngestionJob.cs:315`) and every Claude segment call (`_httpClient.SendAsync(request, cancellationToken)`, `:689`, invoked up to 8 times per document — 4 principles × up to 2 attempts each, `ExtractPrincipleSegmentAsync`, `:440-506`) are bounded by this same 5-minute ceiling per call, including whatever Polly retries happen underneath it (the Claude retry policy, `ResiliencePolicies.GetClaudePolicy`, `ResiliencePolicies.cs:27-52`, is registered as an inner handler relative to the client's own `Timeout`, so the 5-minute clock covers the retries too, not just the first attempt). A stalled/hung connection on any single call cannot block the job forever — it is capped at 5 minutes, after which `HttpClient` throws a `TaskCanceledException`, which `FetchDocumentTextAsync`'s own catch clause explicitly maps to a fetch-failure code (`:335-339`, `"Timed out fetching URL"`) for the fetch step, or (for a Claude call) propagates up uncaught from `CallClaudeAsync` into `ExecuteAsync`'s outer catch, landing in the (a)-analysis above. Worst case across every principle/retry combination timing out back-to-back, the job could still run for roughly 40 minutes (8 × 5 min) before finally failing — bounded, but not short.

The PDF-extraction path (used when `SourceUrl` ends in `.pdf`, `RequirementIngestionJob.cs:296-309`) delegates to `IPdfExtractionService`, which has its own, separate `HttpClient` registration with `client.Timeout = TimeSpan.FromSeconds(30);` (`ServiceCollectionExtensions.cs:107-110`) and no Polly retry policy attached — also a real, hard per-call ceiling, just a different one (30s vs. 5 min) and via a different client than the one used for the Claude calls.

---

## 1. `ExecuteAsync` structure — try/catch scope and what triggers `MarkFailedAsync`

Traced in full, `RequirementIngestionJob.cs:61-226`:

- `document` is declared `null` at `:67`, immediately before the single `try` at `:69`.
- The try block runs the entire pipeline: document load (`:72-75`), URL re-validation (`:91-95`), fetch (`:98-107`), the 4-principle Claude extraction loop (`:127-152`), persistence (`:196-203`), and the terminal `MarkSucceededAsync` call (`:206`).
- The one and only catch, `:212-225`, is unfiltered `catch (Exception ex)`. It logs (`:214-216`) and, **only if `document != null`**, calls `MarkFailedAsync(document, "unknown", ex.Message, cancellationToken)` (`:221-224`). There is **no `throw;`** anywhere in this block or in `ExecuteAsync` generally — the method returns normally after the catch runs.
- Every explicit `return` inside the try block (`:80, 94, 106, 151, 183, 194`) is preceded by a `Mark*Async` call — the intentional-early-exit paths are all covered. It is only the *unplanned* exception path that has the `document != null` gap described in (a).

## 2. What sets `LastIngestionStatus = Ingesting`, and when

`Ingesting` is set in exactly one place in the whole codebase: `RequirementIngestionJob.cs:83`, **inside the job itself**, immediately after the document is loaded (`:72-75`) and saved at `:86` — this happens once the Hangfire worker has actually picked up and started executing the job.

It is **not** set at enqueue time. `RequirementIngestionService.StartIngestionAsync` (`RequirementIngestionService.cs:38-75`) — the method the controller calls to kick off ingestion — loads the document (`:43-45`), optionally updates `SourceUrl` (`:48-52`), validates the URL (`:60-66`), and calls `BackgroundJob.Enqueue<RequirementIngestionJob>(...)` (`:69-70`). **Nothing in this method touches `LastIngestionStatus`.** So there is a real window — from the moment the API request returns 200 to the moment the Hangfire worker actually dequeues and starts the job — during which the document's status is whatever it was before (typically `Idle`, or a stale terminal/`Ingesting` value from a previous attempt), not `Ingesting`, even though a job is already queued and "about to run." This window's length is bounded only by Hangfire's queue/worker availability, not by anything in this application's code.

## 3. Hangfire config for this job, and whether Hangfire-level failure propagates to the document

- `[AutomaticRetry(Attempts = 1)]` and `[Queue("content-generation")]` on `ExecuteAsync` (`RequirementIngestionJob.cs:59-60`).
- No `[DisableConcurrentExecution]` anywhere on this job (confirmed: `grep` for `DisableConcurrentExecution` across `src` returns zero matches).
- Hangfire server registration, `Program.cs:159-170`:
  ```csharp
  builder.Services.AddHangfire(config => config
      .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
      .UseSimpleAssemblyNameTypeSerializer()
      .UseRecommendedSerializerSettings()
      .UsePostgreSqlStorage(options => options
          .UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

  builder.Services.AddHangfireServer(options =>
  {
      options.Queues = new[] { "default", "content-generation" };
  });
  ```
  Only the queue list is customized — no `WorkerCount`, no `SchedulePollingInterval`, no server/heartbeat timeout override. All reclaim/heartbeat/expiration behavior for abandoned jobs runs on `Hangfire.PostgreSql` package defaults, not something this recon can further verify by reading this application's own source.
- **No global Hangfire filter of any kind exists.** `grep` across `src` for `GlobalJobFilters`, `IElectStateFilter`, `IApplyStateFilter`, and bare `JobFilterAttribute` returns nothing beyond the `AddHangfire`/`AddHangfireServer` calls above. There is no hook anywhere that reads Hangfire's own job-failure state and writes it to `RegulatoryDocument` or anything else.
- As established in (b): because `ExecuteAsync` swallows every exception without rethrowing, Hangfire's own internal Failed-state machine (and by extension `[AutomaticRetry(Attempts = 1)]`) essentially never activates for this job under ordinary application-level failures — the two "failure records" (Hangfire's internal state, and `LastIngestionStatus`) are independent in principle, but in practice Hangfire's side almost never even gets populated.

## 4. Process-recycle / kill case

Covered fully in (b) above. Key facts, restated with citations:
- `Ingesting` is written once, at job start (`RequirementIngestionJob.cs:83-86`), well before the long-running extraction work.
- A process kill mid-job runs none of this job's code — no catch, no terminal write.
- Reclaim depends on `Hangfire.PostgreSql` defaults (no overrides configured, `Program.cs:159-170`).
- Re-runs are not checkpointed (full restart of fetch+extract each time) but are reasonably duplicate-safe at the persistence layer via the title-existence check (`RequirementIngestionJob.cs:764-785`).
- No recurring sweep/watchdog exists anywhere in the codebase that would eventually force a terminal status on a `RegulatoryDocument` stuck in `Ingesting` — see §7 for the closest existing patterns that could be mirrored.

## 5. Timeouts on extraction HTTP calls

Covered fully in (c) above. Summary of the two relevant `HttpClient` registrations:

| Client | Timeout | Retry policy | Registration |
|---|---|---|---|
| `RequirementIngestionJob`'s own client (web-page fetch + all Claude segment calls) | 5 minutes per call | `ResiliencePolicies.GetClaudePolicy` (3 retries, 2s/4s/8s + jitter, wrapped **inside** the 5-min timeout) | `ServiceCollectionExtensions.cs:352-358` |
| `IPdfExtractionService` (used only when `SourceUrl` ends `.pdf`) | 30 seconds per call | none | `ServiceCollectionExtensions.cs:107-110` |

Both are real, hard per-request ceilings — nothing in this job's HTTP path can hang indefinitely on a single call. The exposure is cumulative-across-calls (up to 8 Claude calls × 5 min = up to ~40 min worst case), not per-call indefinite hanging.

## 6. Is `ExecuteAsync`'s `CancellationToken` actually honoured / meaningful?

The token **is** threaded through mechanically — `ExecuteAsync(Guid regulatoryDocumentId, CancellationToken cancellationToken = default)` (`:61-63`) passes `cancellationToken` into every `SaveChangesAsync`, the fetch (`_httpClient.GetAsync(sourceUrl, cancellationToken)`, `:315`), and every Claude call (`_httpClient.SendAsync(request, cancellationToken)`, `:689`) down through `CallClaudeAsync` and `ExtractPrincipleSegmentAsync`.

However, **the token that actually reaches the job at runtime is always `CancellationToken.None`, by construction, and this is a codebase-wide convention, not unique to this job.** The one enqueue call site, `RequirementIngestionService.cs:69-70`:
```csharp
BackgroundJob.Enqueue<RequirementIngestionJob>(
    job => job.ExecuteAsync(regulatoryDocumentId, CancellationToken.None));
```
passes the literal `CancellationToken.None` as the argument value baked into the Hangfire job invocation expression. Hangfire has a distinct, purpose-built cancellation mechanism (`IJobCancellationToken`, tied to job/server shutdown signals) that is **never used anywhere in this codebase** (`grep` for `IJobCancellationToken` across `src` returns zero matches). Every other Hangfire enqueue call site in the codebase follows the identical pattern of passing `CancellationToken.None` literally — `TranslationValidationJob` (`TranslationValidationController.cs:100, 371, 428`; `TranslationJobScheduler.cs:19`), `RequirementMappingJob` (`ContentCreationSessionService.cs:1678, 1776, 2177`; `ToolboxTalksController.cs:687`), and `BulkEmployeeImportJob` (`BulkEmployeeImportController.cs:243`). So for every job in the system, including this one, **a Hangfire server shutdown or timeout has no mechanism to actually cancel an in-flight HTTP call via this token** — the parameter exists and is wired correctly in the sense that it would propagate a real cancellation if one were ever supplied, but none ever is. The only thing that can currently interrupt a hung Claude/fetch call is the 5-minute (or 30-second) `HttpClient.Timeout` from §5, which is independent of this token entirely.

## 7. Existing patterns elsewhere for "always write a terminal status" / per-job timeout that Chunk C could mirror

Three existing jobs were read for comparison; all three differ from `RequirementIngestionJob` in ways directly relevant to scoping the fix:

**`TranslationValidationJob.ExecuteAsync`** (`TranslationValidationJob.cs:96-593`):
- Has a *separate* catch clause for cancellation (`:459-506`, not shown as a distinct `catch` type in the excerpt but structurally isolated from the general exception path) that writes a dedicated `Cancelled` status before rethrowing (`:468, 505`).
- The general `catch (Exception ex)` (`:507-590`) calls `UpdateRunStatusAsync(validationRunId, tenantId, ValidationRunStatus.Failed)` (`:524`) — critically, this helper (`:794-815`) **re-queries the run by ID from scratch** rather than relying on an already-loaded local variable, so it can write `Failed` even if the run object was never successfully loaded earlier in the method. It also wraps its own `SaveChangesAsync` in a nested `try/catch` (`:796-814`) that only logs on failure — exactly the defensive nesting `RequirementIngestionJob.MarkFailedAsync` lacks (see (a)).
- Ends the general catch block with an unconditional `throw;` (`:591`) — so unlike `RequirementIngestionJob`, a genuine application exception here **does** propagate to Hangfire, making `[AutomaticRetry(Attempts = 1)]` (`:94`) actually functional for this job.

**`BulkEmployeeImportJob.ExecuteAsync`** (`BulkEmployeeImportJob.cs:64-215`):
- `catch (Exception ex) when (ex is not OperationCanceledException)` (`:190`) — deliberately excludes cancellation from the Failed-status path (implying cancellation is expected to propagate/be handled differently), unlike `RequirementIngestionJob`'s unfiltered catch.
- Writes `session.Status = BulkImportStatus.Failed` (`:203-204`) and wraps the persisting `SaveChangesAsync` in its own nested `try/catch` (`:206-211`) that only logs on failure if that save itself fails — again, the defensive nesting `RequirementIngestionJob` lacks.
- Ends with an unconditional `throw;` (`:213`) — same as `TranslationValidationJob`, letting Hangfire's own retry mechanism engage.
- Class-level doc comment (`:20-31`) explicitly reasons about `[AutomaticRetry(Attempts = 0)]` (`:33`) being intentionally disabled because a retry would re-run rows that may have already succeeded — i.e. this codebase already has precedent for reasoning explicitly about what retry semantics are safe for a given job's side effects, which is directly relevant to whatever Chunk C decides for `RequirementIngestionJob`'s own currently-inert `Attempts = 1`.
- Also has a distinct, **request-triggered** (not scheduled) staleness-recovery pattern: `BulkEmployeeImportController.cs:205-233`, `Confirm` action — if a session is found in `Processing` for longer than a `StuckProcessingThreshold` (30 minutes, per the class doc at `:201-203`), it resets the session back to `Validated` and re-enqueues, rather than leaving it stuck forever. This is triggered by the user's next `/confirm` call rather than by a background sweep.

**`ExpiredSessionCleanupJob`** (`ExpiredSessionCleanupJob.cs:1-73`):
- A **recurring, scheduled** sweep (per its doc comment, `:11-13`, "Daily Hangfire recurring job") that queries for `ContentCreationSession` rows past their own `ExpiresAt` timestamp and not already in a terminal status (`:24-35`), and force-writes a terminal-ish status (`Abandoned`) on each (`:51`) — with a per-row `try/catch` (`:46-63`) so one row's cleanup failure doesn't block the rest, and defers the actual `SaveChangesAsync` to the very end (`:66`) covering the whole batch at once.
- This is the closest direct structural precedent in the codebase for "a background sweep that finds long-running/abandoned work and forces it to a terminal state" — the same shape (age-based query + forced terminal write + per-item isolation) could plausibly be mirrored against `RegulatoryDocument` rows stuck in `Ingesting` past some age threshold, as a backstop independent of whether the in-job catch/rethrow logic is also hardened.

No mechanism in the codebase currently combines both an in-job hardened catch (nested save-failure protection + rethrow so Hangfire's own retry engages) **and** a scheduled age-based sweep for the *same* entity — `TranslationValidationJob`/`BulkEmployeeImportJob` have the former, `ExpiredSessionCleanupJob` has the latter, but for a different domain. `RequirementIngestionJob` currently has neither in a fully defensive form (its catch is unnested and non-rethrowing) nor a sweep at all.

---

## Non-scope confirmation

No fix was designed or written. No code, configuration, or data was modified. No job was run. This recon does not address extraction quality/faithfulness, frontend status display/polling (already covered by the separate `docs/regulatory-ingestion-flow-recon.md` §C, referenced above for context only), or make a recommendation between the in-job-hardening approach and the scheduled-sweep approach — both are documented as existing, available precedents for Chunk C to choose between or combine.
