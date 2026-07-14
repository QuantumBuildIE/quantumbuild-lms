# Pre-Push Audit — transval → Demo (rehearsal for → main → Production)

**Date:** 2026-07-18
**Scope:** Read-only recon. No code, config, or database changes were made while producing this report.
**Compared:** `main` (production) vs `transval` (development), merge-base `a38b0a2` (2026-06-05).

---

## 1. Executive summary

| | |
|---|---|
| Commits ahead of main | **227** |
| New migrations | **16 new + 1 pre-existing migration rewritten in place** |
| Files changed | 553 files, +192,117 / −7,527 lines |
| Build check | `dotnet build` on `src/QuantumBuild.API` — **0 errors, 0 warnings** |
| 🔴 Red flag #1 | `AIProviderOptions` — new `ValidateOnStart()` fail-fast validator requires 4 env vars (`AIProviders__Anthropic__Models__Sonnet`, `...Haiku`, `...Gemini__Models__Flash`, `...ElevenLabs__Models__Transcription`). Repo evidence strongly suggests **Demo does not have these set**. If missing, the API will not start at all. |
| 🟡 Watch item #2 | `SubtitleProcessingSettingsValidator` — new fail-fast validator requires `SubtitleProcessing__ElevenLabs__ApiKey`. Probably already set (feature predates transval), but this is now a hard startup crash instead of a soft runtime failure if it's ever missing. |
| 🟡 Watch item #3 | `AddSingleSupervisorUniqueness` migration adds a **unique** partial index on `SupervisorAssignments(TenantId, OperatorEmployeeId) WHERE IsDeleted=false`. Will fail at migration time if Demo's data already has an operator assigned to more than one active supervisor. |
| 🟢 Resolved, not a new risk | `AddQrCodeCourseId` — a migration that **already shipped on main** was rewritten in place on transval to fix a self-duplicating bug (see §2 detail). transval's version is strictly safer than main's; not a regression. |
| Everything else | 15 of the 16 new migrations are simple additive column adds, all either `nullable: true` or `nullable: false` with an explicit `defaultValue` — low risk. |

**Recommended action:** Push Demo, but **first confirm the 4 `AIProviders__*` env vars are set on the Demo Railway service**. See §7.

---

## 2. Commits (Part 1)

227 commits ahead of `main`, none are reverts of each other (no `revert`/`undo` commits found; no A-does-X/B-reverts-X pairs identified). Breakdown by conventional-commit type:

| Type | Count |
|---|---|
| `feat` | 77 |
| `fix` | 56 |
| `docs` | 69 |
| `test` | 15 |
| `chore` / `refactor` | 10 |

Full list: `git log main..transval --oneline`.

### Major feature threads

- **External Review workflow (Phases 1–4.6, Chunks A–F)** — third-party reviewer invitations, decline reasons, per-section provenance/editability, auto-apply of reviewer edits, portal page, cancel/send UI. This is the single largest thread (~60 commits) and is what the new `workflows` schema (§3) exists for.
- **Learning Wizard rebuild (Phase 5, §22–§27)** — new URL-per-step wizard (`/admin/toolbox-talks/learnings/**`) built in parallel with the legacy SPA wizard, gated by the `UseNewWizard` tenant setting (§25/§26 visual polish, §24 talk-detail inline edit chunks, cutover toggle infrastructure).
- **Course creation rework** — "compose-existing" entry point, dead-hook cleanup, Card primitive adoption.
- **DOCX import (§1.1.11)** and **regulatory applicability surface (§1.2.2)**.
- **Multi-provider AI config unification** — two P0 incident fixes: Anthropic Sonnet model deprecation, and ElevenLabs `unsupported_model`. Both converged on the new `AIProviderOptions` registry (Option B, Chunks 1–2). This is the source of Red Flag #1.
- **Provider concurrency bulkheads** — Polly-based per-provider concurrency ceilings (`ProviderConcurrencyOptions`) to prevent one tenant's AI workload from starving others.
- **Supervisor-operator scoping fixes (§3.14–§3.18)** — schedule create/update scoping, single-active-supervisor-per-operator DB constraint.
- **Security fix** — "tighten four TenantEntity query filters" (6659e3e), consistent with the cross-tenant leak pattern already documented in CLAUDE.md Note 14.
- **Playwright E2E suite** — vitest + RTL install, then a full Playwright install with auth setup, wizard happy-path coverage, selector-scoping fixes.
- **Large volume of `docs`/BACKLOG housekeeping** — design docs (`TRANSLATION_WORKFLOW_DESIGN.md`, `LEARNING_LIFECYCLE.md`), recon reports under `docs/`, BACKLOG section renumbering/closure.

