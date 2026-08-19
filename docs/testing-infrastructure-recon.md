# Testing Infrastructure Recon

Date: 2026-08-17
Branch: transval
Scope: read-only assessment, no code or test changes made.

## Executive Summary

- Backend has real, substantial test infrastructure: 745 xUnit `[Fact]`/`[Theory]` methods across ~60 files in `tests/QuantumBuild.Tests.Integration` running against a genuine Postgres instance (Testcontainers) through the real ASP.NET pipeline (`WebApplicationFactory<Program>`), plus 282 methods across 19 files in `tests/QuantumBuild.Tests.Unit` running fully mocked, no DB, no HTTP.
- Hangfire jobs ARE directly unit/integration-testable today: five distinct job classes are invoked via `ActivatorUtilities.CreateInstance<TJob>(scope.ServiceProvider)` followed by a direct `job.ExecuteAsync(...)` call, explicitly documented in test comments as mirroring Hangfire's own activator. See `tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs:121-125`.
- That pattern proves a job's internal logic against a real database, but it never exercises Hangfire's actual runtime (enqueue, storage, dequeue, worker pickup, retry). `CustomWebApplicationFactory` deliberately disables the Hangfire server in tests (`AddHangfireServer()` is commented out at `tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs:144-145`), so no automated backend test today proves "an enqueued job actually gets picked up and run."
- Frontend E2E (Playwright) exists and mostly matches what CLAUDE.md documents (3-project structure, workers locked to 1, `auth.setup.ts`/`login-page.spec.ts`/`login-flow.spec.ts`), but CLAUDE.md's coverage list is stale: three more authenticated specs exist that are not mentioned (`learning-wizard-pdf.spec.ts`, `regulatory-document-sector-gate.spec.ts`, `tenant-creation.spec.ts`).
- Exactly one test anywhere in the repository drives a real UI action through a real Hangfire-dispatched background job (real `BackgroundJob.Enqueue<TranslationValidationJob>`, real external AI/translation providers, real Cloudflare R2 upload) to a UI-visible completion state: `web/e2e/authenticated/learning-wizard-pdf.spec.ts`. It is explicitly marked "ON-DEMAND ONLY, not wired into CI," costs roughly $2 to $5 per run in real API spend, and takes 5 to 7 minutes wall clock.
- There is no CI pipeline of any kind in this repository. No `.github/workflows`, no other CI config file was found anywhere in the repo root. Backend's 1,027 xUnit tests and the frontend's Playwright/Vitest suites all run only when a developer runs them locally.
- CLAUDE.md does not mention Vitest at all, but a frontend unit-test layer exists (`web/vitest.config.ts`, 6 spec files, ~466 lines) covering pure logic and hooks only. `@testing-library/react` is installed but no test file actually renders a component.
- The project's own internal standards document states the E2E suite "has known drift" and explicitly tells feature chunks not to add to it (`docs/PHASE_5_STANDARDS.md:487-490`), which is itself evidence supporting leadership's assessment.
- Biggest gap: nothing connects "a user clicks a button in a real browser" to "a real Hangfire-dispatched job finishes and the result is verifiable" in a way that is fast, free, and runs on every change. The one spec that does this exists but is deliberately excluded from any repeatable gate.
- Recommended first slice (justified in Section 3): the schedule-processing / recurring-refresh flow, run as an API-level (no UI) test using the already-proven `ActivatorUtilities` + real Hangfire server pattern, is the cheapest way to close the "job actually gets picked up and runs" gap before investing in full UI E2E for it.

---

## Section 1: What Test Infrastructure Exists Today

### 1.1 Test projects in the solution

The solution (`QuantumBuild.sln`) references three test projects under `tests/`, none of which appear in `web/`'s own separate JS test tooling (covered in 1.3):

| Project | csproj | Purpose |
|---|---|---|
| `QuantumBuild.Tests.Common` | `tests/QuantumBuild.Tests.Common/QuantumBuild.Tests.Common.csproj` | Shared test-data builders and the integration test tenant seeder. Not a runnable test project itself (no `[Fact]` methods); referenced by both other projects. |
| `QuantumBuild.Tests.Unit` | `tests/QuantumBuild.Tests.Unit/QuantumBuild.Tests.Unit.csproj` | Pure unit tests: Moq mocks, AutoFixture, no DB, no HTTP, no DI container. |
| `QuantumBuild.Tests.Integration` | `tests/QuantumBuild.Tests.Integration/QuantumBuild.Tests.Integration.csproj` | Full-stack backend integration tests: Testcontainers Postgres, `WebApplicationFactory<Program>`, real HTTP pipeline, real EF Core, real SignalR hubs (in-process), Hangfire with in-memory storage and no worker. |

