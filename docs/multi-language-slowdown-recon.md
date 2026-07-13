# Multi-Language Translation Slowdown — Recon

**Status:** Read-only investigation. No code changed. This document reports findings only; it does not prescribe a fix.

---

## Headline

**The backend does not reproduce the reported 7x/23-minute slowdown.** Direct database and Hangfire job-history inspection for the exact reference talk (`0385399c-b5c0-4928-b809-1b6aa4f4598c`) shows its 2-language "Start All" run actually completed in **~3 minutes 13 seconds total** — essentially matching, not exceeding, the ~3m12s single-language baseline. Both language jobs were dequeued within milliseconds of being enqueued (no worker starvation) and ran genuinely concurrently.

The most likely explanation for the *observed* symptom (UI never showing completion within 23+ minutes) is a **frontend state-tracking gap** in the new wizard's Translate step: the mechanism that would tell the UI "this run finished" depends entirely on a SignalR subscription that is only mounted once React Query's `workflow-state` data already reflects an active run, with **no fallback polling** if that chain misses a beat. This is architecturally fragile — a genuine bug candidate — separate from any backend performance problem.

Separately, a **real latent backend risk exists but wasn't caught in the act**: there is no concurrency throttling anywhere on the Claude/DeepL/Gemini HTTP clients used for translation and back-translation, and multiple unrelated AI-heavy jobs (content generation, quiz generation, regulatory scoring, etc.) share the same Anthropic API key/rate limit. Two languages happened to run cleanly this time; that doesn't mean provider-side throttling can never compound under heavier concurrent load (3+ languages, or other AI jobs running at the same time).

**Fix size estimate for the top candidate (frontend tracking gap):** Small — investigating and hardening `useWorkflowSubscription`/`WorkflowSubscriber` state transitions is likely a few hours of focused frontend work, not a redesign. **Fix size for the secondary/latent candidate (no provider-side throttling):** Small-to-Medium — adding a `SemaphoreSlim`-based concurrency cap per provider is a contained, well-understood change, but sizing it correctly needs a decision on acceptable AI-call concurrency across the whole app, not just this job.

---

## Part 1 — Translation orchestration (frontend)

Two wizard implementations exist (per CLAUDE.md Note 29 — wizard cutover); the observed manual run used the **new wizard**, since only it has a "Start All" affordance.

### New wizard (`/admin/toolbox-talks/learnings/[talkId]/translate`)

- Step component: `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx`
- Row UI: `web/src/features/toolbox-talks/components/learning-wizard/components/WizardTranslationPanel.tsx`

Per-language **Start** (`TranslateStep.tsx:74-85`): one `useStartTalkTranslation()` mutation → `POST /toolbox-talks/{talkId}/translations/{languageCode}/start-translation` with `{ confirmOverwrite }`. One call per language; language is a single value.

**Start All** (`TranslateStep.tsx:87-101`):
```js
const handleStartAll = async () => {
  setIsStartingAll(true);
  try {
    const startable = languages.filter((code) => canStart(stateByCode[code]?.state));
    for (const code of startable) {
      const current = stateByCode[code]?.state;
      const confirmOverwrite = current === 'Stale';
      startTranslation({ talkId, languageCode: code, confirmOverwrite });
      // Stagger initiation to reduce API rate pressure
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
  } finally {
    setIsStartingAll(false);
  }
};
```
This is a **client-side loop with a 1-second `setTimeout` stagger between dispatches**, explicitly commented as intentional ("reduce API rate pressure"). The `startTranslation` mutate calls are fire-and-forget (not awaited to completion) — only the 1s delay is awaited. For 2 languages this adds ~1 second of total client-side overhead, nowhere near enough to explain a 20+ minute gap. Both requests reach the server roughly 1 second apart and their backend work proceeds independently from that point.

Each single-language endpoint call does **both translation and validation** — confirmed by the controller comment at `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs:1577-1581`: "start a translation + validation run for a single language... enqueues `TranslationValidationJob` which generates the translation then validates it inline." There is no separate "start validation" step in this wizard.

### Legacy wizard (`create-wizard`)

Has no "Start All" button — `SettingsStep.tsx:172-213` fires a **single** `POST /toolbox-talks/create/session/{sessionId}/translate-validate` with `{ targetLanguageCodes: string[] }` (array, all languages, one call). Fan-out for N languages happens entirely server-side. Not the path exercised by the observed test (no "Start All" exists here), but relevant to Part 2 below since its server-side handler is the one CLAUDE.md documents as `ContentCreationSessionService.StartTranslateValidateAsync`.

