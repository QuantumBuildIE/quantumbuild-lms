# Provider Client Architecture — Recon for Concurrency Throttling

_Date: 2026-07-13 | Branch: transval | Status: Read-only recon — no code modified_

---

## 0. Headline

**Confirmed: no concurrency throttling of any kind exists for Claude, DeepL, or Gemini calls anywhere in the codebase.** A repo-wide grep for `SemaphoreSlim`, `RateLimit`, `Throttl`, `MaxConcurrency`, `MaxDegreeOfParallelism`, `Parallel.ForEach`, `ConcurrentQueue`, and `PartitionedRateLimiter` across `src/` returns zero real hits (the only matches are `.lscache` build-artifact lines listing `System.Threading.RateLimiting.dll` as a transitive NuGet dependency of ASP.NET Core — not code that uses it). There is also no shared `DelegatingHandler`, no `SocketsHttpHandler`/`HttpClientHandler` configuration, and no `ConfigurePrimaryHttpMessageHandler` call anywhere — every provider `HttpClient` uses the vanilla HttpClientFactory-managed handler with unlimited default connections-per-server.

Two adjacent recon docs already existed and are treated here as hypotheses, both **confirmed accurate**:
- `docs/multi-language-slowdown-recon.md` (2026-07, prior session) — reached the same "no throttling exists" conclusion via a narrower grep scoped to ToolboxTalks Infrastructure; this recon re-verifies at the whole-`src/` level and adds the DI/registration/call-graph detail needed to place a fix.
- `docs/option-b-multi-provider-recon.md` — described a pending migration from legacy per-feature settings classes (`SubtitleProcessingSettings.Claude.Model`, `TranslationValidationSettings.Round1AModel/Round3DModel/Gemini.Model`) to the canonical `AIProviderOptions` registry. **This migration has since been completed** — every provider service read in code today (`ClaudeHaikuBackTranslationService`, `ClaudeSonnetBackTranslationService`, `GeminiTranslationService`, etc.) already injects `IOptions<AIProviderOptions>` and reads `_aiProviders.Anthropic.Models.Haiku` / `.Sonnet` / `_aiProviders.Gemini.Models.Flash`. CLAUDE.md's Note 32 (the "migrate atomically" rule) is confirmed still accurate as a general principle but the specific migration it was written to guard is done. The DeepL free-vs-paid base URL selector flagged in that doc is also confirmed live (see Part 4).

No streaming responses exist on any of these paths (see Part 3) — every provider call is a buffered `SendAsync` + `ReadAsStringAsync`, which simplifies semaphore permit-hold semantics (hold-for-full-request-duration, no early-release-on-first-byte complexity).

---

## Part 1 — Existing provider clients

### Claude (Anthropic)

There is no single "Claude client" — there are **7 independent service classes**, each with its own `HttpClient` (via `AddHttpClient<TInterface, TImplementation>`), each building its own request JSON and calling `POST {baseUrl}/messages` directly. No shared SDK, no shared wrapper class.

| Service | File | Model used | Purpose |
|---|---|---|---|
| `ClaudeHaikuBackTranslationService` | `Infrastructure/Services/Validation/ClaudeHaikuBackTranslationService.cs` | Haiku | Round 1 back-translation (Provider A) |
| `ClaudeSonnetBackTranslationService` | `Infrastructure/Services/Validation/ClaudeSonnetBackTranslationService.cs` | Sonnet | Round 3 back-translation (Provider D, replaced DeepSeek) |
| `ContentTranslationService` | `Infrastructure/Services/Translations/ContentTranslationService.cs` | Sonnet | Forward-translation of sections/quiz/title/description |
| `ClaudeTranslationService` | `Infrastructure/Services/Subtitles/ClaudeTranslationService.cs` | Sonnet | Subtitle (SRT) translation |
| `AiSectionGenerationService` | `Infrastructure/Services/AiSectionGenerationService.cs` | Sonnet | AI content generation — sections from video/PDF |
| `AiQuizGenerationService` | `Infrastructure/Services/AiQuizGenerationService.cs` | Sonnet | AI quiz question generation |
| `AiSlideshowGenerationService` | `Infrastructure/Services/Slideshow/AiSlideshowGenerationService.cs` | Sonnet | HTML slideshow generation from PDF |
| `ContentParserService` | `Infrastructure/Services/ContentCreation/ContentParserService.cs` | Sonnet | Wizard content extraction/parsing |
| `DialectDetectionService` | `Infrastructure/Services/Validation/DialectDetectionService.cs` | Haiku | Source dialect detection |
| `PreFlightScanService` | `Infrastructure/Services/Validation/PreFlightScanService.cs` | Haiku | Pre-translation content scan |
| `RegulatoryScoreService` | `Infrastructure/Services/Validation/RegulatoryScoreService.cs` | Sonnet | Regulatory compliance scoring |
| `RequirementIngestionJob` (own typed HttpClient) | `Infrastructure/Jobs/RequirementIngestionJob.cs` | Sonnet | Regulatory requirement extraction |
| `RequirementMappingJob` (own typed HttpClient) | `Infrastructure/Jobs/RequirementMappingJob.cs` | Sonnet | Content↔requirement AI mapping |