`QuantumBuild.Tests.Common` contents (`tests/QuantumBuild.Tests.Common/`):
- `Builders/EmployeeBuilder.cs`, `Builders/ScheduledTalkBuilder.cs`, `Builders/ToolboxTalkBuilder.cs` — object-mother style entity builders used by both Unit and Integration tests.
- `TestTenant/TestTenantConstants.cs`, `TestTenant/TestTenantSeeder.cs` — fixed GUIDs and a seeding routine for a dedicated test tenant, used exclusively by integration tests (never the production `QUANTUMBUILD` tenant, by explicit design; see `CustomWebApplicationFactory.cs:328-338`).

**QuantumBuild.Tests.Unit** (`tests/QuantumBuild.Tests.Unit/QuantumBuild.Tests.Unit.csproj:22-25`): depends on Moq, FluentAssertions, AutoFixture/AutoFixture.Xunit2. Roughly 282 `[Fact]`/`[Theory]` methods across 19 `.cs` files (counted via `grep -rE "\[Fact\]|\[Theory\]"`), including:
- `ToolboxTalks/Validation/ConsensusEngineTests.cs`, `LexicalScoringServiceTests.cs`, `SafetyClassificationServiceTests.cs`, `WordDiffServiceTests.cs`, `SentenceSplitterTests.cs`, `WordToSentenceMapperTests.cs`, `BackTranslationSelectorTests.cs`, `DiffRunGrouperTests.cs` — pure algorithmic scoring/diff logic.
- `ToolboxTalks/Subtitles/ClaudeTranslationServiceTests.cs`, `ElevenLabsTranscriptionServiceTests.cs`, `GitHubSrtStorageProviderTests.cs`, `SrtGeneratorServiceTests.cs`, `SubtitleProcessingOrchestratorTests.cs` — service classes tested with mocked `HttpMessageHandler`/dependencies, no real network calls.
- `ToolboxTalks/SendRefresherRemindersJobTests.cs`, `SendToolboxTalkRemindersJobTests.cs` — Hangfire job classes constructed directly with Moq mocks (see Section 1.4, Pattern 2).
- `ToolboxTalks/Regulatory/*` — structure-map and coverage-verifier logic.
- `Core/BulkImport/BulkEmployeeImportValidationServiceTests.cs`, `Infrastructure/MigrationStructureTests.cs`, `Application/Http/ResiliencePoliciesTests.cs`, `Builders/*Tests.cs`.

This layer exercises unit-level branching logic only. It never touches a real database, real HTTP client, or the DI container the app actually runs under.

**QuantumBuild.Tests.Integration** (`tests/QuantumBuild.Tests.Integration/QuantumBuild.Tests.Integration.csproj:22-27`): depends on `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`, `Respawn`, `Hangfire.MemoryStorage`, `Microsoft.AspNetCore.SignalR.Client`. Roughly 745 `[Fact]`/`[Theory]` methods across ~60 files, organized as:
- `Core/*` — `AuthenticationTests.cs`, `AuthorizationTests.cs`, `CompanyTests.cs`, `ContactTests.cs`, `EmployeeTests.cs`, `SiteTests.cs`, `SupervisorAssignmentTests.cs`, `TenantIsolationTests.cs`, `UserTests.cs` — CRUD + auth/permission/tenant-scoping tests for the Core module, driven through real HTTP requests against the in-process `TestServer`.
- `ToolboxTalks/*` — the bulk of the suite (~50 files): scheduling, course composition, certificate issuance, subtitle processing, translation validation, regulatory ingestion/mapping, requirement mapping, dashboards, workflow state machines, and the job-invocation tests covered in Section 1.4.
- `Api/HealthCheckTests.cs`, `Setup/SmokeTests.cs`, `SmokeTests.cs` (root) — basic wiring/connectivity smoke tests (health check, swagger reachability, seeded-data presence, role-based access spot checks).
- `ExternalReview/ExternalReviewControllerTests.cs`, `Workflows/TranslationWorkflowServiceTests.cs`, `Infrastructure/SeederBehaviourTests.cs`.
- `Setup/Fakes/*` — nine fake service implementations (detailed in 1.2).

### 1.2 The integration test harness in detail

The harness is `tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs` (497 lines), a `WebApplicationFactory<Program>` subclass, paired with `IntegrationTestBase.cs` (base class for test classes) and `IntegrationTestCollection.cs` (xUnit collection fixture so one factory/container is shared across the whole run).

**Database.** A `PostgreSqlBuilder` spins up a real `postgres:16-alpine` container per test run (`CustomWebApplicationFactory.cs:40-45`). The connection string is injected into configuration before `Program.cs` runs (lines 111-124), and EF Core migrations are applied once via `appContext.Database.Migrate()` (lines 195-204). Between test **classes** (not test methods), `Respawner` (Respawn library) truncates all tables except a fixed allowlist (`Roles`, `Users`, `Permissions`, `Tenants`, lookup tables, etc., lines 272-292), and the test tenant is reseeded via `TestTenantSeeder` (lines 296-297, 344-356). `IntegrationTestBase.InitializeAsync` calls `Factory.ResetDatabaseAsync()` at the start of every test class (`IntegrationTestBase.cs:29-33`) — so isolation is per-class, not per-test-method; tests within the same class share state unless they clean up after themselves.