**Conclusion for Part 1:** the frontend request-dispatch mechanism cannot explain more than ~1 second of overhead for "Start All" with 2 languages. Whatever is happening, it isn't in the click-to-request path.

---

## Part 2 — Backend enqueue path & Hangfire configuration

### Enqueue path

`ContentCreationSessionService.StartTranslateValidateAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs:392`) — this is the **legacy wizard's** session-level entry point. For N languages it creates N `TranslationValidationRun` rows (lines 666-688) then N separate `BackgroundJob.Enqueue<TranslationValidationJob>` calls (lines 704-710) — **2N behavior**, not one job looping over languages.

The **new wizard's** per-language `start-translation` endpoint (used in the observed test) follows the same fundamental shape: one `TranslationValidationRun` row + one `Enqueue<TranslationValidationJob>` call per language, just triggered by 2 separate HTTP requests instead of one session-level request internally looping.

`TranslationValidationJob.ExecuteAsync` signature (`.../Jobs/TranslationValidationJob.cs:96-100`) takes a single `validationRunId` (language is baked into `run.LanguageCode`) plus an optional section-index filter — no multi-language batching inside one job execution.

**Net effect: "Start All" with 2 languages produces 2 independent Hangfire job invocations**, each running the full per-language pipeline (forward-translate everything, then validate every section).

### Hangfire configuration

`src/QuantumBuild.API/Program.cs:152-163`:
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
- **No `WorkerCount` override** → Hangfire default of `Environment.ProcessorCount * 5`. This dev machine reports **16 logical processors → WorkerCount = 80**. Plenty of headroom for 2 concurrent jobs.
- Only two queues (`default`, `content-generation`); every heavy AI job (`TranslationValidationJob`, `MissingTranslationsJob`, `ContentGenerationJob`, `ContentCreationParseJob`, `RequirementIngestionJob`, `RequirementMappingJob`, `CorpusRunJob`, `ValidationReportJob`) shares the single `content-generation` queue.
- **No `[DisableConcurrentExecution]`** on any of these jobs, no `SemaphoreSlim`, no distributed lock — nothing serializes execution, per-talk or globally.
- No Hangfire settings exist in `appsettings.json`/`appsettings.Development.json` at all (verified by grep); there's no `appsettings.Production.json` in the repo, so this configuration is identical everywhere it's deployed **as checked into source** — though Railway's actual container CPU allocation (and therefore effective `WorkerCount`) is an environment variable outside this repo and wasn't verified here. A 1-2 vCPU Railway container would give WorkerCount 5-10 rather than 80 — still likely enough for 2 concurrent jobs, but the safety margin is much thinner than on this dev machine.

### Confirmed from live data (Part 6 detail, summarized here for context)

Hangfire's own job/state tables show, for the two jobs matching the reference talk's runs:

| Job | Enqueued | Processing started | Succeeded | Queue wait |
|---|---|---|---|---|
| 231 (pt) | 14:50:30.504 | 14:50:30.546 | 14:53:25.632 | ~42ms |
| 232 (af) | 14:50:31.241 | 14:50:31.258 | 14:53:44.372 | ~17ms |

Both dequeued essentially instantly. **Worker starvation is not what happened in this run.** On this machine (WorkerCount=80), there was no meaningful pool to starve.

---

## Part 3 — External API call profile

All external HTTP calls happen inside `ConsensusEngine.RunAsync`, invoked from `TranslationValidationService.ValidateSectionAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/TranslationValidationService.cs`). Safety classification, glossary replacement/verification, artefact scanning, and registry scanning are all local (regex/DB-lookup), not HTTP.

`ConsensusEngine.cs` rounds — all **strictly sequential awaits**, never `Task.WhenAll`, even within a round:
- Round 1 (lines 64-68): Claude Haiku back-translate, then DeepL back-translate — sequential.
- Round 2 (88-117, only if Round 1 inconclusive): + Gemini — sequential addition.
- Round 3 (120-140, only if still inconclusive): + Claude Sonnet (replaced DeepSeek per CLAUDE.md Note 2) — sequential addition.

Per section: 2 calls (best case, Round 1 passes) to 4 calls (worst case, all 3 rounds run). `TranslationValidationJob.ExecuteAsync`'s section loop (lines 214-227) is a plain sequential `for` — **no parallelism across sections within one job either.**

Separately, **before** validation starts, `GenerateTranslationForSectionsAsync` (lines 939-1253) forward-translates everything for that language: title + content per section (2 sequential Claude calls each — confirmed directly at lines 1040-1058: `TranslateTextAsync` for title, then again for content, inside a `foreach (var section in originalSections)` loop), plus quiz question/option translation. For the reference talk's 5 sections this alone is at least 10 sequential Claude calls, before any back-translation begins.