**Abstraction layers between application code and the Anthropic HTTP wire, per call:**
1. Command handler / Hangfire job (application layer) — calls the interface (e.g. `IClaudeHaikuBackTranslationService`)
2. Concrete service class — builds Anthropic Messages API JSON payload directly, no SDK
3. Injected `HttpClient` (from `IHttpClientFactory` via typed-client registration) — `SendAsync`
4. Polly `IAsyncPolicy<HttpResponseMessage>` — chained via `.AddPolicyHandler(...)` at registration (see Part 3) — wraps the actual socket call transparently
5. `AnthropicResponseParser.Parse(responseBody)` (`Infrastructure/Services/AnthropicResponseParser.cs:13`) — static utility, extracts `ContentText`/`InputTokens`/`OutputTokens`/`Model` from the raw JSON
6. `IAiUsageLogger.LogAsync(...)` (`Infrastructure/Services/AiUsageLogger.cs`) — fire-and-forget-style (awaited but self-swallowing try/catch) DB write of usage row — called **after** every successful response, before returning to caller

Not every Claude service calls steps 5/6 — confirmed present in `ClaudeHaikuBackTranslationService`, `ClaudeSonnetBackTranslationService`, `ContentTranslationService`, `ClaudeTranslationService`, `AiSectionGenerationService`, `AiQuizGenerationService`, `AiSlideshowGenerationService`, `ContentParserService`, `DialectDetectionService`, `PreFlightScanService`, `RegulatoryScoreService` (11 files reference `IAiUsageLogger`/`_aiUsageLogger`). No middleware/DelegatingHandler does this centrally — it's a manual call duplicated in each service's method body, after `SendAsync` returns and before the method returns its result.

### DeepL

Single service: `DeepLTranslationService` (`Infrastructure/Services/Validation/DeepLTranslationService.cs`). Direct REST call to `POST {baseUrl}/translate` (form-encoded), own local source/target language-code mapping dictionaries. No Anthropic-style response parser needed — parses its own JSON inline (`ParseDeepLResponse`). No `IAiUsageLogger` call (DeepL isn't Claude, so it's outside the AI-usage-logging scope, which the codebase documents as being for Claude billing specifically). Confirmed the free-vs-paid base URL trade-off flagged in the prior recon: a 403 response triggers a specific log hint pointing at `TranslationValidation:DeepL:BaseUrl` mismatch (line ~172-177).

### Gemini