**Hangfire.** Configured explicitly for tests, differently from production. `GlobalConfiguration.Configuration.UseMemoryStorage()` is called early in `ConfigureWebHost` (`CustomWebApplicationFactory.cs:105`) specifically "to prevent PostgreSQL storage initialization." Then all Hangfire service descriptors registered by `Program.cs` are stripped out and Hangfire is re-added with `UseMemoryStorage()` (lines 128-142). Critically, `services.AddHangfireServer()` is never called; the line is present but commented out with the comment "we don't need background job processing" (lines 144-145). This means: in the integration test process, jobs can be enqueued into in-memory storage, but nothing ever dequeues or executes them automatically. Any job execution in this test suite happens because test code calls the job's `ExecuteAsync` method directly (Section 1.4).

**External HTTP dependencies.** Nine fake services are registered to replace every external HTTP integration (`CustomWebApplicationFactory.cs:211-250`):
- `IEmailService` → `FakeEmailService`
- `IToolboxTalkEmailService` → `FakeToolboxTalkEmailService`
- `ITranscriptionService`, `ITranslationService`, `ISrtStorageProvider`, `IVideoSourceProvider`, `ISubtitleProgressReporter` → subtitle-pipeline fakes (ElevenLabs/Claude substitutes)
- `IR2StorageService` → `FakeR2StorageService`
- `IContentParserService`, `IPdfExtractionService`, `IDocxExtractionService`, `IAiQuizGenerationService` → content-creation/AI fakes
- `ITranslationValidationService` → `FakeTranslationValidationService`, which "returns deterministic Pass results and preserves existing reviewer decisions exactly as the real service does" (comment at lines 246-248)

So: no real calls to Claude, DeepL, Gemini, ElevenLabs, Cloudflare R2, or MailerSend occur anywhere in the backend integration suite. This buys speed and determinism but means the integration suite gives zero coverage of what those real providers actually do or how the app behaves under their real latency/failure modes (that gap is only closed by the one Playwright spec discussed in Section 1.3/2.1).

**SignalR.** Is tested, but only at the connection/subscription level, in-process. `SubtitleProcessingHubTests.cs` connects a real `HubConnection` using `Factory.Server.CreateHandler()` as the `HttpMessageHandlerFactory` (`tests/QuantumBuild.Tests.Integration/ToolboxTalks/SubtitleProcessingHubTests.cs:15-26`), i.e. talking to the same in-process `TestServer`, not over a real network socket. This proves hub wiring and auth work, not real-world SignalR behavior (reconnects, network drops, etc. — those are only handled by frontend reconnect logic noted in CLAUDE.md, untested here).

**Auth.** JWTs are minted directly by the factory (`GenerateTestToken`, lines 403-496) for most tests, bypassing the real `/api/auth/login` endpoint. A handful of tests (e.g. `AuthenticationTests.cs`, and the `GetLoginAuthenticatedClientAsync` helper at `IntegrationTestBase.cs:274-301`) do exercise the real login endpoint end to end.

**What this harness does NOT set up:** no real Hangfire worker/dashboard, no real SignalR network transport, no real external AI/translation/storage/email providers, no browser or any UI-layer concept at all (it talks to the API over `HttpClient` against an in-process `TestServer`).

### 1.3 Frontend test setup

**Playwright.** `web/playwright.config.ts` defines exactly what CLAUDE.md's Note 30 describes: `testDir: "./e2e"`, `fullyParallel: false`, `workers: 1` (`playwright.config.ts:5-6`), and three projects (`playwright.config.ts:16-37`):
- `setup` matches `auth.setup.ts`, logs in as SuperUser via the real UI, saves session state to `e2e/.auth/superuser.json`.
- `unauthenticated` matches top-level `e2e/*.spec.ts` files (currently just `login-page.spec.ts`).
- `authenticated` runs everything under `e2e/authenticated/`, pre-loading the saved SuperUser session, and depends on `setup`.

Two `webServer` entries (lines 39-60) auto-spawn `npm run dev` (Next.js) and `dotnet run --project ../src/QuantumBuild.API --launch-profile http` (the real .NET API, with real Hangfire, real Postgres from local dev config), reusing already-running instances outside CI.