**Resilience policies** (`src/Core/QuantumBuild.Core.Application/Http/ResiliencePolicies.cs`): `GetClaudePolicy` (3 retries, 2s/4s/8s backoff, on 429/5xx/529 — up to ~14s extra per call) is wired to both `ClaudeHaikuBackTranslationService` and `ClaudeSonnetBackTranslationService`; `GetTransientPolicy` (3 retries, 1s/2s/4s) to DeepL and Gemini. Critically, `PreFlightScanService`, `DialectDetectionService`, `RegulatoryScoreService`, `ContentTranslationService`/`AiSectionGenerationService`/`AiQuizGenerationService` **all share the same Anthropic API key/rate limit** as the back-translation services — every Claude-based feature in the app competes for the same upstream quota.

**No in-process throttling exists.** A grep for `SemaphoreSlim|RateLimit|Throttle|MaxDegreeOfParallelism` across the ToolboxTalks Infrastructure layer returns zero matches. Nothing prevents N concurrent language jobs — or N concurrent *anything* using Claude — from hammering the same provider simultaneously; if a provider starts 429-ing under combined load, Polly's exponential backoff would compound stall time with no backpressure/queueing to smooth it out. **This is a real, evidenced architectural gap** — it just isn't what visibly triggered slowness in the one run captured in Part 6.

---

## Part 4 — Database contention

Nearly every write in `TranslationValidationJob`/`TranslationValidationService` is scoped by `ValidationRunId` or `{ToolboxTalkId, LanguageCode}`, giving each language's run independent rows:

- `TranslationValidationRuns` status/progress writes — own row per run (own `Id`).
- `TranslationValidationResults` — upsert keyed `{ValidationRunId, SectionIndex}` — independent per run.
- `TranslationFlags` — insert-only, scoped by `{ToolboxTalkId, LanguageCode}`.
- `ToolboxTalkTranslations` — **one row per `(ToolboxTalkId, LanguageCode)`**, confirmed no shared JSON blob keyed internally by language — rules out the suspected read-modify-write clobber scenario entirely.
- Translation workflow state (`TranslationWorkflowService`) is event-sourced (`AddEvent`, append-only, keyed by `(talkId, languageCode)`), so no shared mutable row to race on there either.

**One genuine but benign same-row race:** `ContentCreationSessions.Status`/`ValidationRunIds` — `TryUpdateSessionStatusAsync` (`TranslationValidationJob.cs:1315-1378`) loads the same session row for every language's job and, once all runs are terminal, writes `Status = Validated`. No `RowVersion`/concurrency token exists on `ContentCreationSession`, so two language jobs can both write the same target value near-simultaneously — idempotent, not corrupting, but a real (harmless) race. This only applies to the **legacy** session-based flow; the new wizard's per-language runs (`IsNewWizard = true`) explicitly skip session-relevance checks (lines 954-998).

**No explicit locking anywhere** — no `RowVersion` on `ToolboxTalk`/`TranslationValidationRun`, no `FOR UPDATE`, no `[DisableConcurrentExecution]`.