Single service: `GeminiTranslationService` (`Infrastructure/Services/Validation/GeminiTranslationService.cs`). Direct REST call to `POST {baseUrl}/models/{model}:generateContent?key={apiKey}`. No shared SDK. No `IAiUsageLogger` call either — same reasoning as DeepL (Gemini/DeepL usage isn't tracked in the Claude-billing-focused `AiUsageLog` table at all currently — a gap, but out of scope here).

### Call graph — "translate a section from English to Irish" (forward translation + validation)

This is the highest-volume, highest-concurrency-risk path (per CLAUDE.md and confirmed in Part 2/3):

```
TranslationValidationController.StartValidation / StartTranslation (API)
  → BackgroundJob.Enqueue<TranslationValidationJob>            [Hangfire, queue "content-generation"]
    → TranslationValidationJob.ExecuteAsync
      → GenerateTranslationForSectionsAsync (forward-translate phase, sequential per-section loop)
        → ContentTranslationService.TranslateTextAsync (title, then per-section content)
          → HttpClient.SendAsync → Polly GetClaudePolicy → Anthropic /messages
          → AnthropicResponseParser.Parse → IAiUsageLogger.LogAsync
      → per-section validation loop (sequential `for`, TranslationValidationJob.cs:~214-227)
        → TranslationValidationService.ValidateSectionAsync
          → GlossaryReplacementService (local, no HTTP)
          → SafetyClassificationService (local, no HTTP)
          → ConsensusEngine.RunAsync
            Round 1 (always): ClaudeHaikuBackTranslationService.BackTranslateAsync, then DeepLTranslationService.BackTranslateAsync — sequential awaits
            Round 2 (if inconclusive): + GeminiTranslationService.BackTranslateAsync
            Round 3 (if still inconclusive): + ClaudeSonnetBackTranslationService.BackTranslateAsync
          → LexicalScoringService (local scoring, no HTTP)
          → GlossaryTermVerificationService (local, no HTTP)
          → persists TranslationValidationResult (upsert)
      → SignalR broadcasts (ValidationProgress / SectionCompleted / ValidationComplete)
```

No class or method in this entire chain is named a "Claude client," "provider gateway," or similar — the abstraction is purely at the per-provider-per-purpose service level (one class per {provider, feature} pair), registered individually in DI.

---

## Part 2 — Consumers

### Claude consumers (top-level)

| Consumer | Type | Calls |
|---|---|---|
| `TranslationValidationJob` | Hangfire job | `ContentTranslationService` (forward-translate), `ConsensusEngine` → `ClaudeHaikuBackTranslationService` + `ClaudeSonnetBackTranslationService` |
| `ContentGenerationJob` | Hangfire job | `AiSectionGenerationService`, `AiQuizGenerationService`, `AiSlideshowGenerationService` (via `IContentGenerationService` orchestrator) |
| `ContentCreationParseJob` / `ContentCreationParseJobForTalk` | Hangfire job | `ContentParserService` |
| `RequirementIngestionJob` | Hangfire job | own typed `HttpClient` directly (fetches doc + calls Claude) |
| `RequirementMappingJob` | Hangfire job | own typed `HttpClient` directly (AI mapping analysis) |
| `CorpusRunJob` | Hangfire job | `TranslationValidationService.ValidateSectionAsync(persist: false)` — same consensus/Claude/DeepL/Gemini path as live validation, dry-run mode, looped sequentially over corpus entries (`for` loop, `CorpusRunJob.cs:134`) |
| `GenerateContentTranslationsCommandHandler` | CQRS command handler | `ContentTranslationService` |
| `ParseToolboxTalkContentCommandHandler` | CQRS command handler | `ContentParserService` |
| `GenerateToolboxTalkQuizCommandHandler` | CQRS command handler | `AiQuizGenerationService` |
| `RegulatoryScoreController` (synchronous HTTP request path) | Controller | `RegulatoryScoreService` |
| `ContentCreationSessionService` | Application service (invoked from wizard endpoints, also enqueues jobs) | `IContentParserService`, dispatches `TranslationValidationJob`/`ContentGenerationJob` per language/section |

### DeepL / Gemini consumers

Both are consumed **only** through `ConsensusEngine.RunAsync` — no other call site exists in the codebase (confirmed by the interface-name grep: `IDeepLTranslationService`/`IGeminiTranslationService` appear only in their own service file, the DI registration file, and `ConsensusEngine.cs`). This means DeepL/Gemini concurrency exposure is a strict subset of Claude's — every DeepL/Gemini call happens inside a `TranslationValidationJob` or `CorpusRunJob` section-validation loop, never from a content-generation or quiz path.

### Lifetime: transient-per-resolution, shared handler pool

All provider services are registered via `services.AddHttpClient<TInterface, TImplementation>(...)` (`ServiceCollectionExtensions.cs`), which is ASP.NET Core's **typed client** pattern:
- The service class itself (e.g. `ClaudeHaikuBackTranslationService`) is registered as a **transient** DI service — a new instance is constructed on every resolution (once per Hangfire job execution scope, or once per HTTP request scope for `RegulatoryScoreController`).
- The `HttpClient` injected into its constructor is drawn from `IHttpClientFactory`'s internal handler pool — the underlying `SocketsHttpHandler` (and its TCP connection pool) is shared and recycled (default 2-minute handler lifetime), not created fresh per call. No `MaxConnectionsPerServer` or other connection cap is configured anywhere, so the effective per-provider concurrency ceiling today is **whatever the OS/network allows**, not anything the app controls.
- No `AddSingleton` registration exists for any provider service — a semaphore held as an instance field on one of these classes would NOT be shared across resolutions unless the semaphore itself were registered as a singleton dependency (a distinct, injectable object) rather than a private field.

---

## Part 3 — Existing patterns

### Retry (confirmed present, provider-specific)

`ResiliencePolicies.cs` (`src/Core/QuantumBuild.Core.Application/Http/ResiliencePolicies.cs`) defines three named Polly policies, chained at `AddHttpClient(...).AddPolicyHandler(...)` registration time (never inside service classes, per CLAUDE.md rule — confirmed, no manual retry loop found in any provider service body):

| Policy | Retries | Backoff | Triggers | Wired to |
|---|---|---|---|---|
| `GetClaudePolicy` | 3 | 2s/4s/8s + ±500ms jitter | `HttpRequestException`, 429/500/502/503/529 | `ContentTranslationService`, `AiSectionGenerationService`, `AiQuizGenerationService`, `AiSlideshowGenerationService`, `ClaudeSonnetBackTranslationService`, `ClaudeHaikuBackTranslationService`, `DialectDetectionService`, `PreFlightScanService`, `RegulatoryScoreService`, `ContentParserService`, `RequirementIngestionJob`, `RequirementMappingJob`, plus the generic `"ClaudeApi"` named client in `Program.cs:97-102` |
| `GetElevenLabsPolicy` | 2 | 2s/4s + jitter | same codes | `ElevenLabsTranscriptionService` (not in scope here — no throttling concern raised for it) |
| `GetTransientPolicy` | 3 | 1s/2s/4s | `HttpRequestException`, 429, all 5xx | `DeepLTranslationService`, `GeminiTranslationService` |

**Note**: `ClaudeTranslationService` (subtitle translation) is registered at `ServiceCollectionExtensions.cs:104-107` with **no** `.AddPolicyHandler(...)` call — it has a 5-minute timeout but no retry policy at all. This is an inconsistency (every other Claude-calling service gets `GetClaudePolicy`) but is outside this recon's throttling scope; flagged for awareness since it means retry behavior differs across "the same provider."

### Circuit breaker

**None.** No `Policy.Handle<...>().CircuitBreakerAsync(...)` or `AdvancedCircuitBreakerAsync` anywhere in the codebase. A provider having a bad day (e.g., Anthropic 529 "overloaded" storms) is handled only by the same 3-retry/8s-max-backoff policy per call — no breaker opens to stop hammering a failing provider across calls.

### Timeouts

Set per-`HttpClient` at registration (`client.Timeout = TimeSpan.FromMinutes(N)`), ranging from 1 minute (`PreFlightScanService`, `DialectDetectionService`) to 5 minutes (`ContentTranslationService`, `ClaudeTranslationService`, `AiSlideshowGenerationService`, `RequirementIngestionJob`, `RequirementMappingJob`) — these are per-request ceilings, not concurrency controls.

### Concurrency control — confirmed absent

Exhaustive grep (`SemaphoreSlim|Semaphore\(|RateLimit|Throttl|MaxDegreeOfParallelism|MaxConcurrency|Parallel\.ForEach|ConcurrentQueue|DelegatingHandler|PartitionedRateLimiter`) across all of `src/` returns **zero** real matches (only `.lscache` build-artifact noise listing a transitive DLL). Specifically confirmed:
- `ConsensusEngine.RunAsync` (`ConsensusEngine.cs:64-140`) — every round is a plain sequential `await`, never `Task.WhenAll`, even for the two independent Round-1 calls (Haiku + DeepL) that have no data dependency on each other and could run in parallel today.
- `TranslationValidationJob`'s per-section loop (`TranslationValidationJob.cs:~214-227`) and its forward-translation loop (`foreach (var section in originalSections)`, line 1024) are both plain sequential loops.
- `CorpusRunJob`'s per-entry loop (`CorpusRunJob.cs:134`, `for (int i = 0; i < entries.Count; i++)`) is sequential, dry-run-calling the same `ConsensusEngine` path.
- No `[DisableConcurrentExecution]` Hangfire attribute exists on any job in the codebase (grep confirmed zero hits) — nothing prevents N `TranslationValidationJob`/`ContentGenerationJob`/`CorpusRunJob` instances from all running at once, each independently hammering the same Anthropic API key.
- A **dead config knob** exists that looks like it should control this: `TranslationValidationSettings.ProcessingMode` (`Infrastructure/Configuration/TranslationValidationSettings.cs:55`), documented as `"Sequential" or "Parallel"`, default `"Sequential"`. Grep confirms its **only** consumer is `PipelineVersionService.cs:135`, which just serializes the string into an audit JSON snapshot — `ConsensusEngine` never reads `_settings.ProcessingMode` and always executes sequentially regardless of this value. Worth flagging: a future throttling/parallelism change should either wire this property up for real or remove it, since as-is it silently documents behavior the code doesn't implement.
- No shared `DelegatingHandler` in any `AddHttpClient` chain — every registration only adds `.AddPolicyHandler(...)`, never a custom handler class. This means a concurrency-limiting `DelegatingHandler` would be a **new** pattern for this codebase, not an extension of an existing one.
- No ASP.NET Core outbound rate limiting (`Microsoft.Extensions.Http.Resilience`, `System.Threading.RateLimiting`) is wired into any `HttpClient` — the `System.Threading.RateLimiting.dll` reference is a transitive dependency of `Microsoft.AspNetCore.RateLimiting` used only for the ASP.NET Core inbound-request rate-limiting middleware, and that middleware itself is not invoked anywhere in `Program.cs` either (`AddRateLimiter`/`UseRateLimiter` — zero hits).

### Streaming — confirmed none

Every provider call in scope uses `await _httpClient.SendAsync(request, cancellationToken)` followed by `await response.Content.ReadAsStringAsync(cancellationToken)` — fully buffered request/response, no `HttpCompletionOption.ResponseHeadersRead`, no `Stream`-based reading, no Server-Sent-Events parsing anywhere in the Claude/DeepL/Gemini call sites. This means a semaphore permit held for "the duration of the call" has simple, well-defined semantics here — acquire before `SendAsync`, release after the response body is fully read (or on exception) — with no risk of a permit being held open indefinitely by a slow client consuming a live stream.

---

## Part 4 — Configuration pattern

### Strongly-typed `IOptions<T>`, consistently

All provider configuration is bound via `IOptions<T>` — no raw `IConfiguration["..."]` access was found in any provider service constructor or method body. Three settings classes are involved (plus the canonical registry):

1. **`AIProviderOptions`** (`src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs`) — the canonical model-identifier registry (CLAUDE.md Note 32). Confirmed shape:
   ```
   AIProviders (SectionName)
   ├── Anthropic.Models.{ Sonnet, Haiku }
   ├── Gemini.Models.{ Flash }
   └── ElevenLabs.Models.{ Transcription }
   ```
   Registered with `.BindConfiguration(...).ValidateOnStart()` in both `Program.cs:116-119` (API entry point) and `ServiceCollectionExtensions.cs` is not where it's registered — it's registered once, at `Program.cs`, and consumed via constructor injection everywhere else. `AIProviderOptionsValidator` (`Configuration/AIProviderOptionsValidator.cs`) fails startup if any of the 4 model strings is empty — this is a **fail-fast-at-startup** pattern, confirmed working as documented.
   **Migration status (re: Note 32 / option-b-multi-provider-recon.md): complete.** Every service that recon flagged as a pending migration target (`ClaudeHaikuBackTranslationService`, `ClaudeSonnetBackTranslationService`, `GeminiTranslationService`, `ContentTranslationService`, `AiSectionGenerationService`, `AiQuizGenerationService`, `AiSlideshowGenerationService`, `ContentParserService`, `ClaudeTranslationService`, `PipelineVersionService`) now reads model identifiers from `IOptions<AIProviderOptions>`, confirmed by direct file reads.

2. **`SubtitleProcessingSettings`** (`Infrastructure/Configuration/SubtitleProcessingSettings.cs`) — still holds `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl` (the `Model` property was the one migrated away). These three remain the live source for auth header, token cap, and base URL on every Claude-calling service that predates the `AIProviderOptions` split — i.e. most Claude services inject **both** `IOptions<SubtitleProcessingSettings>` (for API key/base URL/max tokens) **and** `IOptions<AIProviderOptions>` (for the model string) side by side. This dual-injection pattern is now the norm, not an anomaly — any new provider-related config addition (e.g. a per-provider concurrency limit) has an established precedent for living in either class, but a **new dedicated options class** (e.g. `AIProviderConcurrencyOptions`) bound the same `AddOptions<T>().BindConfiguration(...).ValidateOnStart()` way would be the cleanest fit, mirroring the `AIProviderOptions` pattern exactly.

3. **`TranslationValidationSettings`** (`Infrastructure/Configuration/TranslationValidationSettings.cs`) — holds `DeepL.{ApiKey, BaseUrl}`, `Gemini.{ApiKey, BaseUrl}`, plus business-logic thresholds (`DefaultThreshold`, `SafetyCriticalBump`, `MaxRounds`, `AgreementThreshold`, `ProcessingMode` [dead, see Part 3], `SessionExpiryHours`, `PromptVersion`, `PipelineVersion`). Registered via plain `services.Configure<TranslationValidationSettings>(...)` — **no** `ValidateOnStart()` here (unlike `AIProviderOptions` and `SubtitleProcessingSettings`), so a missing DeepL/Gemini API key fails silently at first real call (each service already defensively checks `IsNullOrWhiteSpace(ApiKey)` and returns `null`/skips rather than throwing — by design, since DeepL/Gemini are optional consensus-engine providers, not hard requirements).

### Environment variables

Confirmed via `appsettings.json`/`appsettings.Development.json` structure (Railway env vars use the ASP.NET Core `__` separator per Note 31): `AIProviders__Anthropic__Models__Sonnet`, `AIProviders__Anthropic__Models__Haiku`, `AIProviders__Gemini__Models__Flash`, `TranslationValidation__DeepL__ApiKey`, `TranslationValidation__DeepL__BaseUrl`, `TranslationValidation__Gemini__ApiKey`, `SubtitleProcessing__Claude__ApiKey`, etc. Any new concurrency-limit setting would follow the same `Section__SubSection__Key` env var convention.

### DeepL free-vs-paid base URL (confirmed live)

`DeepLSettings.BaseUrl` defaults to `https://api-free.deepl.com/v2`; CLAUDE.md's "Known Issues" #1 documents that paid keys require `https://api.deepl.com/v2` (no trailing `/translate`). `DeepLTranslationService.cs:172-177` has a defensive log hint that fires specifically on HTTP 403, pointing at this exact mismatch. Confirmed still the live, correct behavior — not a stale doc claim.

### Relevant CLAUDE.md Notes for wiring a per-provider semaphore into job-called services

- **Note 21 (Hangfire enqueue via concrete types)** — not directly relevant to injecting a semaphore, but relevant if the fix introduces any new Hangfire job: always `Enqueue<ConcreteClass>`, never `Enqueue<IInterface>`, or job attributes (retry count, queue) silently fall back to defaults. Not expected to be needed for a throttling fix (no new job type anticipated), but worth remembering if the design ends up wanting a dedicated "drain queue" job.
- **Note 22 (TenantEntity creation inside Hangfire jobs)** — irrelevant; a semaphore is not a `TenantEntity` and carries no tenant-scoped DB write.
- **Note 23 (DbContext isolation in long-running jobs)** — relevant as **context**, not as a blocker: `TranslationValidationJob` currently does NOT follow this pattern (it reuses one injected `IToolboxTalksDbContext` across its whole section loop, confirmed at multiple `_dbContext.*` call sites spanning lines 114 through 1362). This means the job already holds one DbContext open for the job's entire duration, including through every Claude/DeepL/Gemini call. **Implication for throttling design:** if a semaphore wait blocks inside this job while the DbContext is open (e.g., between an EF write and the next provider call), there's no NEW deadlock risk introduced beyond what already exists — the DbContext isn't used *concurrently* from multiple threads in a single job execution, so a semaphore delay just makes the single job's timeline longer, it doesn't create contention on the DbContext itself. The one thing to avoid: never call `_dbContext.SaveChangesAsync()` while *holding* a semaphore permit meant to gate a different job's HTTP call — that's a separate resource (DB vs. HTTP) so no cross-resource deadlock is introduced by the current design, but it's worth being deliberate about permit acquire/release boundaries when implementing.
- Since all provider services are DI-resolved by Hangfire jobs the same way they are by HTTP-request-scoped controllers (confirmed — `RegulatoryScoreController` calls `RegulatoryScoreService` synchronously in the request path; `TranslationValidationJob` calls the same class of services from a background job), any singleton semaphore/limiter registered in DI is trivially shared across both call paths with zero additional wiring — this is a `AddSingleton<T>()` registration exactly like `ILexicalScoringService` et al. already use.

---

## Part 5 — Placement recommendation

**Recommend: a small set of named, singleton, per-provider semaphores (or an injectable rate-limiter abstraction) applied inside each provider service class's HTTP call — NOT a `DelegatingHandler`, NOT a change to `ConsensusEngine`/job orchestration.**

Rationale, tied to findings above:

1. **Not the abstraction/orchestration layer (`ConsensusEngine`, `TranslationValidationJob`, `ContentGenerationJob`).** These orchestrate calls to *multiple different providers* per invocation (Claude Haiku + DeepL in Round 1, +Gemini in Round 2, +Claude Sonnet in Round 3). A single throttle placed here would conflate three independent providers' concurrency budgets into one, which is wrong — DeepL rate limits have nothing to do with Anthropic's, and vice versa. Per-provider limiting must live closer to "where the provider identity is known," which is the service class, not the orchestrator.

2. **Not a `DelegatingHandler`.** This would be architecturally clean (one handler per typed `HttpClient` registration, analogous to how Polly policies are already chained via `.AddPolicyHandler`) and IS a legitimate alternative — but two things argue against it as the first move: (a) it's a brand-new pattern for this codebase (no `DelegatingHandler` exists anywhere today — the team's established idiom for "wrap this HttpClient's behavior" is Polly policies chained at registration, not custom handlers), and (b) a handler operates below the request-building code, meaning a "waiting for permit" state can't easily be surfaced in logs/SignalR progress messages the way an explicit semaphore wait inside the service method can (e.g., logging "queued waiting for Claude Haiku slot" before `_httpClient.SendAsync`). That said, if the team wants a single, reusable "concurrency-limiting Polly policy" (Polly does support bulkhead policies — `Policy.BulkheadAsync`), that could be chained via `.AddPolicyHandler(...)` exactly like the existing retry policies, keeping full consistency with the current idiom. **This is worth strongly considering as the actual implementation mechanism** — it fits the existing "chain policies at HttpClient registration" convention perfectly, requires zero changes to service class bodies, and Polly's `Bulkhead` policy is purpose-built for exactly this (max concurrent executions + optional queue depth). Recommend evaluating `Policy.BulkheadAsync<HttpResponseMessage>(maxParallelization, maxQueuingActions)` chained the same way `GetClaudePolicy`/`GetTransientPolicy` are today, added as a 4th named policy in `ResiliencePolicies.cs` (e.g. `GetProviderBulkheadPolicy(maxConcurrency)`), registered per-provider (not per-service — Claude Haiku, Claude Sonnet, ContentTranslationService, etc. all share the same Anthropic quota and should probably share one bulkhead instance keyed by provider, not one per typed client).

