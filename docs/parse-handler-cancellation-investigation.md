# Parse Handler Cancellation Investigation

_Date: 2026-06-19_
_Branch: transval_
_Author: Claude Code (read-only investigation — no code changed)_
_Trigger: Post-close navigation recon identified no Cancel/abort in new wizard step 2. Design intent is "delete talk on cancel, let in-flight AI call finish, discard result by virtue of row being gone." This recon answers: is that safe?_

---

## 1. Parse Flow Sequence

```
frontend mutation
  useParseTalk(talkId)                             web/src/features/toolbox-talks/components/learning-wizard/hooks/useParseTalk.ts:11
  → parseTalk(id)                                  web/src/lib/api/toolbox-talks/toolbox-talks.ts:127-130
  → POST /api/toolbox-talks/{id}/parse

API endpoint
  ToolboxTalksController.ParseContent(id, ct)      src/QuantumBuild.API/Controllers/ToolboxTalksController.cs:423
  → _mediator.Send(ParseToolboxTalkContentCommand)

Command handler
  ParseToolboxTalkContentCommandHandler.Handle()   src/.../Commands/ParseToolboxTalkContent/ParseToolboxTalkContentCommandHandler.cs:33

  [CHECK] talks query with !t.IsDeleted             line 37 — explicit soft-delete guard
  → dispatch on InputMode:

  PDF / Text path (synchronous — runs inline in the HTTP request):
    HandlePdfAsync / HandleTextAsync               lines 56, 78
    → AI call via _contentParserService.ParseContentAsync()
    → MaterialiseSectionsAsync()                   line 129
    → _dbContext.SaveChangesAsync()
    → return DTO to caller
    [NO race window — entire operation within one HTTP request]

  Video path (asynchronous — returns immediately):
    HandleVideoAsync()                             line 109
    → talk.Status = Processing                     line 120
    → _dbContext.SaveChangesAsync()
    → _parseJobScheduler.EnqueueVideoTranscriptionJob(talkId, tenantId)  line 124
    → return DTO (sections = [])
    [RACE WINDOW OPENS HERE]

Background job chain (video path only):
  Job 1: VideoTranscriptionJobForTalk.ExecuteAsync()
    src/.../Jobs/VideoTranscriptionJobForTalk.cs:29
    [AutomaticRetry(Attempts = 2)]
    → ElevenLabs transcription call
    → talk.ExtractedVideoTranscript = transcriptText  line 98
    → SaveChangesAsync()
    → BackgroundJob.Enqueue<ContentCreationParseJobForTalk>()  line 107

  Job 2: ContentCreationParseJobForTalk.ExecuteAsync()
    src/.../Jobs/ContentCreationParseJobForTalk.cs:24
    [AutomaticRetry(Attempts = 3)]
    → _contentParserService.ParseContentAsync()    line 62  (Claude AI call — cost incurred here)
    → soft-delete existing sections               lines 81-85
    → INSERT new ToolboxTalkSection rows          lines 87-100
    → talk.Status = Draft, LastEditedStep = 2     lines 102-104
    → SaveChangesAsync()                          line 105
```

---

## 2. The Write Step

In `ContentCreationParseJobForTalk`, lines 80–105:

```csharp
// Soft-delete any existing sections (e.g. from a prior parse attempt)
var existing = await dbContext.ToolboxTalkSections
    .Where(s => s.ToolboxTalkId == talkId && !s.IsDeleted)
    .ToListAsync(cancellationToken);
foreach (var s in existing)
    s.IsDeleted = true;

var sectionNumber = 1;
foreach (var parsed in result.Sections.OrderBy(s => s.SuggestedOrder))
{
    dbContext.ToolboxTalkSections.Add(new ToolboxTalkSection
    {
        Id = Guid.NewGuid(),
        ToolboxTalkId = talkId,          // FK to the (possibly deleted) talk
        SectionNumber = sectionNumber++,
        Title = parsed.Title,
        Content = parsed.Content,
        RequiresAcknowledgment = true,
        Source = ContentSource.Video,
    });
}

talk.GeneratedFromVideo = true;
talk.Status = ToolboxTalkStatus.Draft;  // <-- clears Processing status
talk.LastEditedStep = 2;
await dbContext.SaveChangesAsync(cancellationToken);
```

This is a direct `DbSet.Add` + `SaveChangesAsync`. No FK violation occurs because the FK is configured `OnDelete(DeleteBehavior.Cascade)` (section config line 66-69) — soft-delete does not touch the actual row, so the FK still points to a valid primary key.

---

## 3. Behaviour When the Talk Is Gone at Write Time

**Outcome: C — Zombie Revival**

### How the jobs fetch the talk

Both jobs use `.IgnoreQueryFilters()` on their initial talk load:

```csharp
// VideoTranscriptionJobForTalk.cs:35-37
var talk = await dbContext.ToolboxTalks
    .IgnoreQueryFilters()           // bypasses IsDeleted + tenant filter
    .FirstOrDefaultAsync(t => t.Id == talkId && t.TenantId == tenantId, cancellationToken);
```

```csharp
// ContentCreationParseJobForTalk.cs:30-32
var talk = await dbContext.ToolboxTalks
    .IgnoreQueryFilters()           // bypasses IsDeleted + tenant filter
    .FirstOrDefaultAsync(t => t.Id == talkId && t.TenantId == tenantId, cancellationToken);
```