**DbContext lifetime note (tangential, not a contention finding):** `TranslationValidationJob` reuses a single injected `IToolboxTalksDbContext` across its entire section loop rather than a fresh scope per section (per CLAUDE.md Note 23's prescribed pattern, which only `BulkEmployeeImportJob` currently follows). Each job execution gets its own DI scope so two concurrent language jobs don't share a context with each other — safe in that sense — but change-tracker growth within one job's run is a latent hygiene issue, unrelated to the reported symptom.

**Conclusion for Part 4: DB contention is not a plausible explanation.** Two concurrent language runs write to genuinely independent rows.

---

## Part 5 — SignalR broadcast load

All broadcasts (`ValidationProgress`, `SectionCompleted`, `ValidationComplete`) originate in `TranslationValidationJob.cs` via `_hubContext.Clients.Group($"validation-{validationRunId}").SendAsync(...)` (call sites: 220-223, 329, 394, 439, plus error/cancel paths at 469, 555, and the send helpers at 832-833, 868-869, 897-909).

Grouping is **per validation-run-id** (`TranslationValidationHub.cs:11,29,41`) — 2 languages produce 2 distinct groups, not a shared/interleaved stream. Sends are `await`ed inline in the section loop but only wait for the message to be handed to SignalR's transport (no client-ack wait observed, no backplane configured), each wrapped in its own try/catch that swallows exceptions. No batching/throttling exists, but volume scales with section count per run, not with concurrent-run count.

**Conclusion for Part 5: SignalR broadcast load is not a plausible backend bottleneck.** Nothing here would multiplicatively slow job execution.

---

## Part 6 — Reference talk state (direct DB inspection)

Queried the local dev Postgres instance directly (`rascor_stock` database, `toolbox_talks` schema — entities live in a dedicated schema, not `public`).

**Talk:** `0385399c-b5c0-4928-b809-1b6aa4f4598c` — "E2E Wizard Manual Run 1783950583689", Code `EWMR1-001`, Status `Draft`, tenant `11111111-1111-1111-1111-111111111111` (RASCOR).

**TranslationValidationRuns (only 2 ever created for this talk — no earlier attempts exist):**

| LanguageCode | Status | StartedAt | CompletedAt | Duration | Score | Outcome |
|---|---|---|---|---|---|---|
| pt | Completed | 14:50:30.614 | 14:53:25.455 | **2m 55s** | 89 | Review |
| af | Completed | 14:50:31.274 | 14:53:44.346 | **3m 13s** | 88 | Review |

Both `StartedAt` timestamps are **~1 second apart**, matching the frontend's documented 1-second stagger exactly. `TotalSections = 5` for both, matching the "5 sections" recorded for this same fixture in the manual-run observation doc's timing table. This is very likely the actual pair of runs from the observed manual session (talk ID, language pair pt+af, section count, and stagger interval all match); it cannot be proven with 100% certainty from the DB alone, but the correlation is strong on every available axis.

**Hangfire job history for these exact runs** (`hangfire.job`/`hangfire.state`):

| Job ID | Enqueued | Processing | Succeeded |
|---|---|---|---|
| 231 (pt) | 14:50:30.504 | 14:50:30.546 | 14:53:25.632 |
| 232 (af) | 14:50:31.241 | 14:50:31.258 | 14:53:44.372 |

Both jobs dequeued in under 50ms — **no queueing delay whatsoever.**

**Per-section timing within each run** (`TranslationValidationResults.CreatedAt`):
- pt run: sections completed at 14:53:07, 12, 16, 20, 25 — all 5 sections validated in **~18 seconds total**, but the job didn't reach its first section until **2m37s after it started**.
- af run: sections completed at 14:53:26, 30, 35, 40, 44 — again ~18 seconds for all 5 sections, first section reached **2m55s after start**.

This pinpoints where the ~3 minutes actually goes: **not** the back-translation/consensus validation phase (fast — ~18s for 5 sections, all resolved in 3 rounds each), but the **forward-translation generation phase** (`GenerateTranslationForSectionsAsync`) — title + content + quiz questions/options translated one sequential Claude call at a time before validation can even begin. The af run's slightly longer pre-validation phase (2m55s vs pt's 2m37s, a ~10% difference) is consistent with mild resource contention between the two concurrently-running jobs, not a severe one.

**Total wall clock for 2 languages together: ~3m13s (from the earlier start to the later finish) — essentially equal to, not 7x worse than, the ~3m12s single-language baseline documented for this same fixture.**

I also confirmed (via `hangfire.job` timestamps around this window) that the API server had been freshly (re)started at ~14:31:43 — a burst of scheduled recurring jobs (`UpdateOverdueToolboxTalksJob`, `DailyTranslationScanJob`, `ExpiredSessionCleanupJob`, etc.) all fired within the same second, a classic Hangfire "catch up on recurring jobs due" pattern on server startup. This places roughly 19 minutes between server start and the "Start All" click — plausibly consumed by the wizard's earlier steps (Input & Config, Parse, Quiz generation, Settings) plus normal Playwright-driven test/observation overhead, not by anything relevant to the translation job itself.

**This directly contradicts "did not complete within 23+ minutes."** The backend completed the actual work in about 3 minutes. Whatever the tester observed, it wasn't the backend failing to finish.

---

## Part 7 — Diagnosis