None of the 227 commits touch anything on Production-only infrastructure (no `main`-only hotfixes are missing from transval — transval is a strict superset built from the shared 2026-06-05 base).

---

## 3. Migrations (Part 2)

### 3.1 Pre-existing migration rewritten in place — `AddQrCodeCourseId` (id `20260424221512`)

This is the one migration that needed deep investigation: the file exists on **both** `main` and `transval` under the same migration ID, but with different `Up()`/`Down()` bodies (Designer.cs is identical on both — same model snapshot state, so this is a legitimate content-only rewrite, not a corrupt migration).

- **main's version:** creates `QrPin*` columns on `Employees` and creates the `QrLocations`/`QrCodes`/`QrSessions` tables from scratch.
- **transval's version** (commit `e6d3919`, "fix(migrations): rewrite AddQrCodeCourseId to add only CourseId column"): adds only the `CourseId` column + FK + index to the already-existing `QrCodes` table.

**Why:** three earlier migrations in the same original feature branch — `20260424160000_AddEmployeePinFields`, `20260424200000_AddQrLocationAndQrCode`, `20260424210000_AddQrSession` — already create those columns/tables, and are **identical (same blob hash) on both main and transval**, sequenced *before* `AddQrCodeCourseId` by timestamp. main's version of `AddQrCodeCourseId` therefore duplicates work already done by the three prior migrations — applying main's migration set to a genuinely fresh database would fail with a "column/table already exists" error at `20260424221512`. transval's rewrite removes the duplication and only adds what's actually new (`CourseId`).

**Risk assessment:** Because EF Core's `__EFMigrationsHistory` table tracks applied migrations **by ID string, not by content hash**, this rewrite is inert on any environment where `20260424221512_AddQrCodeCourseId` has already been recorded as applied (regardless of which version ran) — EF will simply skip it on the next deploy. It only matters on an environment that has *never* applied this migration ID yet: there, transval's version will succeed where main's version would have failed. **transval is strictly safer than main here — not a new risk introduced by this push.** Flagging only so the deploy log is understood if it's ever inspected side-by-side with main.

### 3.2 New migrations (16)

All new migrations are additive. None drop columns, rename columns, or transform data.