### What each job checks after the load

Both jobs guard on status only:

```csharp
if (talk is null)   { LogWarning; return; }          // graceful if row physically absent
if (talk.Status != ToolboxTalkStatus.Processing)
    { LogWarning; return; }                           // graceful if status changed
```

Neither job checks `if (talk.IsDeleted) { ... return; }`.

### The scenario: user cancels via talk deletion

1. Parse triggered on a video talk → status set to `Processing`, job enqueued.
2. **User deletes the talk** → talk row gets `IsDeleted = true`, `Status` remains `Processing`.
3. `VideoTranscriptionJobForTalk` runs:
   - `IgnoreQueryFilters()` retrieves the soft-deleted row.
   - `talk.Status == Processing` → guard passes.
   - `IsDeleted` is never checked.
   - Transcription proceeds (ElevenLabs API call, billed).
   - `talk.ExtractedVideoTranscript = ...` + `SaveChangesAsync()` — **writes to the deleted talk row**.
   - Enqueues `ContentCreationParseJobForTalk`.
4. `ContentCreationParseJobForTalk` runs:
   - `IgnoreQueryFilters()` retrieves the soft-deleted row.
   - `talk.Status == Processing` → guard passes (status was not changed by VideoTranscriptionJob — note: VideoTranscriptionJob does NOT change status on success, only on failure).
   - `IsDeleted` is never checked.
   - Claude AI called (billed).
   - New `ToolboxTalkSection` rows inserted with FK pointing to the deleted talk.
   - `talk.Status = Draft`, `IsDeleted` still `true`.
   - `SaveChangesAsync()` succeeds — the FK points to a physically present row.
5. **Result**: the talk row exists in the DB with `IsDeleted = true`, `Status = Draft`, and a fresh set of child section rows. Normal user queries (which apply the `HasQueryFilter`) will not see the talk or its sections. Admin queries that call `IgnoreQueryFilters()` will. The draft list (uses standard filtered queries) correctly suppresses it. The data is a ghost — invisible in production UI but taking up storage and costing API money.

### Why it is not Outcome B (crash)

The FK from `ToolboxTalkSection` to `ToolboxTalk` is on `Id` (the primary key). Soft-delete marks `IsDeleted = true` but does not remove the row. The primary key still exists, so the FK constraint is satisfied and EF's `SaveChangesAsync` succeeds without error.

### Why it is not Outcome A (graceful no-op)

A graceful no-op would require one of:
- An `if (talk.IsDeleted) return;` check in the job, or
- Not using `IgnoreQueryFilters()`, so the normal filter returns `null` for a deleted talk.

Neither exists in either job.

---

## 4. Smallest Change Required

Add one line to each job's existence check, immediately after the `if (talk is null)` guard:

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

That is 6 lines per file (4 net, excluding the closing brace). No other changes required.

**Why `IgnoreQueryFilters()` must stay:** these jobs intentionally bypass the tenant filter so they can run without an HTTP context (no `ICurrentUserService` TenantId). Removing `IgnoreQueryFilters()` would break the tenant filter bypass needed for Hangfire. The `IsDeleted` check must be explicit.

**Note on AI cost:** neither guard prevents the AI API call from being billed for work done before the delete. The checks only prevent the write and the chained job. The transcription cost (ElevenLabs, Job 1) is incurred before any write attempt. The Claude AI cost (Job 2) is incurred at line 62, before the section writes at line 81. The fix makes the write a no-op but does not recover already-spent API credits.

---

## 5. Adjacent Concerns

**Text/PDF parse path** — safe by design. `ParseToolboxTalkContentCommandHandler` checks `!t.IsDeleted` explicitly at line 37, and the entire parse runs within a single synchronous HTTP request. There is no race window.

**`TranslationValidationJob`** — loads the parent talk at line 990-992 with `.IgnoreQueryFilters()` but explicitly adds `&& !t.IsDeleted` to the predicate: `FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted, ...)`. This job handles the deleted-talk case correctly.

**`ValidationReportJob`** — loads the `TranslationValidationRun`, not the talk directly. The run entity has its own `IsDeleted` flag. Would need a separate check on the run row, but runs are a different lifecycle from talks. Not a concern for the parse-cancel scenario.

**`MissingTranslationsJob`** — loads talks and dispatches per-language translation work. Uses the standard filtered DbContext (no `IgnoreQueryFilters()`), so deleted talks are automatically excluded. Safe.

---

## Summary

| | Text/PDF path | Video path |
|---|---|---|
| Race window exists? | No — synchronous | **Yes** — two background jobs |
| `IsDeleted` checked? | Yes — line 37 of command handler | **No** — neither job checks it |
| Outcome if talk deleted mid-flight | Request returns 404 immediately | **Outcome C: zombie revival** |
| AI cost incurred before safe exit? | N/A | Yes — transcription + Claude call complete before abort is possible |
| Fix required? | None | Yes — one `if (talk.IsDeleted) return;` in each job |

The "delete talk on cancel" design is **safe in intent but unsafe in the current implementation** for video-mode talks. The two-line fix above makes it safe. For text and PDF talks, the design is already safe without any changes.
