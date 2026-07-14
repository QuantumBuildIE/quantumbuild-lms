# DataSeeder Production Exposure — Recon Report

**Date:** 2026-06-17  
**Branch:** transval  
**Triggered by:** Playwright Step 2 recon out-of-scope flag

---

## Verdict

**P1 (urgent) — DataSeeder. P0 (stop-work) flagged out-of-scope for API keys in committed config.**

DataSeeder runs unconditionally on every startup in Production with two users whose passwords are hardcoded literal strings in source code. Skip-if-exists prevents the password being overwritten once set, so Production is only vulnerable if these accounts still hold their seeded credentials — which is unknowable from code alone. Any fresh deploy (new DB, DB reset, demo instance) immediately produces live accounts with publicly-known passwords.

A separate, potentially more severe finding emerged out-of-scope: `appsettings.json` contains what appear to be actual API keys committed to the repository (Claude, ElevenLabs, R2 Storage, MailerSend, Float). This is addressed in the **Out-of-Scope Findings** section and warrants immediate attention independent of the DataSeeder work.

---

## 1. What DataSeeder Creates

Called from `Program.cs:332` (unconditional, no environment check):

```csharp
await DataSeeder.SeedAsync(app.Services);          // line 332
await SeedToolboxTalksDataAsync(app.Services);     // line 335
```

**DataSeeder.SeedAsync creates (in order):**

| Entity type | What | Notes |
|---|---|---|
| Tenant | `QUANTUMBUILD` / ID `11111111-1111-1111-1111-111111111111` | Skip-if-exists by ID |
| Permissions | All permission names from `Permissions.GetAll()` | Skip-if-exists by name |
| Roles | SuperUser, Admin, Supervisor, Operator | Skip-if-exists by name |
| RolePermissions | Assigns permissions to roles per `GetPermissionsForRole()` | Skip-if-exists; also runs cleanup of stale Supervisor perms |
| User | `superuser@certifiediq.ai` / SuperUser role | Skip-if-exists by email |
| User | `admin@quantumbuild.ai` / Admin role | Skip-if-exists by email |
| Employee | Linked to `admin@quantumbuild.ai` | Skip-if-exists on `adminUser.EmployeeId != null` |
| LookupCategories | TrainingCategory, Department, JobTitle, Language | Skip-if-exists by name |
| LookupValues | 33 language codes | Skip-if-exists by code |
| TenantLookupValues | 15 training categories for QUANTUMBUILD tenant | Skip-if-exists by code |
| TenantModules | Learnings module for every tenant | Skip-if-exists by tenant+module |

**SeedToolboxTalksDataAsync (also unconditional) creates:**  
ToolboxTalk sample data, SafetyGlossary terms, Sectors, RegulatoryProfiles, RegulatoryRequirements. No users or credentials — system/reference data only.

---

## 2. How Credentials Are Sourced

### SuperUser (`DataSeeder.cs:253–305`)

```csharp
const string superUserEmail = "superuser@certifiediq.ai";   // line 255 — literal
const string superUserPassword = "SuperUser123!";            // line 256 — literal
```

- Email: **hardcoded literal string in source**
- Password: **hardcoded literal string in source**
- Role: `"SuperUser"` — hardcoded literal
- Created via `userManager.CreateAsync(superUser, superUserPassword)` — ASP.NET Identity hashes the password with PBKDF2 at seed time. The plaintext `SuperUser123!` is **not stored in the DB**, but it IS known from source code to anyone with repo access.

### Admin (`DataSeeder.cs:307–358`)

```csharp
const string adminEmail = "admin@quantumbuild.ai";   // line 309 — literal
const string adminPassword = "Admin123!";             // line 310 — literal
```

- Email: **hardcoded literal string in source**
- Password: **hardcoded literal string in source**
- Role: `"Admin"` — hardcoded literal
- Same `UserManager.CreateAsync` hashing applies.

---

## 3. Invocation Gate

**None.** `Program.cs:332`:

```csharp
await DataSeeder.SeedAsync(app.Services);
```

No `app.Environment.IsDevelopment()` wrapper. No appsettings flag. No compile-time switch.  
Runs identically on every startup in Development, Production, and any other environment.

For contrast: the Hangfire dashboard (`Program.cs:432`) and Swagger (`Program.cs:393`) ARE gated by `IsDevelopment()`. The seeder was never given the same treatment.

---

## 4. Idempotency

**Skip-if-exists on all writes.** The seeder is safe to re-run — it will not corrupt existing data or overwrite passwords.

For users specifically (`SeedSuperUserAsync` line 258, `SeedAdminUserAsync` line 315):

```csharp
var existingUser = await userManager.FindByEmailAsync(superUserEmail);
if (existingUser != null)
{
    logger.LogInformation("SuperUser already exists, skipping");
    return;
}
```