Actual spec inventory as of this recon (`web/e2e/`):
- `auth.setup.ts` — matches CLAUDE.md exactly: logs in via `data-testid="login-email-input"` / `login-password-input"` / `login-submit-button"`, waits for `/admin/tenants`, persists storage state (`web/e2e/auth.setup.ts:13-26`).
- `login-page.spec.ts` — matches CLAUDE.md: asserts the login page renders (brand text, email/password fields, Sign In button) (`web/e2e/login-page.spec.ts:1-13`).
- `authenticated/login-flow.spec.ts` — matches CLAUDE.md: confirms the saved SuperUser session reaches `/admin/tenants` and that `/api/auth/me` reports `isSuperUser: true` (`web/e2e/authenticated/login-flow.spec.ts:9-29`).
- `authenticated/tenant-creation.spec.ts` (129 lines, **not in CLAUDE.md**) — three tests: create a tenant with contact details (asserts a linked admin user actually exists in the DB via a real follow-up `GET /api/users` call, lines 61-76), create a tenant without contact details, and a client-side validation-rejection case. Exercises real API + real dev DB.
- `authenticated/regulatory-document-sector-gate.spec.ts` (66 lines, **not in CLAUDE.md**) — one test verifying that ingestion is gated on an attached regulatory sector, not just picker selection; drives a real create-document dialog and a real attach-sector action against the real API.
- `authenticated/learning-wizard-pdf.spec.ts` (452 lines, **not in CLAUDE.md**) — see Section 2.1, the most significant spec in the repo for this recon's purposes.

**Discrepancy from CLAUDE.md:** the 3-spec inventory CLAUDE.md documents (Note 30) is accurate as far as it goes but is stale; three additional, materially more significant specs exist and are undocumented there.

**Vitest (not mentioned anywhere in CLAUDE.md).** `web/package.json` defines `"test": "vitest run"` and `"test:watch": "vitest"`. `web/vitest.config.ts` runs in `jsdom` with `@testing-library/jest-dom` set up (`vitest.config.ts:1-18`). Six spec files exist, ~466 lines total:
- `src/__tests__/framework-smoke.test.ts` (13 lines)
- `src/app/external-review/[token]/__tests__/page.helpers.test.ts` (48 lines)
- `src/features/toolbox-talks/components/learning-wizard/lib/__tests__/stepOrder.test.ts` (179 lines)
- `src/features/toolbox-talks/hooks/__tests__/useCoursePreference.test.ts` (71 lines)
- `src/features/toolbox-talks/hooks/__tests__/useWizardPreference.test.ts` (71 lines)
- `src/features/toolbox-talks/lib/__tests__/reviewCoverage.test.ts` (84 lines)

All six test pure logic or hooks (step-reachability rules, wizard/course preference resolution, review-coverage calculation, a helper function). `@testing-library/react` (`^16.3.2`) and `@testing-library/user-event` are installed as dependencies but a repo-wide search for `render(` / `@testing-library/react` imports inside test files found zero matches; no component is actually mounted and rendered in a test anywhere in the frontend. No Storybook or component test-runner is configured.

**`data-testid` convention.** Present in only 3 component files (`grep -rl "data-testid" src --include="*.tsx"`): `src/app/login/page.tsx`, `src/app/external-review/[token]/page.tsx`, `src/features/toolbox-talks/components/SubtitleProcessingPanel.tsx` (42 occurrences total). Of these, only the login page's test IDs are actually consumed by a spec (`auth.setup.ts:18-20`). Every other spec relies on ARIA role/label/text-based selectors (`getByRole`, `getByLabel`, `getByText`), and the specs' own inline comments repeatedly document real selector fragility discovered while writing them: a Radix `Select` trigger with no accessible name reachable via `getByLabel` (`learning-wizard-pdf.spec.ts:219-229`, `regulatory-document-sector-gate.spec.ts:52-55`), and a substring name collision between "Document" (PDF mode) and "Word Document" (Docx mode) buttons requiring an anchored regex (`learning-wizard-pdf.spec.ts:178-186`).

### 1.4 Hangfire job testing patterns (the crux finding)

Three distinct construction patterns exist across the two backend test projects. All three call a job's `ExecuteAsync` method directly from test code; none go through Hangfire's actual `BackgroundJob.Enqueue` → storage → worker dequeue → execute pipeline.

**Pattern 1: `ActivatorUtilities.CreateInstance` in a fresh DI scope, integration project.** This is the pattern explicitly described (and reused) as mirroring Hangfire's own activator:

```csharp
// tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs:121-125
using (var jobScope = Factory.Services.CreateScope())
{
    var job = ActivatorUtilities.CreateInstance<ProcessToolboxTalkSchedulesJob>(jobScope.ServiceProvider);
    await job.ExecuteAsync(CancellationToken.None);
}
```

The same file defines a reusable private helper doing the same thing (`RecurringScheduleRefreshCharacterisationTests.cs:535-540`), and its class-level doc comment states this directly: "Run the actual job, resolved via ActivatorUtilities (mirroring Hangfire's own job activator), with NO manual tenant-context workaround anywhere in this test" (lines 119-121). This is the test that specifically proves the Chunk 2a tenant-context fix (`ExecuteAsync_NoManualTenantContextWorkaround_ProcessesDueScheduleAndCreatesScheduledTalk`, lines 98-133): the job is built with zero HttpContext and zero pre-set tenant context, exactly as Hangfire's cron dispatch would construct it, and the test asserts the resulting `ScheduledTalk` row exists in the real (Testcontainers) Postgres database.