| Candidate | Verdict | Evidence |
|---|---|---|
| **A. Hangfire worker starvation** | **Not supported** | Both jobs dequeued in <50ms; WorkerCount=80 on this machine (16 cores × 5); no `[DisableConcurrentExecution]` or queue caps found. |
| **B. External API rate limiting** | **Plausible in general, not evidenced in this run** | No in-process throttling exists anywhere (confirmed by grep), and many unrelated Claude-based features share the same API key/quota — a genuine latent risk. But the captured run's per-section validation phase completed in ~18s for 5 sections with 3 rounds each, showing no sign of 429/backoff stalls in this instance. |
| **C. Database contention** | **Ruled out** | Every write is scoped by `ValidationRunId`/`{TalkId, LanguageCode}` to independent rows; no shared-row read-modify-write pattern exists; only one harmless idempotent same-row race found (legacy-only, not exercised here). |
| **D. Non-linear cost per language (cross-language validation)** | **Ruled out** | Nothing in `ConsensusEngine`, `TranslationValidationService`, or `TranslationValidationJob` reads or compares across languages/runs. Each run is fully independent. |
| **E. Frontend status-tracking gap (new candidate, not in original list)** | **Most likely explanation of the observed symptom** | `useWorkflowSubscription.ts` explicitly does **not poll** ("consumers must render one `<WorkflowSubscriber />` per activeRunId... to receive SignalR-driven invalidations when jobs complete" — no fallback refetch interval exists). `activeRunIds` is derived solely from the last-fetched `workflow-state` query; `WorkflowSubscriber` only mounts (and only then subscribes to the run's SignalR group) for runs already reflected in that data. If any single invalidation/refetch in this chain lands out of order, gets missed, or the SignalR connection to a given run's group fails silently at exactly the wrong moment, that run's row has no other mechanism to ever learn it finished — there is no periodic re-check. This is architecturally the same class of gap the codebase had already found and fixed twice for the **Validate** step (commits `d0996c4`, `cc55b71`, referenced in CLAUDE.md and the manual-run observation doc) — those fixes were never applied to the **Translate** step's row-level tracking, which is a separate hook/component tree. |

**Ranking:** E > B > (A, C, D ruled out for this specific reproduction). The reported symptom (UI stuck for 23+ minutes) is best explained by a frontend tracking/display defect, evidenced by the fact the backend genuinely finished in ~3 minutes for the same run. B remains a real, unaddressed architectural gap worth closing regardless, since it could cause genuine (not just apparent) multiplicative slowdowns under different conditions (3+ languages simultaneously, or other AI-heavy jobs — content generation, quiz generation, regulatory scoring — running concurrently against the same Claude quota).

---

## Part 8 — Fix size estimate

**Top candidate (E — frontend tracking gap):** **Small.** Scope would be: add either (a) a periodic fallback refetch/poll on the `workflow-state` query (bounded interval, e.g. every 5-10s, only while any language is in an active state) as a safety net independent of SignalR, or (b) apply the same fix pattern already proven for the Validate step (`d0996c4`/`cc55b71`) to the Translate step's subscription chain. Either is a contained, well-understood frontend change — likely a few hours including verification against a live 2-language run, not a redesign.

**Secondary candidate (B — no provider-side throttling):** **Small-to-Medium.** A `SemaphoreSlim`-based concurrency limiter per external provider (Claude, DeepL, Gemini) wrapping the relevant `HttpClient` calls is a contained, standard pattern. Sizing the actual limits requires a decision (not purely a recon question) about acceptable cross-feature Claude concurrency — since Claude is shared by translation, back-translation, content generation, quiz generation, and regulatory scoring — so this may warrant a short dedicated design pass rather than a drive-by change.

---

## Adjacent findings (flagged, not fixed)

1. **DbContext lifetime in `TranslationValidationJob`** does not follow CLAUDE.md Note 23's per-unit-of-work scope pattern (only `BulkEmployeeImportJob` currently does). Not evidenced as causing a problem here, but flagged as a hygiene gap per the existing documented standard.
2. **`ContentCreationSessions` same-row write race** (Part 4) — idempotent and harmless today, but worth a `RowVersion`/concurrency token if the session-status update logic ever becomes non-idempotent.
3. **Railway/production Hangfire `WorkerCount`** was not verified — this recon only confirmed the dev machine's 16-core/80-worker figure. If Railway's container allocation is much smaller (e.g. 1-2 vCPUs → WorkerCount 5-10), the safety margin for concurrent AI jobs is thinner in production than what this recon observed locally. Worth a quick confirmation before treating "no worker starvation" as a universal conclusion.
4. **RoundsUsed = 3 for every section in both observed runs** — the consensus engine escalated to the full 3-round chain (Haiku+DeepL, then Gemini, then Sonnet) for all 10 sections across both languages, with no section resolving at Round 1. This wasn't investigated further (a correctness/tuning question, not a timing one) but is worth someone's attention — if Round 1 essentially never resolves in practice, the "escalate only when inconclusive" design isn't actually saving any calls, and the documented ≤10pt agreement tolerance may be miscalibrated.