| Migration | Change | Risk |
|---|---|---|
| `AddWorkflowPrimitives` | Creates new `workflows` schema + 4 tables (`ExternalParticipantInvitations`, `WorkflowEvents`, `WorkflowReviews`) + `TranslationFlags` table in `toolbox_talks` schema, with indexes and FKs. No existing data affected — brand new schema/tables. | Low |
| `AddValidationResultIdToTranslationFlag` | Adds `ValidationResultId` (uuid, **NOT NULL, no default**) + FK + index to `TranslationFlags`. Non-nullable with no default only matters if the table has rows; `TranslationFlags` is created in this same migration set and cannot have pre-existing data on any environment. | Low (safe because the parent table is itself new) |
| `AddExternalReviewContextAndDeclineReason` | Adds nullable `DeclineReason` to `WorkflowReviews`; adds nullable `ContextPayload` + NOT-NULL `ContextType` (`defaultValue: ""`) to `ExternalParticipantInvitations`. | Low |
| `AddLastEditedStepToToolboxTalk` | Adds nullable `LastEditedStep` (int) to `ToolboxTalks`. This is the new-vs-legacy-wizard discriminator (CLAUDE.md Note 29). | Low |
| `AddLearningWizardFieldsToToolboxTalk` | Adds 12 nullable/defaulted columns to `ToolboxTalks` (audit metadata: `AudienceRole`, `AuditPurpose`, `ClientName`, `DocumentRef`, `PreserveSourceWording` default `false`, `ReviewerName/Org/Role`, `SourceFileName/Type/Url`, `SourceText`, `TargetLanguageCodes`). | Low |
| `AddTranscriptWordsJsonToToolboxTalk` | Adds `InputMode` (NOT NULL, `defaultValue: "Text"`) and nullable `TranscriptWordsJson` to `ToolboxTalks`. | Low |
| `AddAllowRetryToToolboxTalk` | Adds `AllowRetry` (bool, NOT NULL, `defaultValue: true`). | Low |
| `AddCoverImageUrlToToolboxTalk` | Adds nullable `CoverImageUrl`. | Low |
| `AddIsNewWizardToTranslationValidationRun` | Adds `IsNewWizard` (bool, NOT NULL, `defaultValue: false`). | Low |
| `AddPublishedAtToToolboxTalk` | Adds nullable `PublishedAt` (DateTime). | Low |
| `AddWizardDefaultsToToolboxTalkSettings` | Adds 5 columns to `ToolboxTalkSettings`, all NOT NULL with defaults matching current hardcoded wizard behaviour (`DefaultAutoAssignDueDays=14`, `DefaultGenerateCertificate=true`, `DefaultIsActive=true`, `DefaultMinimumVideoWatchPercent=90`, `DefaultRefresherFrequency="Once"`). | Low |
| `AddNotificationTogglesToToolboxTalkSettings` | Adds 4 bool columns, all NOT NULL `defaultValue: true`. | Low |
| **`AddSingleSupervisorUniqueness`** | Creates a **unique partial index** `IX_SupervisorAssignments_TenantId_OperatorEmployeeId_Active` on `(TenantId, OperatorEmployeeId) WHERE IsDeleted = false`. | **Medium — data-dependent, see §3.3** |
| `WidenSourceFileTypeColumn` | `AlterColumn` widening `SourceFileType` from `varchar(64)` to `varchar(255)`. Widening a varchar is safe (no truncation risk, no data rewrite needed in Postgres for this direction). | Low |
| `AddExternalReviewProvenanceToTranslations` | Adds nullable `LastExternalReviewedAt`/`LastExternalReviewedBy` to `ToolboxTalkTranslations`. | Low |
| `AddEditableSectionIndicesToExternalReviewInvitation` | Adds nullable `EditableSectionIndicesJson` (text) to `ExternalParticipantInvitations`. | Low |

### 3.3 `AddSingleSupervisorUniqueness` — the one migration that can fail on existing data

Unlike every other migration in this set, this one is not purely additive — it asserts an invariant (**"an operator can have at most one active supervisor"**) against whatever `SupervisorAssignments` rows already exist. If Demo (or later, Production) currently has any operator with two or more non-deleted `SupervisorAssignments` rows within the same tenant, `CREATE UNIQUE INDEX` will fail with a Postgres uniqueness-violation error, and — under migration-on-startup — the API will not start.

This can't be determined from the repo; it depends on live data. Recommend a pre-flight check against Demo's database (see §7) rather than discovering it via a failed deploy.

### 3.4 Down() methods

All 17 migrations' `Down()` methods are the mechanical inverse of their `Up()` (drop what was added). None contain a destructive `Down()` beyond what's expected for a rollback of that specific migration (e.g., dropping the `workflows` schema tables would lose any external-review data recorded since the schema was created — normal and expected for a rollback, not a hidden landmine).