The identical pattern (same construction, same "mirrors Hangfire" rationale) recurs in:
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/MissingTranslationsJobTests.cs:110,136` — for `MissingTranslationsJob`. Class doc comment: "Dispatch strategy: resolve MissingTranslationsJob via ActivatorUtilities so all real dependencies (SignalR hub context, ISender, etc.) are injected from the test host, same pattern as Hangfire's AspNetCoreJobActivator" (lines 17-19).
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/BulkLearningTranslationSweepJobTests.cs:131` — for `BulkLearningTranslationSweepJob`.
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/BulkSopImportJobTests.cs` — doc comment: "Hangfire's background server is disabled in tests (see CustomWebApplicationFactory), so the job is invoked directly afterwards, the same pattern MissingTranslationsJobTests uses for its job" (lines 17-20). This test also proves a real production bug fix: a job-scope tenant-context defect that caused every wizard command handler call inside `BulkSopImportJob.ProcessItemAsync` to fail with "Learning not found" (lines 22-28).

What this pattern proves: the job's real `ExecuteAsync` logic, real command/query handlers, real EF Core query filters (including tenant-scoping), running against a real Postgres database, with only external HTTP calls faked. What it does not prove: that Hangfire itself will ever call that `ExecuteAsync` method in the deployed app. `BackgroundJob.Enqueue<T>` is never invoked by these tests, and no assertion anywhere checks that the job was actually dequeued and run by a worker.

**Pattern 2: direct construction with Moq mocks, unit project.** No DI container, no real DbContext, no HTTP:

```csharp
// tests/QuantumBuild.Tests.Unit/ToolboxTalks/SendRefresherRemindersJobTests.cs:18-29
private readonly Mock<IToolboxTalksDbContext> _dbContextMock = new();
private readonly Mock<ITenantRepository> _tenantRepositoryMock = new();
private readonly Mock<IToolboxTalkEmailService> _emailServiceMock = new();
private readonly Mock<ILogger<SendRefresherRemindersJob>> _loggerMock = new();

private SendRefresherRemindersJob CreateJob() => new(
    _dbContextMock.Object,
    _tenantRepositoryMock.Object,
    _emailServiceMock.Object,
    _loggerMock.Object);
```

`ExecuteAsync` is then called directly on this fully-mocked instance (line 78). The same pattern appears in `tests/QuantumBuild.Tests.Unit/ToolboxTalks/SendToolboxTalkRemindersJobTests.cs`. `DbSet<T>` behavior is synthesized via a `MockDbSetHelper` (referenced at `SendRefresherRemindersJobTests.cs:57-58`). This is the fastest, most isolated pattern; it proves only a job's internal branching against synthetic in-memory data, with no database, no query filters, no tenant plumbing exercised at all.

**Pattern 3: direct construction with a real (but manually-scoped) DbContext, integration project.**

```csharp
// tests/QuantumBuild.Tests.Integration/ToolboxTalks/StaleIngestionSweepJobTests.cs:62,81,104
var job = new StaleIngestionSweepJob(context, NullLogger<StaleIngestionSweepJob>.Instance);
```

Used where the job has few enough constructor dependencies (`IToolboxTalksDbContext` plus a logger) that `ActivatorUtilities` isn't needed; `context` is still a real EF Core context resolved from a test scope against the real Testcontainers Postgres instance.

**Summary of the crux finding:** direct job invocation via `ActivatorUtilities` (Pattern 1) is an established, deliberately-documented, repeatedly-reused convention, not a one-off. Five distinct job classes across four test files use it. It genuinely proves a job's business logic end to end against a real database. It does not, and structurally cannot (because `AddHangfireServer()` is never called in tests, `CustomWebApplicationFactory.cs:144-145`), prove that Hangfire's actual runtime will pick up and execute an enqueued job. Any regression in Hangfire configuration itself (queue names, storage connection, retry policy, serialization, recurring-job cron registration in `Program.cs:467-478`) has zero automated test coverage anywhere in the repository.

---

## Section 2: The Gaps for Full-Flow Testing

### 2.1 Can the UI be driven today against a real API and DB? Can it assert on backend state? Can it trigger and await a real Hangfire job?

**Can the UI be driven against a real API + DB?** Yes, and this is proven, not theoretical. `playwright.config.ts:39-60` auto-spawns the real `dotnet run` API process and the real Next.js dev server; three authenticated specs (`tenant-creation.spec.ts`, `regulatory-document-sector-gate.spec.ts`, `learning-wizard-pdf.spec.ts`) drive real pages against this real stack, hitting whatever Postgres database the developer's local `appsettings.Development.json` points to (the local dev DB, not a disposable per-run container the way the backend integration suite works).

**Can such a test assert against backend/DB state?** Yes, demonstrated concretely in `tenant-creation.spec.ts:52-76`: after creating a tenant through the UI form, the test extracts the JWT from `localStorage`, makes a raw `GET /api/users` call with an `X-Tenant-Id` header, and asserts the expected admin user row exists. This is UI-driven action followed by direct backend-state verification, done today, working.

**Can it trigger AND await a Hangfire job's completion?** Yes, exactly once, in `learning-wizard-pdf.spec.ts`. The "Start All" click in Step 5 (`learning-wizard-pdf.spec.ts:343-345`) hits an endpoint that calls `BackgroundJob.Enqueue<TranslationValidationJob>` for real (confirmed at `src/QuantumBuild.API/Controllers/TranslationValidationController.cs:99`), against the real Postgres-backed Hangfire storage the dev API process is running (`Program.cs:163-174`, unconditional, no test/dev branch), picked up by the real `AddHangfireServer()` worker in that same process. The test then polls the UI for a real state transition ("Validated" text becoming visible, `expect(...).toBeVisible({timeout: 8*60_000})`, lines 356-358) that only occurs once the job has actually run to completion through a real multi-round back-translation consensus against real Claude/DeepL/Gemini APIs. This is the one and only place in the entire test estate (backend or frontend) where an enqueued Hangfire job's real runtime execution is observed and asserted on by any test.

The catch: this spec is explicitly marked "ON-DEMAND ONLY, not wired into CI" (line 4), costs real money per run ("roughly $2-5 per run, dominated by the multi-round back-translation consensus," lines 8-9), and takes 5 to 7 minutes wall clock (line 9). It is not runnable in any automated, repeatable gate today, and there is no CI to wire it into even if someone wanted to (Section 2.2).

### 2.2 Hangfire-in-test-mode options: what does the codebase already support, and what would each option cost?

`Program.cs` configures Hangfire unconditionally, no environment branch:

```csharp
// src/QuantumBuild.API/Program.cs:163-174
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

