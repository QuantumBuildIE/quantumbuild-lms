# §26 Chunk 1 Recon — Parse jobs tolerate soft-deleted talks

_Date: 2026-06-19_
_Branch: transval_
_Author: Claude Code (read-only recon — no code changed)_
_Triggered by: §26 BACKLOG entry, post-§25 close_

---

## 1. Verification verdict — no drift

Both parse jobs and the convention reference match the investigation's file:line citations. No material drift found.

| File | Investigation cite | Current state | Drift? |
|------|---|---|---|
| `VideoTranscriptionJobForTalk.cs` line 35–37 | `IgnoreQueryFilters()` + predicate on `talkId && tenantId` only | Lines 35–37: exact match | None |
| `VideoTranscriptionJobForTalk.cs` null guard | `if (talk is null)` at line ~40, `LogWarning "[...] not found"` | Lines 39–44: exact match | None |
| `VideoTranscriptionJobForTalk.cs` status guard | `if (talk.Status != Processing)` at line ~44 | Lines 47–53: +3 lines (empty line before try); same logic | Cosmetic only |
| `VideoTranscriptionJobForTalk.cs` write step | `talk.ExtractedVideoTranscript = ...` + `SaveChangesAsync` at lines 95–99 | Lines 98–101: same statements | Cosmetic only |
| `ContentCreationParseJobForTalk.cs` line 30–32 | `IgnoreQueryFilters()` + predicate on `talkId && tenantId` only | Lines 30–32: exact match | None |
| `ContentCreationParseJobForTalk.cs` null guard | `if (talk is null)` at line ~35, `LogWarning "[...] not found"` | Lines 34–39: exact match | None |
| `ContentCreationParseJobForTalk.cs` status guard | `if (talk.Status != Processing)` at line ~39 | Lines 42–48: +3 lines; same logic | Cosmetic only |
| `ContentCreationParseJobForTalk.cs` write step | soft-delete, insert sections, update talk at lines 81–105 | Lines 81–105: exact match | None |
| `TranslationValidationJob.cs` convention reference | `FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted, ...)` at lines 990–992 | Lines 990–992: exact match | None |

Neither parse job checks `IsDeleted` anywhere in the method body. The zombie-revival outcome described in the investigation is confirmed in the current code.

---

## 2. Implementation shape recommendation — Shape B (explicit if-block)

**Recommendation: Shape B.** The BACKLOG entry states a preference for Shape A (predicate extension). After reading the current log messages, that preference should be revisited.

### Why Shape A has a subtle problem

Shape A — adding `&& !t.IsDeleted` to the `FirstOrDefaultAsync` predicate — routes a soft-deleted talk through the existing null guard:

```csharp
// current null guard (VideoTranscriptionJobForTalk.cs:39-44)
if (talk is null)
{
    logger.LogWarning(
        "[VideoTranscriptionForTalk] Talk {TalkId} not found for tenant {TenantId}",
        talkId, tenantId);
    return;
}
```

This fires at **`LogWarning`** and says "not found". That's appropriate when a job is handed a talkId that doesn't exist in the database — a sign of something unexpected (stale ID, double-enqueue after hard delete, etc.).

A soft-deleted talk during a cancel operation is the **intentional, expected** path that Chunk 2's Cancel feature creates. Logging that at `Warning` level mixes an expected success path with genuine anomaly signals. In production log aggregation, teams filter on log level; a Warning caused by a user clicking Cancel is noise.

### Why Shape B is correct

Shape B keeps the two cases separate:

```csharp
if (talk is null)
{
    // unexpected — talkId doesn't exist at all
    logger.LogWarning("[...] not found ...");
    return;
}

if (talk.IsDeleted)
{
    // expected once Cancel lands — normal termination
    logger.LogInformation("[...] has been deleted — skipping");
    return;
}
```