---

## 4. Sequence sanity (Part 3)

- All new migrations that reference newly-created tables/columns are correctly sequenced *after* the migration that creates the dependency (e.g., `AddValidationResultIdToTranslationFlag` (id `20260608...`) references `TranslationFlags`, created by `AddWorkflowPrimitives` (id `20260606...`) — correct order. `WidenSourceFileTypeColumn` (id `20260623...`) alters a column added by `AddLearningWizardFieldsToToolboxTalk` (id `20260610...`) — correct order).
- No migration's `Up()` assumes a schema state that a *later* migration establishes.
- **Designer.cs pairing (CLAUDE.md Note 28 pitfall):** every one of the 16 new migrations has a matching `.Designer.cs` file present. No orphaned `.cs`-without-`.Designer.cs` files were found on transval.
- `ApplicationDbContextModelSnapshot.cs` is updated and consistent with the new migration set (confirmed by a clean `dotnet build` — see §1).

---

## 5. Config-level changes (Part 4)

Three new `IValidateOptions<T>` fail-fast startup validators exist on transval that do not exist on main at all (grepped — zero hits on main for all three):

### 5.1 `ProviderConcurrencyOptions` — 🟢 safe
Binds to `ProviderConcurrency`. Every property has a baked-in C# default (Anthropic 5/20/30s, DeepL 10/40, Gemini 5/20). The validator only fails on an *explicitly set* invalid value (e.g., `MaxConcurrency <= 0`). A missing config section never fails startup — confirmed by reading both the options class and its validator, which both document this intent in their XML comments.

### 5.2 `AIProviderOptions` — 🔴 the top risk in this push
Binds to `AIProviders`. Unlike `ProviderConcurrencyOptions`, its four leaf properties (`Anthropic.Models.Sonnet`, `Anthropic.Models.Haiku`, `Gemini.Models.Flash`, `ElevenLabs.Models.Transcription`) default to `string.Empty` — there is no safe fallback. `AIProviderOptionsValidator` fails startup if any of the four is blank.

This validator is new work from this transval cycle (P0 incidents `832abde` "Anthropic model deprecation" and `8733c86` "ElevenLabs unsupported_model", closed via "Option B" chunks `a2dce8b`/`53ea063`). BACKLOG.md's own entry for this work states explicitly:

> "**Railway env vars:** Ensure `AIProviders__Anthropic__Models__Sonnet` and `AIProviders__Anthropic__Models__Haiku` (plus Gemini Flash and ElevenLabs Transcription) are set in Railway **Production and Development** services before deploy."

Demo is conspicuously **not named** in that checklist. Combined with the separate BACKLOG/CLAUDE.md entry noting Demo is currently disconnected from auto-deploy and has a stale, unrelated "14 missing env vars" list (§7 below) dated *before* this AIProviders work landed, there is no evidence these 4 vars have ever been set on Demo. **If they are not set, the API will fail `ValidateOnStart()` and never come up.**

### 5.3 `SubtitleProcessingSettingsValidator` — 🟡 low-medium risk
Binds to `SubtitleProcessing`, requires `SubtitleProcessing__ElevenLabs__ApiKey` to be non-blank. Subtitle processing (video transcription) is a pre-existing, already-shipped feature, so this key is very likely already configured on any environment where the feature has ever been tested — including possibly Demo, if Demo was ever used to demo subtitle processing. The behavior *change* is that a missing key now hard-crashes the whole API at startup instead of only breaking subtitle processing at the moment it's invoked. Lower confidence of actual failure than §5.2, but same failure mode if wrong.