No `if (app.Environment.IsEnvironment("Testing"))` or similar branch exists around this block. The Hangfire dashboard is the only piece gated by environment (`Program.cs:461-465`, development only, for security). This means the integration test harness has to actively override Hangfire at the WebApplicationFactory level rather than relying on any built-in test mode; it does so as described in Section 1.2 (`UseMemoryStorage()`, strip and re-add Hangfire descriptors, never call `AddHangfireServer()`).

Given this architecture, the realistic options, ranked by how much new plumbing each needs:

1. **Direct `ExecuteAsync` invocation (already proven, least new plumbing).** This is Pattern 1 from Section 1.4. Zero new infrastructure required; it's the existing convention. Downside: never exercises Hangfire's own dispatch, so it cannot catch a Hangfire configuration/wiring regression (wrong queue name, broken recurring-job registration, serialization failure on a real job argument type, etc.).
2. **Enable `AddHangfireServer()` with in-memory storage inside `CustomWebApplicationFactory`, then poll for job completion.** Moderate new plumbing: the harness already builds and configures `MemoryStorage` (`CustomWebApplicationFactory.cs:140-142`); adding a real `AddHangfireServer()` call and a polling helper (`JobStorage.Current.GetMonitoringApi()` state checks) would close the "does Hangfire's own dispatch work" gap without needing a real Postgres-backed Hangfire schema. This is a real option that fits the codebase's existing shape closely, since half of it (`MemoryStorage`) already exists.
3. **Polling a real Hangfire queue (Postgres-backed) until job completion, as `learning-wizard-pdf.spec.ts` effectively does today via UI polling.** This is what the on-demand Playwright spec already proves works, but only manually/expensively at the UI layer with real external providers. Doing the equivalent at the API level (no UI, no browser) would need: a way to point tests at the real dev API process (or spin one up), and a way to fake the external AI/translation providers for that process specifically (the dev process does not have the Fakes wired in the way `CustomWebApplicationFactory` does), which is nontrivial new plumbing.
4. **A test-mode flag that runs Hangfire jobs synchronously inline (e.g. a custom `IBackgroundJobClient` that calls `.Execute()` immediately instead of enqueuing).** Not present anywhere in the codebase today. Would require introducing an abstraction the app doesn't currently have (all call sites use Hangfire's static `BackgroundJob.Enqueue<T>` directly, e.g. `TranslationValidationController.cs:99,370,427`, `TranslationJobScheduler.cs:18`, `ContentCreationSessionService.cs:707`), so this is the most new plumbing of the four options and would touch production code paths, not just test infrastructure.

Given the existing shape of the code, Option 1 is already fully in place, and Option 2 is the natural next step: it reuses the harness's existing `MemoryStorage` setup and would be additive (no production code changes), closing the specific gap of "does Hangfire's own runtime dispatch actually get exercised by any automated test."

---

## Section 3: What a Reliable Foundation Would Look Like

### 3.1 Three layers, honestly costed against this codebase's actual shape