The distinction is operationally meaningful:
- `Warning "not found"` → investigate (how did the job get an ID that doesn't exist?)
- `Information "has been deleted — skipping"` → expected (user cancelled during parse)

Shape B adds 6 lines per file (4 net), matching the investigation's proposed change exactly. The diff is still tiny. Shape A saves 2 lines per file but introduces a log-level ambiguity that will matter once Cancel lands.

**Conclusion: implement Shape B.**

---

## 3. Log message wording (Shape B)

Log prefix convention in both files: `[VideoTranscriptionForTalk]` and `[ContentCreationParseForTalk]` — bracket-prefixed job name, PascalCase, present in every log line in both jobs. Match exactly.

**`VideoTranscriptionJobForTalk.cs`**, after line 44:
```csharp
if (talk.IsDeleted)
{
    logger.LogInformation(
        "[VideoTranscriptionForTalk] Talk {TalkId} has been deleted — skipping",
        talkId);
    return;
}
```

**`ContentCreationParseJobForTalk.cs`**, after line 39:
```csharp
if (talk.IsDeleted)
{
    logger.LogInformation(
        "[ContentCreationParseForTalk] Talk {TalkId} has been deleted — skipping",
        talkId);
    return;
}
```

`LogInformation` (not `LogWarning`) throughout — same rationale as above.

---

## 4. Adjacent jobs scan

All jobs using `IgnoreQueryFilters()` in the codebase were reviewed. Only hits relevant to `ToolboxTalks` are assessed for vulnerability:

| Job / Call site | Entity loaded | IsDeleted checked? | Vulnerable? | Notes |
|---|---|---|---|---|
| `VideoTranscriptionJobForTalk` | `ToolboxTalks` | **No** | **Yes — fix target** | This chunk |
| `ContentCreationParseJobForTalk` | `ToolboxTalks` | **No** | **Yes — fix target** | This chunk |
| `TranslationValidationJob` line 990–992 | `ToolboxTalks` | Yes (`&& !t.IsDeleted` in predicate) | No | Convention reference — already correct |
| `TranslationValidationJob` line 418–421 | `ToolboxTalks` — title only | No | **No — read-only, graceful null** | Selects title for a notification; `FirstOrDefaultAsync` returns null for a deleted row with default filter bypassed but no `!t.IsDeleted`; caller uses `?? "Unknown"` fallback. No write path. Not vulnerable in any meaningful sense. |
| `TranslationValidationJob` line 473–476 | `ToolboxTalks` — title only | No | **No — read-only, graceful null** | Same pattern as 418–421; failure path notification; graceful null. |
| `BulkEmployeeImportJob` | `BulkImportSessions` | Yes (`!s.IsDeleted` in predicate) | No | Different entity; safe |
| `GenerateEmployeePinsJob` | `Employees`, `Tenants` | Yes (`!e.IsDeleted` in predicate) | No | Different entities; safe |
| `LessonParseJob` | `ParseJobs` | Yes (`!j.IsDeleted` in predicate) | No | Different entity; safe |
| `DataSeeder` (multiple) | `Tenants`, `Permissions`, `LookupValues` | Yes (explicit where clauses) | No | Startup seeder, not a runtime job |
| `AuthService` | `TenantModules` | Yes (`!m.IsDeleted` in predicate) | No | Not a job; HTTP context path |
| `TenantService`, `UserService` | `Tenants`, `Employees` | Yes (explicit predicates) | No | Service layer, not jobs |

**No additional jobs need to be folded into Chunk 1.** The two title-only reads in `TranslationValidationJob` are read-only and gracefully null-handle — they're not a meaningful vulnerability (a deleted talk simply produces "Unknown" in a notification string, which is acceptable). They could be tidied up but that's a separate quality concern, not a correctness issue.

---

## 5. Test coverage assessment

**Existing coverage for the parse jobs:** None. The test suite was checked comprehensively:

- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/` — no file named `*VideoTranscription*`, `*ContentCreationParseJob*`, or similar
- `tests/QuantumBuild.Tests.Unit/` — same result
- `ParseToolboxTalkContentCommandHandlerTests.cs` exists and covers the synchronous command handler (PDF/Text path), not the async job chain (Video path)
- `MissingTranslationsJobTests.cs` exists but covers a different job

**Does the soft-deleted-mid-job scenario have test coverage?** No. There is no test that:
1. Creates a talk in Processing status
2. Soft-deletes it
3. Invokes `VideoTranscriptionJobForTalk.ExecuteAsync` or `ContentCreationParseJobForTalk.ExecuteAsync`
4. Asserts the job returns without writing to the deleted talk row

**Should Chunk 1 add such a test?**

**No — don't add tests in Chunk 1.** The reasoning:

1. The fix is two predicate-extension or two explicit if-blocks in two files. It follows a well-established pattern (`TranslationValidationJob`) that's already in production and already passes the existing test suite. The code review of the diff against this recon's spec is the primary verification mechanism.

2. Testing the "soft-deleted mid-job" scenario for a Hangfire job requires either: (a) invoking the job directly in a unit test with a mocked DbContext, or (b) an integration test with a real DB and direct job invocation (no Hangfire scheduler needed, just `new Job(...).ExecuteAsync(...)`). Both are meaningful setups. Option (b) would be valuable, but its setup cost (integration test base, seeded talk, explicit status transition, direct job call, assertion on DB state) is meaningfully larger than the fix itself.

3. The test suite philosophy recorded in BACKLOG §5.15 notes the integration test suite needs a comprehensive post-Phase-5 review, and PHASE_5_STANDARDS §11 discourages adding tests beyond what verifies non-obvious new behaviour. The IsDeleted check pattern is well-understood; adding a test here is "testing the framework, not the feature."

4. If the test value is judged worth the effort, add it as a separate, named task post-implementation — not as a requirement blocking Chunk 1 from shipping.

---

## 6. Sized implementation chunk

### Files changed

**File 1:** [VideoTranscriptionJobForTalk.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/VideoTranscriptionJobForTalk.cs)

- **Position:** After line 44 (the `return;` closing the null guard block), before line 46 (the `// Uploaded video file...` comment)
- **Change:** Insert 6 lines:
  ```csharp
  if (talk.IsDeleted)
  {
      logger.LogInformation(
          "[VideoTranscriptionForTalk] Talk {TalkId} has been deleted — skipping",
          talkId);
      return;
  }
  ```
- **Net diff:** +6 lines

**File 2:** [ContentCreationParseJobForTalk.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentCreationParseJobForTalk.cs)

- **Position:** After line 39 (the `return;` closing the null guard block), before line 41 (the `if (talk.Status != ToolboxTalkStatus.Processing)` status guard)
- **Change:** Insert 6 lines:
  ```csharp
  if (talk.IsDeleted)
  {
      logger.LogInformation(
          "[ContentCreationParseForTalk] Talk {TalkId} has been deleted — skipping",
          talkId);
      return;
  }
  ```
- **Net diff:** +6 lines

### Total diff

12 lines added across 2 files. No deletions. No other files touched.

### Estimated effort

Under 30 minutes including the scope-discipline read-pass. This is a genuinely trivial change: two insertions of an identical 6-line pattern into two files in the same directory. The recon took longer than the implementation will.

### Exact insertion positions (line numbers after edit)

After the fix, the guard sequence in each job reads:
```
if (talk is null)    → LogWarning "not found" → return
if (talk.IsDeleted)  → LogInformation "deleted — skipping" → return
if (talk.Status != Processing) → LogWarning "wrong status" → return
```

This matches the convention established in `TranslationValidationJob`, which uses Shape A (predicate) — the same logical ordering is produced by Shape B here because the null guard and IsDeleted guard are adjacent and produce the same method-exit behaviour.

---

## 7. Verification approach

This is backend-only with no UI surface. The verification chain:

1. **`dotnet build`** — confirm clean compile (no syntax errors, no new warnings)
2. **`dotnet test`** — run all tests, confirm existing count unchanged (pre-fix baseline: check current count against the BACKLOG §5.15 reference of ~397 integration tests). Expectation: all passing, none failing due to this change (the change only affects Hangfire job paths with no existing test coverage)
3. **Code review of the diff** — diff should show exactly 12 lines added across 2 files, in the positions described in §6, matching this recon's spec

Visual verification does not apply. Manual end-to-end verification is not practical for this path (would require triggering a video parse job, soft-deleting the talk mid-transcription, and inspecting DB state — possible but overkill for a 12-line change following an established pattern).

---

## 8. Files read

| File | Purpose |
|---|---|
| `docs/parse-handler-cancellation-investigation.md` | Primary investigation — bug description, fix proposal, adjacent concerns |
| `docs/25/post-close-navigation-recon.md` | Context — gap B and recommended next step |
| `BACKLOG.md` §26 | Chunk plan, preferred implementation shape, scope |
| `src/.../Jobs/VideoTranscriptionJobForTalk.cs` | Fix target 1 — full read |
| `src/.../Jobs/ContentCreationParseJobForTalk.cs` | Fix target 2 — full read |
| `src/.../Jobs/TranslationValidationJob.cs` | Convention reference — `IgnoreQueryFilters` + `!t.IsDeleted` pattern |
| All `IgnoreQueryFilters` usages in `src/` | Adjacent jobs scan |
| `tests/**/*.cs` (full listing) | Test coverage assessment |

---

## 9. Report written

`docs/26/chunk-1-recon.md` — this file.

---

## 10. Out of scope items flagged

| Item | Reason |
|---|---|
| The two title-only `IgnoreQueryFilters` reads on `ToolboxTalks` in `TranslationValidationJob` (lines 418, 473) | Read-only, graceful null, no write path. Not a correctness issue — a separate quality tidying concern at most. Not Chunk 1's scope. |
| `TranslationValidationJob` itself | Already handles `IsDeleted` correctly. No change needed. |
| Adding integration tests for the parse-job soft-delete scenario | Assessment: don't add in Chunk 1 (see §5). Can be added as a separate task. |
| Shape A vs Shape B BACKLOG preference | BACKLOG stated preference is Shape A; this recon recommends Shape B due to log-level distinction. The BACKLOG preference was set before the log message analysis — the implementer should read §2 and decide. Not changing the BACKLOG; the recon documents the reasoning. |
| Chunk 2 (Cancel frontend) | Depends on Chunk 1 landing first. Separate scope. |
| Chunks 3 and 4 | Out of scope per this recon's brief. |
| Aborting in-flight AI API calls | Deliberately out of §26 scope per BACKLOG entry. |
| Orphaned drafts from browser-close / network-drop | Separate concern. |
| `LessonParseJob` | Loads `ParseJobs` (not `ToolboxTalks`) with `!j.IsDeleted` already in predicate. Safe. Not related. |