**Consequence:** If Production's `superuser@certifiediq.ai` or `admin@quantumbuild.ai` has ever had its password changed since initial seeding, subsequent startups leave that password untouched. The known password only applies to accounts that have never had their password changed since the first time the seeder ran on that database.

**What happens on a fresh DB or DB reset:** Both accounts are created with `SuperUser123!` and `Admin123!`. They are immediately active with publicly-known credentials.

---

## 5. Verdict

**P1 (urgent).** Two accounts with hardcoded known passwords are seeded into every environment including Production, with no gate.

The skip-if-exists protects against password reset on rolling restarts of a live system, but:

1. Any fresh database (new deployment, DB reset, demo instance restore) immediately produces `superuser@certifiediq.ai` / `SuperUser123!` and `admin@quantumbuild.ai` / `Admin123!` as live, working credentials.
2. The BACKLOG lists a planned Demo deployment that requires a database restore. If that DB starts fresh, these accounts land with known credentials and SuperUser / Admin access to all tenant data.
3. Whether Production's current passwords have been changed from the seeded values is unknowable from the code. If they haven't been changed since the initial deploy, they are currently exploitable.
4. `superuser@certifiediq.ai` is a SuperUser account — bypasses tenant query filters, has cross-tenant access to all data.

**Minimum fix:** move `SeedSuperUserAsync` and `SeedAdminUserAsync` behind an `IsDevelopment()` check in `Program.cs`, and on Production bootstrap from environment variables via `IConfiguration` rather than literals. Separately, verify (DB-direct) whether Production passwords have ever been changed from the seeded values; if not, rotate immediately.

---

## Out-of-Scope Findings

These were encountered during the required pre-flight reads. Each requires a separate scoped investigation.

### A. API keys committed to `appsettings.json` (likely P0)

`src/QuantumBuild.API/appsettings.json` is the **base configuration file loaded in all environments** (including Production). It contains what appear to be actual service API keys committed directly to the repository:

| Key | Location | Value pattern |
|---|---|---|
| Anthropic Claude API key | `appsettings.json:86` | `sk-ant-api03-uMCUHp…` (full key) |
| ElevenLabs API key | `appsettings.json:84` | `9c1fd862…` (full key) |
| R2 Storage AccessKeyId | `appsettings.json:100` | `190d7bd9…` |
| R2 Storage SecretAccessKey | `appsettings.json:101` | `d443e825…` (full key) |
| R2 Storage (media) keys | `appsettings.json:115–116` | `2954ce34…` / `8993b092…` |
| MailerSend API key | `appsettings.json:64` | `mlsn.786be9c0…` (full key) |
| Float API key | `appsettings.json:154` | `6c8e7ef8…` (full key) |

`appsettings.Development.json` contains a **different** Claude API key (`sk-ant-api03-MRc9GV1Z…`) than the base file, confirming these are not both dev placeholders — at least one is a real key. There is no `appsettings.Production.json`; if Railway does not override these keys via environment variables, the keys in `appsettings.json` are the live Production keys.

Also in `appsettings.json:11`: `JwtSettings.Secret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"` — a placeholder that, if not overridden by Railway, allows any party with repo access to forge valid JWTs for Production.

Also in `appsettings.Development.json:28`: Azure PostgreSQL connection string with password `quantumbuild_009988776655_#1` committed in plaintext.

**This is outside the DataSeeder scope but warrants immediate, separate investigation** to confirm whether Railway environment variables fully override every key in `appsettings.json` before deciding whether rotation is required.

### B. `SeedToolboxTalksDataAsync` is a second ungated seeding path

`Program.cs:335` calls `SeedToolboxTalksDataAsync` unconditionally alongside `DataSeeder.SeedAsync`. Its sub-seeders (`ToolboxTalksSeedData`, `SafetyGlossarySeedData`, `SectorSeedData`, `RegulatoryProfileSeedData`, `RegulatoryRequirementSeedData`) create system/reference data only — no users or credentials were found. This is not a security concern but is noted per scope: there is a second ungated seeder path.

---

## Files Read

| File | Purpose |
|---|---|
| `src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs` | Primary seeder — full read |
| `src/QuantumBuild.API/Program.cs` | Invocation site, gate check |
| `src/QuantumBuild.API/appsettings.json` | Base configuration (all environments) |
| `src/QuantumBuild.API/appsettings.Development.json` | Development overrides |

`appsettings.Production.json` — **does not exist**. No `appsettings.Testing.json` read (no credentials expected; confirmed by glob result).

ToolboxTalks seeder files (`ToolboxTalksSeedData.cs`, etc.) — grep for user/email/password patterns returned 0 matching hits; no user-creation code found.