**Layer 1: Backend integration tests (have this today).** `WebApplicationFactory` + Testcontainers Postgres + direct job invocation, as described in Section 1. Catches: permission/tenant-isolation regressions, command/query handler bugs, EF Core query filter mistakes, workflow state-machine defects, job business logic bugs (given a real DB). Does not catch: Hangfire runtime wiring, real external-provider behavior (rate limits, timeouts, format drift from Claude/DeepL/Gemini/ElevenLabs), SignalR over a real network, anything about the actual rendered UI, or R2/Cloudflare integration specifics (all faked). Maintenance cost: moderate. The suite is large (1,027 tests) and already shows the kind of "drift" the project's own standards doc flags (`PHASE_5_STANDARDS.md:467-471`, "the test suite has accumulated drift... the right time to address it comprehensively is after Phase 5 ships"). Multi-tenancy is well exercised here (`Core/TenantIsolationTests.cs`, plus explicit `IgnoreQueryFilters()` assertions scattered through the ToolboxTalks tests) since tenant scoping is a first-class, previously-buggy concern in this codebase (see CLAUDE.md Note 14).

**Layer 2: Full-flow API-level tests (API to DB to job, no UI, don't currently exist as a distinct thing).** Would sit between the current integration suite and Playwright: same `WebApplicationFactory`/Testcontainers foundation, but with `AddHangfireServer()` actually enabled (Option 2 in Section 2.2) so tests assert "I called the enqueue endpoint, and polling shows the job really ran," rather than calling `ExecuteAsync` directly. Catches exactly the gap identified in Section 1.4/2.2: Hangfire wiring/dispatch regressions, and (with the existing Fakes for external providers still in place) fast, free, deterministic proof that "trigger job via API, it runs, result lands in DB" works end to end. This is the natural next investment given how close the current harness already is (MemoryStorage already configured, Fakes already exist, only the disabled worker needs enabling plus a poll-until-done helper). Maintenance cost: low to moderate, since it is additive to infrastructure that already exists and is well understood by whoever maintains the current integration suite.

**Layer 3: UI E2E (Playwright, partially exists, mostly unused).** Catches what nothing else can: real selector/accessibility regressions, real multi-step wizard state persistence across page loads, and (only in the one on-demand spec) real external-provider behavior end to end. This is also the most expensive layer for this codebase specifically, because of two properties unique to it: (a) the flagship business flow (learning creation/translation/validation) genuinely requires several real paid external API calls with multi-minute real latency to observe a true positive, which no amount of engineering effort removes without building a parallel fakeable-dev-server story that does not exist today; and (b) the app's own Radix/shadcn component choices already produce documented, real accessibility gaps (missing accessible names on `Select` triggers, ambiguous button names) that make ordinary Playwright locators fragile, evidenced repeatedly in the specs' own inline comments (Section 1.3). Maintenance cost: high, and the project has already said so about itself (`PHASE_5_STANDARDS.md:487-490`: "Add Playwright E2E coverage as a Phase 5 deliverable. The existing E2E suite has known drift; adding to it now is premature").

### 3.2 Maintenance reality for UI E2E specifically

Given what exists today (Section 1.3), a larger Playwright suite would stay maintainable only if it addressed two concrete, already-visible problems rather than just adding more specs:

1. **No `data-testid` convention beyond the login page.** 42 occurrences across exactly 3 files, and only the login page's IDs are actually used by a spec. Every other spec (including the two non-AI ones) reaches for `getByRole`/`getByLabel`/`getByText`, and the specs themselves document real breakage points from this choice: a Radix `Select` trigger whose accessible name never resolves via `getByLabel` because the `FormLabel`'s `htmlFor` doesn't reach the nested Radix button (`learning-wizard-pdf.spec.ts:219-229`, `regulatory-document-sector-gate.spec.ts:52-55`), and a substring name collision between two mode-selector buttons that required an anchored regex workaround (`learning-wizard-pdf.spec.ts:178-186`). Extending `data-testid` coverage to the interactive controls in shared components (the `MultiSelectCombobox`, the `Select` wrapper used throughout admin forms) would remove a real, already-encountered source of flakiness, cheaper than continuing to patch around it spec by spec.
2. **The one AI-dependent, real-money spec sets a bad precedent if imitated.** `learning-wizard-pdf.spec.ts` is well-engineered for what it is (extensive diagnostics logging, carefully-tuned timeouts backed by real observed run data, explicit non-CI framing), but it is fundamentally a manual/on-demand smoke test, not a repeatable regression gate. A larger suite needs a clear, enforced line between "fast, free, CI-eligible specs" (the two non-AI authenticated specs are a good model: real API, real dev DB, no external paid providers, seconds not minutes) and "expensive, on-demand-only specs" like this one, or the suite will either become too slow/expensive to run regularly or will silently stop being run at all (which, per `PHASE_5_STANDARDS.md:487-490`, has effectively already happened once).

There is no CI to enforce any of this today (Section 1.3), so "maintainable" currently means "maintained by whichever developer remembers to run it locally," which is the core of leadership's stated concern.

### 3.3 Proposed first slice, and why

**Recommendation: build the schedule/recurring-refresh flow as a Layer 2 (API-level, no UI) full-flow test**, using the exact `ActivatorUtilities` pattern already proven in `RecurringScheduleRefreshCharacterisationTests.cs`, but upgraded to go through a real enqueue-and-poll cycle (Section 2.2, Option 2) instead of calling `ExecuteAsync` directly. Concretely: create a schedule via the real API, call the real `/api/toolbox-talks/schedules/{id}/process` endpoint (or, for the cron path, actually enqueue `ProcessToolboxTalkSchedulesJob` through a real, in-memory-storage-backed `AddHangfireServer()`), poll until Hangfire reports the job `Succeeded`, then assert the resulting `ScheduledTalk` row and any triggered notification exist.

**Why this flow over the alternatives:**

- **Vs. toolbox talk creation through to assignment completion.** This flow is richer end to end (touches video/PDF processing, AI content generation, quiz generation, employee completion, certificate issuance) but that richness is exactly why it is expensive: most of its interesting steps require the same real external-AI-provider dependency that makes `learning-wizard-pdf.spec.ts` a $2-5, 5-7-minute on-demand-only test. Building the harness pattern around this flow first would import that cost into the very first slice, which defeats the point of establishing a fast, repeatable pattern.
- **Vs. employee bulk import.** `BulkEmployeeImportJob` is already a strong reference implementation for a different concern (per-row DbContext isolation, Note 23) and already has integration coverage using the direct-`ExecuteAsync` pattern. It is a good second or third candidate, but it is a single-pass batch job, not a recurring/cadence-driven one; it does not exercise the "does the job get correctly re-triggered on schedule, and not duplicated" class of bug that recurring schedules do, and that class of bug is exactly what has needed fixing twice already in this codebase (the Chunk 2a tenant-context fix and the Chunk 2b cadence/idempotency fix visible in `RecurringScheduleRefreshCharacterisationTests.cs`'s own history).
- **Why recurring schedules specifically best exercises the hard part.** The Hangfire-job-in-test-loop problem is precisely "does a real enqueue reach a real worker and produce a real, correct result, including on a second/duplicate run." The schedule flow already has, in the same test file, purpose-built assertions for exactly that: idempotency on repeat calls (`ProcessCalledTwiceBeforeCadenceInterval_DoesNotCreateDuplicateScheduledTalks`), cadence-respecting due-filtering (`JobDueFilter_DoesNotReselectSchedule_WhileNextRunDateIsInTheFuture`), and manual-vs-cron interaction (`ManualProcessThenCronOnSameCycle_DoesNotDuplicate_ButStillAdvancesCadence`). Upgrading these existing tests (or adding siblings) to go through a real enqueue-and-poll cycle rather than direct invocation would, in one motion, prove both "the job's business logic is correct" (already proven) and "Hangfire's real dispatch delivers that job correctly, including under repeat/duplicate conditions" (currently unproven anywhere), which is the single most valuable, currently-missing piece of coverage identified in this recon. It also requires no external AI/translation spend at all, since scheduling/assignment creation involves no AI calls, keeping the first slice fast and free, the properties Layer 3 in this codebase structurally cannot offer.

---

## Appendix: Files Read During This Recon

- `QuantumBuild.sln`, all `*.csproj` under `tests/` and `src/`
- `tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs`, `IntegrationTestBase.cs`, `IntegrationTestCollection.cs`
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs`, `MissingTranslationsJobTests.cs`, `BulkSopImportJobTests.cs`, `StaleIngestionSweepJobTests.cs` (grep-located sections), `SubtitleProcessingHubTests.cs`
- `tests/QuantumBuild.Tests.Integration/Setup/SmokeTests.cs`, `SmokeTests.cs` (root)
- `tests/QuantumBuild.Tests.Unit/ToolboxTalks/SendRefresherRemindersJobTests.cs`
- `src/QuantumBuild.API/Program.cs` (Hangfire registration, recurring job registration, hub mapping, dashboard gating)
- `src/QuantumBuild.API/Controllers/TranslationValidationController.cs`, `TranslationJobScheduler.cs`, `ContentCreationSessionService.cs` (Hangfire enqueue call sites)
- `web/playwright.config.ts`, `web/vitest.config.ts`, `web/package.json`
- `web/e2e/auth.setup.ts`, `login-page.spec.ts`, `authenticated/login-flow.spec.ts`, `authenticated/tenant-creation.spec.ts`, `authenticated/regulatory-document-sector-gate.spec.ts`, `authenticated/learning-wizard-pdf.spec.ts`
- `docs/PHASE_5_STANDARDS.md` (Section 11, Testing)
- `docs/playwright/playwright-setup-recon.md`, `docs/playwright-wizard-manual-run-observations.md` (prior recon docs, used for corroboration and history, not as primary evidence)
- Repo-wide checks: no `.github/workflows` or other CI config found anywhere in the repository.