3. **Provider client class directly (semaphore as constructor-injected singleton)** is the fallback if Bulkhead-via-Polly proves awkward (e.g., if Round 1 Haiku and Round 3 Sonnet need genuinely different concurrency budgets from each other despite sharing a provider/API key — plausible, since they're registered as separate typed clients today). In that case: register one `SemaphoreSlim` (or a small custom `IProviderConcurrencyLimiter` wrapping one) as `AddSingleton` per provider ("Anthropic", "DeepL", "Gemini") in `ServiceCollectionExtensions.cs`/`Program.cs`, inject it into each relevant service constructor, `await _limiter.WaitAsync(cancellationToken)` immediately before `SendAsync` and release in a `finally`. This mirrors the existing DI idiom exactly (singleton scoring/diff services like `ILexicalScoringService` already prove this registration pattern works for cross-cutting, stateless-but-shared infrastructure).

4. **Whichever mechanism**, it must be keyed by **provider**, not by service class or by feature — Claude Haiku (back-translation), Claude Sonnet (back-translation + forward-translation + section/quiz/slideshow generation + regulatory scoring + requirement ingestion/mapping) all share one Anthropic API key/quota per CLAUDE.md's own framing ("every Claude-based feature in the app competes for the same upstream quota"), so the limiter instance must be provider-scoped and injected into all ~11 Claude-calling services, not instantiated separately per class.

---

## Part 6 — Fix size estimate and risks

### Minimal footprint

- **If Polly Bulkhead approach:** 1 file changed for the policy definition (`ResiliencePolicies.cs`, add `GetProviderBulkheadPolicy`), plus edits to the `.AddPolicyHandler(...)` chain at each of the ~13 `AddHttpClient<...>` registration call sites in `ServiceCollectionExtensions.cs` (Claude services) + `Program.cs:97-102` (the generic `"ClaudeApi"` client) + 2 sites for DeepL/Gemini. **Total: 2 files touched**, ~15 registration-line edits. No service class body changes needed at all — this is the main appeal of the Bulkhead approach.
- **If explicit-semaphore approach:** 1 new class/interface (`IProviderConcurrencyLimiter` or similar) + DI registration (1-2 lines in `ServiceCollectionExtensions.cs`) + constructor injection and 2 call-site edits (acquire/release) in each of the ~11 Claude-calling services + `DeepLTranslationService` + `GeminiTranslationService`. **Total: ~14 files touched** (1 new + 13 edited).
- **Tests:** `ConsensusEngineTests.cs` (`tests/QuantumBuild.Tests.Unit/ToolboxTalks/Validation/ConsensusEngineTests.cs`) mocks all four back-translation services as interfaces and injects `IOptions<TranslationValidationSettings>` only for `MaxRounds` — confirmed **no changes needed** there regardless of approach, since the engine doesn't hold or know about the limiter. Per the existing test-file inventory (from `option-b-multi-provider-recon.md`, still accurate), most individual provider services (`AiQuizGenerationService`, `AiSectionGenerationService`, `AiSlideshowGenerationService`, `ContentTranslationService`, `ContentParserService`, `ClaudeHaikuBackTranslationService`, `ClaudeSonnetBackTranslationService`, `GeminiTranslationService`) have **no unit tests today** — so a semaphore-injection approach adds a constructor parameter with zero existing tests to update, but also zero test coverage validating the new behavior unless new tests are written. `ClaudeTranslationServiceTests.cs` is the one exception with an existing test — would need its `CreateService()` factory updated if that class also gains a limiter dependency. `tests/QuantumBuild.Tests.Integration/ToolboxTalks/TranslationValidationTests.cs` exercises the full validation run lifecycle against a real-ish test harness — worth checking this doesn't start timing out if a very low concurrency limit is set and tests run parallel validation runs (unlikely given Playwright/xUnit test isolation, but worth a smoke-check post-implementation).
- New tests should be added regardless of approach: at minimum, a test proving that the Nth+1 concurrent call to a given provider is delayed (or queued) rather than dispatched immediately, and a test proving a slow/blocked permit doesn't leak (releases on both success and exception paths).

### Special-handling consumers

- **Hangfire background jobs** (`TranslationValidationJob`, `ContentGenerationJob`, `ContentCreationParseJob`, `RequirementIngestionJob`, `RequirementMappingJob`, `CorpusRunJob`) — all share the single `"content-generation"` Hangfire queue (confirmed via `[Queue("content-generation")]` attributes on all 6). A blocked semaphore wait inside one of these jobs is an `await` (non-blocking on the thread pool) but **does** occupy one of Hangfire's `WorkerCount` job execution slots for the duration of the wait — since `WorkerCount` is uncapped in config (defaults to `ProcessorCount * 5`, per the multi-language-slowdown-recon's confirmed finding), this is very unlikely to starve the queue on the dev machine (80 slots) but has a **thinner safety margin on Railway** if the production container has fewer vCPUs (not verified in this recon either, per that doc's own caveat — inherited, not re-verified here). This is a legitimate, if secondary, risk: throttling done correctly is supposed to make jobs *wait longer*, and if enough jobs queue up waiting on the same provider's semaphore, Hangfire's own queue could visibly back up even though nothing is "broken" — this is the intended trade-off of throttling, but worth surfacing to whoever sets the concurrency limit numbers (too low a limit converts an external-API problem into a visible internal-queue-depth problem).
- **Synchronous request-scoped calls** — only one exists in this call graph: `RegulatoryScoreController` calling `RegulatoryScoreService` directly in the HTTP request pipeline (not via a background job). If the Anthropic-provider semaphore is ever saturated by concurrent background job traffic, an admin's live "Regulatory Score" button click would block on the same semaphore and the HTTP response would hang until a permit frees up — a real UX risk if the limit is set aggressively low relative to background job volume. Recommend either a shorter timeout/cancellation-token wired specifically for this one synchronous path, or accepting the (likely rare) wait as a documented trade-off.
- **Batch/bulk operations** — `CorpusRunJob` iterates a `List<AuditCorpusEntry>` sequentially, calling the full validation pipeline (hence the full provider chain) once per entry; a corpus run with many entries would hold/release the same provider permit many times in sequence within one job — this is fine (not a sustained hold), but a corpus run happening *concurrently* with live production translation validation runs would compete for the same semaphore slots, which is exactly the point of a shared per-provider limiter (both are legitimately drawing from the same Anthropic quota) — not a special case needing separate handling, just worth confirming in testing that `persist: false` dry-run calls go through the same limiter as `persist: true` calls (they call the identical `ValidateSectionAsync`/`ConsensusEngine` code path, so they should, automatically, with either placement option from Part 5).

### Risks

- **Blocking Hangfire workers waiting for permits** — covered above; mitigated by `SemaphoreSlim.WaitAsync` (or Polly Bulkhead, which is also async) never blocking an OS thread, only occupying a logical async execution slot. Real risk is queue-depth optics, not actual deadlock or thread starvation.
- **HttpClient handler lifetime vs. semaphore state persistence** — a `SemaphoreSlim` registered as a DI singleton is independent of `IHttpClientFactory`'s handler recycling (2-minute default lifetime) — the semaphore's internal counter persists across handler rotations with zero interaction, since it's a separate object graph entirely. No risk here as long as the semaphore is registered `AddSingleton`, not accidentally scoped to the same lifetime as the typed HttpClient (which is transient) — a transient-scoped "semaphore" would silently provide zero throttling (a fresh semaphore per call defeats the purpose), so this is the single most important implementation detail to get right regardless of which placement option is chosen.
- **Deadlock potential (semaphore held while awaiting DB or other resource)** — per the Note 23 analysis in Part 4, `TranslationValidationJob`'s single shared DbContext is not used concurrently within one job execution, so there's no cross-thread DbContext contention risk from adding a wait. The only risk pattern to avoid by design: don't acquire the provider semaphore and then, while still holding it, await a DB call that itself could be slow/contended — keep the permit's held span limited to "immediately before SendAsync" through "immediately after the response body is read," matching the no-streaming buffered-call pattern confirmed in Part 3.
- **Testability impact on integration tests** — `TranslationValidationTests.cs` integration tests likely run against a `CustomWebApplicationFactory`-style harness with real or faked provider HttpClients; if a low concurrency limit (e.g. 1) is configured and any integration test issues 2+ concurrent validation runs, test wall-clock time increases (waiting for permits) rather than failing outright — worth checking test parallelism settings so CI doesn't silently get slower. Given CLAUDE.md Note 30 documents Playwright E2E tests are already `workers: 1` (`fullyParallel: false`) due to shared-DB safety, there's precedent for this kind of "intentionally serialized for safety" test config, so a similar constraint for provider-limited integration tests wouldn't be an unusual addition.

### Rough hours estimate

- **Polly Bulkhead approach:** 3-5 hours — write/verify the new policy function, wire it into ~15 registration call sites, decide and document the concurrency numbers per provider, write 2-4 new unit tests proving throttling behavior, smoke-test one real validation run locally, update `CustomWebApplicationFactory`/integration test config if the default limit is too aggressive for test parallelism.
- **Explicit semaphore/limiter approach:** 5-8 hours — additional class design work (interface + implementation + registration), more call sites touched (constructor + 2 call-site edits per service across ~13 files vs. registration-only), otherwise same testing/verification scope as above.
- Either way, the actual "which numbers to use per provider" (Anthropic vs. DeepL vs. Gemini concurrency ceilings) is a **product/ops decision requiring input on Anthropic's actual rate-limit tier**, not purely an engineering estimate — recommend surfacing that as an open question for whoever scopes the implementation chunk, since it materially affects both approaches' correctness (too permissive = no protection; too restrictive = the Hangfire-queue-depth risk above becomes real).

---

## Appendix — Files referenced in this recon

- `src/Core/QuantumBuild.Core.Application/Http/ResiliencePolicies.cs`
- `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs`
- `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs`
- `src/QuantumBuild.API/Program.cs` (lines 80-230 read in full)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/ServiceCollectionExtensions.cs` (read in full)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ConsensusEngine.cs` (read in full)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ClaudeHaikuBackTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/GeminiTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DeepLTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiUsageLogger.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AnthropicResponseParser.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs` (targeted reads + grep)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/CorpusRunJob.cs` (targeted reads)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/CostEstimationService.cs`
- `docs/multi-language-slowdown-recon.md` (prior recon, verified and cross-referenced)
- `docs/option-b-multi-provider-recon.md` (prior recon, verified — migration confirmed complete)
- `tests/QuantumBuild.Tests.Unit/ToolboxTalks/Validation/ConsensusEngineTests.cs`
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/TranslationValidationTests.cs`