Related BACKLOG history is worth knowing: a real P0 incident (`§5.34`) already occurred from an *incomplete* version of this exact migration pattern — the validator checked the new `AIProviders:ElevenLabs:Models:Transcription` key (present) while the actual service code still silently read the old, now-defaulted-to-empty key, so the validator passed but transcription failed at runtime. That specific bug is stated as fixed (CLAUDE.md Note 32/33, "Option B is the canonical example of this rule applied end-to-end"). The current state should not have that gap — but it's the kind of thing worth a real transcription smoke test after deploy, not just a login check.

### 5.4 No other new `BindConfiguration`/`ValidateOnStart` calls
Grepped `Program.cs` and all `*ServiceCollectionExtensions*.cs` diffs between main and transval; the three validators above are the complete list of new startup-time config gates.

### 5.5 appsettings files
Only `src/QuantumBuild.API/appsettings.Testing.json` is tracked in git (used for the test suite / CI). `appsettings.Development.json` and `appsettings.Production.json` are gitignored and configured entirely through Railway environment variables per-environment, consistent with the `__` separator convention already documented in CLAUDE.md. There is no diff to review here — the risk is entirely in "does Railway's Demo service have the right env vars," which cannot be answered from the repo.

---

## 6. Demo-specific risk assessment (Part 5)

**Schema state — genuinely unknown.** No `railway.json`/`railway.toml`, no deploy-log file, and no migration-state tracking is committed to this repo. There is no way to determine which migrations Demo's database has already applied without connecting to it or triggering a deploy and reading the startup log. This is stated plainly rather than guessed at — **we will not know Demo's exact migration baseline until the deploy happens and the migration-on-startup log is read.**

That said, two things reduce (not eliminate) the practical risk on the schema side:

1. All 16 new migrations are additive-only except `AddSingleSupervisorUniqueness` (§3.3), so regardless of how far behind Demo is, applying this set from *any* earlier point should succeed unless Demo's existing `SupervisorAssignments` data already violates the new uniqueness constraint.
2. The one migration that *looks* alarming on diff (`AddQrCodeCourseId`, §3.1) is actually a fix, not a new risk — worst case it's a no-op on Demo.

**Config state — evidenced, not just guessed.** Unlike the schema question, there is direct textual evidence in this repo (BACKLOG.md + CLAUDE.md) that:
- Demo has been disconnected from GitHub auto-deploy for some time and requires manual reconnect + a documented list of "14 missing env vars" before it can be redeployed at all — and that list was written **before** the `AIProviders` config work existed.
- The `AIProviders__*` env var checklist that *does* exist explicitly scopes itself to "Production and Development," not Demo.

This is a much stronger basis than "Demo is behind, degree unknown" — it specifically points at the `AIProviders` env vars as the most likely first failure Demo will hit, ahead of and independent of any migration risk.

---

## 7. Recommended action

**Push Demo, but not blind.** Before pushing:

1. **Verify (or add) these 4 Railway env vars on the Demo service:**
   - `AIProviders__Anthropic__Models__Sonnet`
   - `AIProviders__Anthropic__Models__Haiku`
   - `AIProviders__Gemini__Models__Flash`
   - `AIProviders__ElevenLabs__Models__Transcription`

   This alone is expected to be the difference between "Demo starts" and "Demo doesn't start."

2. **Also confirm** `SubtitleProcessing__ElevenLabs__ApiKey` is set (lower confidence of being missing, same failure mode if it is).

3. **Resolve the pre-existing "14 missing env vars" checklist** (TranslationValidation block + `Cors__AllowedOrigins`) — that's independent of transval's new work but still blocking per the existing BACKLOG entry.

4. **Watch the deploy log specifically for `AddSingleSupervisorUniqueness`** — if it fails, it means Demo has an operator with more than one active supervisor assignment; that's a data cleanup task, not a code rollback.

5. Everything else in this push (227 commits, 15 of 16 migrations) is low-risk by content — the volume of change is large, but almost all of it is new tables/nullable-or-defaulted columns with no destructive `Down()` and no forward-referencing ordering problems.

No fix prompts are included per the scope of this recon — this document is diagnostic only.
