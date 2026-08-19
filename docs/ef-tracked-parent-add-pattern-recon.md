# EF Tracked-Parent Navigation-Collection `.Add()` Pattern — Codebase Recon

## Verdict

**LOCALISED.** The bug fixed in commit `5104b20` ("Fix schedule assignment inserts
misclassified as updates when adding to a tracked schedule") was an isolated defect in
the three `ToolboxTalkSchedule.Assignments` write sites (`CreateToolboxTalkSchedule`,
`ProcessToolboxTalkSchedule` ×2, `UpdateToolboxTalkSchedule`), all now fixed with an
explicit `EntityState.Added` assignment. A full sweep of every EF navigation-collection
property in the solution (Core + ToolboxTalks + LessonParser) found **27 genuine
navigation-collection `.Add()`/`.AddRange()` call sites** across the codebase. Of those,
26 are provably safe (parent newly created and added to its `DbSet` in the same flow, or
the same instance is separately added explicitly to a `DbSet`). **One new AT-RISK
instance was found**, structurally identical to the fixed bug and with **zero test
coverage**: `AuditCorpusService.AddEntryAsync` (`corpus.Entries.Add(entry)` on an
already-tracked, DB-loaded `AuditCorpus`, with a client-assigned `Guid` key on the new
`AuditCorpusEntry`, no explicit `EntityState.Added`). This is reachable from a real
endpoint (`POST /api/pipeline-audit/corpus/{id}/entries`) and will 0-row/`DbUpdateConcurrencyException`
the same way the schedule bug did, the first time a second entry is manually added to an
existing corpus.

Everywhere else in the codebase, the risky idiom either never occurs, or the codebase
already avoids it — in several places (`MarkSectionReadCommandHandler`,
`GenerateContentTranslationsCommandHandler`, `TranslationValidationService`) new child
entities are added via an **explicit `_dbContext.Set<T>().Add(child)`** call even though
a navigation-collection `.Add()` is also present or would have been the more obvious
choice — one call site (`GenerateContentTranslationsCommandHandler.cs:666`) even carries
a comment ("Save the translation using DbSet.Add() for reliable change tracking")
suggesting this was already a known trap being deliberately avoided.

---

## All navigation-collection `.Add()` / `.AddRange()` occurrences found

Every entity with an `ICollection<T>`/`List<T>` navigation property (37 total, enumerated
in "Search methodology" below) was checked. The table below lists every call site where a
value was added to one of those navigation properties directly (as opposed to via an
explicit `DbSet<T>.Add()`/`_dbContext.Add()`/`AddRange()` call, which is inherently safe
and is not enumerated below individually except where it was a plausible false positive
worth ruling out).

| # | File:Line | Handler / Method | Nav collection | Classification | Reason |
|---|-----------|------------------|-----------------|-----------------|--------|
| 1 | `src/Modules/LessonParser/QuantumBuild.Modules.LessonParser.Infrastructure/Services/LessonGeneratorService.cs:141` | `LessonGeneratorService.GenerateFromContentAsync` | `toolboxTalk.Sections` | SAFE - parent newly added | `toolboxTalk` is `new ToolboxTalk{...}` at line 119, never queried; `_toolboxTalksDbContext.ToolboxTalks.Add(toolboxTalk)` at line 177 happens after all `Sections`/`Questions` adds, while `toolboxTalk` was still untracked the whole time. |
| 2 | `LessonGeneratorService.cs:161` | same | `toolboxTalk.Questions` | SAFE - parent newly added | Same as above. |
| 3 | `LessonGeneratorService.cs:210` | same | `course.CourseItems` | SAFE - parent newly added | `course` is `new ToolboxTalkCourse{...}` at line 196; `_toolboxTalksDbContext.ToolboxTalkCourses.Add(course)` at line 220 happens after the `CourseItems.Add` loop, `course` was never tracked before that. |
| 4 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalk/CreateToolboxTalkCommandHandler.cs:95` | `CreateToolboxTalkCommandHandler.Handle` | `toolboxTalk.Sections` | SAFE - parent newly added | `toolboxTalk` is `new ToolboxTalk{...}` at line 57; `_dbContext.ToolboxTalks.Add(toolboxTalk)` at line 116 happens after the Sections/Questions loops. |
| 5 | `CreateToolboxTalkCommandHandler.cs:113` | same | `toolboxTalk.Questions` | SAFE - parent newly added | Same as above. |
| 6 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs:198` | `CreateToolboxTalkScheduleCommandHandler.Handle` | `schedule.Assignments` | SAFE - parent newly added | `schedule` is `new ToolboxTalkSchedule{...}` at line 170; `_dbContext.ToolboxTalkSchedules.Add(schedule)` at line 201 happens after the Assignments loop. Confirmed as the unaffected "create path" in the `5104b20` commit message. |
| 7 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs:244` | `ProcessToolboxTalkScheduleCommandHandler.Handle` (one-time assignment loop) | `schedule.Assignments` | SAFE - already fixed | `schedule` is loaded via `FirstOrDefaultAsync` at line 43 (already-tracked). Line 245: `_dbContext.Entry(assignment).State = EntityState.Added;` immediately follows the nav-collection `.Add()`, explicitly fixed by commit `5104b20`. |
| 8 | `ProcessToolboxTalkScheduleCommandHandler.cs:317` | same handler, recurring/criteria-derived assignment loop | `schedule.Assignments` | SAFE - already fixed | Same fix pattern, line 318: `_dbContext.Entry(assignment).State = EntityState.Added;`. |
| 9 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs:227` | `UpdateToolboxTalkScheduleCommandHandler.Handle` | `schedule.Assignments` | SAFE - already fixed | `schedule` loaded via `FirstOrDefaultAsync` at line 41 (already-tracked). Line 228: `_dbContext.Entry(assignment).State = EntityState.Added;` immediately follows. |
| 10 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/MarkSectionRead/MarkSectionReadCommandHandler.cs:115` | `MarkSectionReadCommandHandler.Handle` | `scheduledTalk.SectionProgress` | SAFE - explicit Add | `scheduledTalk` loaded via `FirstOrDefaultAsync`+`Include` at lines 43-49 (already-tracked). New `ScheduledTalkSectionProgress` has a client-assigned Guid key (`Id = Guid.NewGuid()`, line 109) — same risk shape as the schedule bug — but line 116 immediately follows with `_dbContext.ScheduledTalkSectionProgress.Add(progress)` on the *same instance*, which authoritatively sets its state to `Added` regardless of the nav-collection add. |
| 11 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/Courses/Commands/CreateToolboxTalkCourseCommandHandler.cs:95` | `CreateToolboxTalkCourseCommandHandler.Handle` | `course.CourseItems` | SAFE - parent newly added | `course` is `new ToolboxTalkCourse{...}` at line 38; `_dbContext.ToolboxTalkCourses.Add(course)` at line 112 happens after the Items loop. |
| 12 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/AutoAssignmentService.cs:93` | `AutoAssignmentService.AssignNewEmployeeTrainingAsync` (course branch) | `assignment.ScheduledTalks` | SAFE - parent newly added | `assignment` is `new ToolboxTalkCourseAssignment{...}` at line 65; `_context.ToolboxTalkCourseAssignments.Add(assignment)` at line 96 happens after the per-course-item `ScheduledTalks.Add` loop, `assignment` was never tracked before that. |
| 13 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Seed/RegulatoryStructureMapSeedData.cs:74` | `RegulatoryStructureMapSeedData.SeedAsync` | `map.Principles` | SAFE - parent newly added | `map` is `new RegulatoryStructureMap{...}` at line 53; `context.Set<RegulatoryStructureMap>().AddAsync(map)` at line 109 happens after the full Principles/Standards/Features tree is built in memory, `map` was never tracked before that. |
| 14 | `RegulatoryStructureMapSeedData.cs:88` | same | `principle.Standards` | SAFE - parent newly added | `principle` is itself only reachable via `map` (added at line 74) and is never independently tracked; same single deferred `AddAsync(map)` call covers the whole graph. |
| 15 | `RegulatoryStructureMapSeedData.cs:93` | same | `standard.Features` | SAFE - parent newly added | Same reasoning, one level deeper. |
| 16 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentExtractionService.cs:515` | `ContentExtractionService` (auto-transcription path) | `subtitleJob.Translations` | SAFE - parent newly added | `subtitleJob` is `new SubtitleProcessingJob{...}` at line 499, still untracked at this call (occurs before the explicit `Add` at line 527). |
| 17 | `ContentExtractionService.cs:547` | same, target-language loop | `subtitleJob.Translations` | SAFE - parent newly added | This add happens **after** `_dbContext.SubtitleProcessingJobs.Add(subtitleJob)` (line 527). `subtitleJob`'s state is `Added` (not `Unchanged`/loaded-from-DB) at this point and throughout to `SaveChangesAsync`, so EF's cascade-from-Added-principal behaviour applies (the behaviour that is absent for an `Unchanged` principal, which is what caused the schedule bug) — new children reachable from an `Added` entity are correctly marked `Added` regardless of call order relative to the parent's own `Add()`. |
| 18 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/SubtitleProcessingOrchestrator.cs:128` | `SubtitleProcessingOrchestrator.StartAsync`(ish) | `job.Translations` | SAFE - parent newly added | `job` is `new SubtitleProcessingJob{...}` at line 116; `_dbContext.SubtitleProcessingJobs.Add(job)` at line 150 happens after both `Translations.Add` calls. |
| 19 | `SubtitleProcessingOrchestrator.cs:140` | same, target-language loop | `job.Translations` | SAFE - parent newly added | Same as above. |
| 20 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/AuditCorpusService.cs:136` | `AuditCorpusService.FreezeFromTalkAsync` | `corpus.Entries` | SAFE - parent newly added | `corpus` is `new AuditCorpus{...}` at line 95; `_dbContext.AuditCorpora.Add(corpus)` at line 140 happens after the Entries loop, `corpus` was never tracked before that. |
| 21 | `AuditCorpusService.cs:219` | `AuditCorpusService.AddEntryAsync` | `corpus.Entries` | **AT RISK** | See dedicated section below. |
| 22 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs:566` | `ContentCreationSessionService.StartTranslateValidateAsync` (new-draft branch) | `newDraftTalk.Sections` | SAFE - parent newly added | `newDraftTalk` is `new ToolboxTalk{...}` at line 546; `_dbContext.ToolboxTalks.Add(newDraftTalk)` at line 611 happens after the Sections/Questions loops. |
| 23 | `ContentCreationSessionService.cs:595` | same | `newDraftTalk.Questions` | SAFE - parent newly added | Same as above. |
| 24 | `ContentCreationSessionService.cs:1750` | `ContentCreationSessionService.PublishAsync`(ish) | `talk.Sections` | SAFE - parent newly added | `talk` is `new ToolboxTalk{...}` at line 1711; `_dbContext.ToolboxTalks.Add(talk)` at line 1771 happens after the Sections add and `SyncQuizQuestionsToTalk` call. |
| 25 | `ContentCreationSessionService.cs:1925` | `ContentCreationSessionService.PublishAsCourseAsync` (video-repurpose branch) | `course.CourseItems` | SAFE - parent newly added | `course` is `new ToolboxTalkCourse{...}` at line 1848; `_dbContext.ToolboxTalkCourses.Add(course)` at line 2142 happens after every `CourseItems.Add` call in this method (lines 1925 and 2079), `course` was never tracked before that. |
| 26 | `ContentCreationSessionService.cs:1969` | `PublishAsCourseAsync` (per-section talk loop) | `talk.Sections` | SAFE - parent newly added | `talk` is `new ToolboxTalk{...}` at line 1945, added to `_dbContext.ToolboxTalks` at line 1971, immediately after the single `Sections.Add`; `talk` was never tracked before that. |
| 27 | `ContentCreationSessionService.cs:2079` | `PublishAsCourseAsync` (per-section talk loop, course item) | `course.CourseItems` | SAFE - parent newly added | Same `course` / same deferred `Add(course)` at line 2142 as row 25. |

### Call sites that matched the search keywords but are not navigation-collection adds (false positives, ruled out by reading)

These are listed because they matched the broad `\.(PropertyName)\.Add\(` grep (the
property names collide with `DbSet<T>` property names on the DbContext, or with local
variable names) and needed to be read to confirm they are not the risky pattern:

| File:Line | What it actually is |
|---|---|
| `src/Core/QuantumBuild.Core.Application/Features/Contacts/ContactService.cs:277` | `_context.Contacts.Add(contact)` — `DbSet<Contact>`, not `Company.Contacts` nav collection. |
| `src/Core/QuantumBuild.Core.Application/Features/Employees/SupervisorAssignmentService.cs:187` | `_context.SupervisorAssignments.Add(assignment)` — `DbSet<SupervisorAssignment>`, not `Employee.SupervisorAssignments`/`OperatorAssignments`. |
| `src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs:253` | `_context.Sites.Add(site)` — `DbSet<Site>`, not `Company.Sites`. |
| `src/Modules/ToolboxTalks/.../Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs:191` | `_dbContext.ScheduledTalks.Add(scheduledTalk)` — `DbSet<ScheduledTalk>`, not `ToolboxTalkCourseAssignment.ScheduledTalks`. |
| `src/Modules/ToolboxTalks/.../Services/AutoAssignmentService.cs:137` | `_context.ScheduledTalks.Add(scheduledTalk)` — `DbSet<ScheduledTalk>` (standalone-talk branch). |
| `src/Modules/ToolboxTalks/.../Services/RefresherSchedulingService.cs:59,153` | `context.ScheduledTalks.Add(...)` — `DbSet<ScheduledTalk>`, not a nav collection on the newly created `ScheduledTalk`/`ToolboxTalkCourseAssignment` refresher records. |
| `src/Modules/ToolboxTalks/.../Services/Sectors/TenantSectorService.cs:93` | `dbContext.TenantSectors.Add(tenantSector)` — `DbSet<TenantSector>`, not `Sector.TenantSectors`. |
| `src/QuantumBuild.API/Controllers/QrLocationController.cs:348` | `_dbContext.QrCodes.Add(code)` — `DbSet<QrCode>`, not `QrLocation.QrCodes`; the location itself is only existence-checked (`AnyAsync`), never loaded as a tracked entity. |
| `src/QuantumBuild.API/Controllers/QrScanController.cs:447` | `_db.ScheduledTalks.Add(scheduledTalk)` — `DbSet<ScheduledTalk>`. |
| `src/Modules/ToolboxTalks/.../Services/QuizGenerationService.cs:83` | `result.Questions.Add(generated)` — `result` is `RandomizedQuiz`, an in-memory DTO, not an EF entity; `Questions` is `List<RandomizedQuizQuestion>`, not a DbContext navigation property. N/A. |
| `src/Modules/ToolboxTalks/.../Persistence/Seed/RegulatoryProfileSeedData.cs:364` | `newCriteria.Add(new RegulatoryCriteria{...})` — `newCriteria` is a local `List<RegulatoryCriteria>`, later bulk-inserted via `context.Set<RegulatoryCriteria>().AddRangeAsync(newCriteria)` (line 380). Not a nav-collection add. |

### Other write paths checked and confirmed to use the safe explicit-`Add` pattern (no nav-collection add present at all)

Checked because they create children of already-tracked parents and could plausibly have
used the risky idiom, but do not:

- `AddCourseItemCommandHandler.cs:67` and `UpdateCourseItemsCommandHandler.cs:98` — both add new `ToolboxTalkCourseItem` rows to an **already-tracked, DB-loaded** `course` via `_dbContext.ToolboxTalkCourseItems.Add(item)`, never via `course.CourseItems.Add(item)`.
- `SubmitQuizAnswersCommandHandler.cs:173-186` — new `ScheduledTalkQuizAttempt` added to an already-tracked `scheduledTalk` via `_dbContext.ScheduledTalkQuizAttempts.Add(quizAttempt)`, never via `scheduledTalk.QuizAttempts.Add(...)` (only `.Count` is read from the nav collection).
- `GenerateContentTranslationsCommandHandler.cs:463-467` and `:666-672` — new `ToolboxTalkSlideTranslation`/`ToolboxTalkSlideshowTranslation` added to an already-tracked `toolboxTalk`/`slide` via `_context.ToolboxTalkSlideTranslations.Add(...)` / `_context.ToolboxTalkSlideshowTranslations.Add(...)`. The slideshow-translation call site carries the comment *"Save the translation using DbSet.Add() for reliable change tracking"*, suggesting this was a deliberate choice.
- `TranslationValidationService.cs:218-229` (`ValidateSectionAsync`, upsert pattern documented in CLAUDE.md as Note 15/1) — queries for an existing `TranslationValidationResult` by `{ValidationRunId, SectionIndex}`; if absent, constructs a new instance and adds it via `dbContext.TranslationValidationResults.Add(entity)` when `entity.Id == Guid.Empty` — explicit `DbSet` add, and the key here is left at the CLR default (not client-pre-assigned), so this is doubly safe.
- `SafetyGlossaryController.cs:223` and `:362-372` — new `SafetyGlossaryTerm` rows added via `_dbContext.SafetyGlossaryTerms.Add(...)`, never via `glossary.Terms.Add(...)`.
- `RequirementMappingJob.cs:394-404` — new `RegulatoryRequirementMapping` added via `_dbContext.RegulatoryRequirementMappings.Add(mapping)`, never via `requirement.Mappings.Add(...)`.
- `PipelineVersionService.cs:152-167` — new `PipelineChangeRecord` added via `_dbContext.PipelineChangeRecords.Add(record)`, never via `pipelineVersion.ChangeRecords.Add(...)`.
- `StartTalkTranslationCommandHandler.cs:86`, `ContentCreationSessionService.cs:686`, `TranslationValidationController.cs:96` — new `TranslationValidationRun` added via `_context.TranslationValidationRuns.Add(run)` / `_dbContext.TranslationValidationRuns.Add(run)`, never via `pipelineVersion.Runs.Add(...)`.
- `CorpusRunJob.cs:198,244` — new `CorpusRunResult` rows added via `_dbContext.CorpusRunResults.Add(...)`, never via `run.Results.Add(...)`.

---

## AT RISK — detail

### `AuditCorpusService.AddEntryAsync` — `corpus.Entries.Add(entry)`

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/AuditCorpusService.cs`
**Lines:** 183-227 (method body); the risky add is line 219.

```
178:    public async Task<AuditCorpus> AddEntryAsync(
179:        Guid corpusId, AddCorpusEntryRequest request, CancellationToken ct = default)
180:    {
181:        var tenantId = _currentUser.TenantId;
182:
183:        var corpus = await _dbContext.AuditCorpora
184:            .Include(c => c.Entries)
185:            .FirstOrDefaultAsync(c => c.Id == corpusId && c.TenantId == tenantId, ct)
186:            ?? throw new InvalidOperationException($"Corpus {corpusId} not found");
...
199:        var entry = new AuditCorpusEntry
200:        {
201:            Id = Guid.NewGuid(),
202:            CorpusId = corpus.Id,
...
217:        };
218:
219:        corpus.Entries.Add(entry);
220:        corpus.Version += 1;
221:        corpus.UpdatedAt = DateTime.UtcNow;
222:        corpus.UpdatedBy = _currentUser.UserName;
223:
224:        await _dbContext.SaveChangesAsync(ct);
225:
226:        return corpus;
227:    }
```

**Why this matches all three risk conditions:**

- **(A) Client-assigned key.** `AuditCorpusEntry.Id = Guid.NewGuid()` is set explicitly at line 201, before the entity is added to the graph (`AuditCorpusService.cs:201`). The entity's key is `Guid Id` inherited from `BaseEntity` (`src/Core/QuantumBuild.Core.Domain/Common/BaseEntity.cs:11`), and `AuditCorpusEntryConfiguration` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/AuditCorpusEntryConfiguration.cs`) declares only `builder.HasKey(e => e.Id)` (line 13) with no `.ValueGeneratedNever()`/`.ValueGeneratedOnAdd()` override — the same "no explicit key-generation config, client sets the Guid in code" shape as `ToolboxTalkScheduleAssignment` (`ToolboxTalkScheduleAssignmentConfiguration.cs:18`, also just `builder.HasKey(a => a.Id)` with no override), the entity type at the centre of the fixed bug.
- **(B) Already-tracked parent, attached via nav collection.** `corpus` is loaded from the database via `_dbContext.AuditCorpora.Include(c => c.Entries).FirstOrDefaultAsync(...)` (lines 183-185) — an `Unchanged` (from-DB) entity, not one created and `Add()`-ed in this method — and the new `entry` is attached via `corpus.Entries.Add(entry)` (line 219), not via `_dbContext.Set<AuditCorpusEntry>().Add(entry)` or `_dbContext.AuditCorpusEntries.Add(entry)`. This is structurally identical to the fixed schedule bug, where `schedule` was loaded via `FirstOrDefaultAsync` and the new child was attached via `schedule.Assignments.Add(assignment)`.
- **(C) `SaveChangesAsync` expecting an insert.** Line 224 calls `await _dbContext.SaveChangesAsync(ct)` expecting `entry` to be inserted.
- **No mitigation present.** Unlike the three now-fixed schedule handlers, there is no `_dbContext.Entry(entry).State = EntityState.Added;` (or equivalent) anywhere in this method.

**Expected failure mode**, by direct analogy with the fixed bug: EF's `DetectChanges()` at `SaveChangesAsync` time discovers `entry` as newly reachable from the `Unchanged` `corpus` via the `Entries` navigation. Because `entry.Id` is a non-default (client-set) `Guid` and its principal (`corpus`) is not itself `Added`, EF's default heuristic classifies `entry` as `Modified` rather than `Added`, and issues an `UPDATE` matching zero rows on the primary key `entry.Id` — followed by a `DbUpdateConcurrencyException` from `SaveChangesAsync`, exactly as described in the `5104b20` commit message for the schedule assignments.

**Reachability:** this is a live, callable endpoint — `PipelineAuditController.AddCorpusEntry` (`src/QuantumBuild.API/Controllers/PipelineAuditController.cs:810-821`), `POST /api/pipeline-audit/corpus/{id}/entries`, gated by `[Authorize(Policy = "Learnings.Manage")]`, calling `_corpusService.AddEntryAsync(id, request, cancellationToken)` at line 821. It is the manual "add one more reference entry to an existing (already-frozen-from-a-talk) corpus" path described in CLAUDE.md Note 8 (Translation Pipeline Audit Controls — Phase 4 Corpus).

**Test coverage:** none found. `grep -rli "corpus" tests --include="*.cs"` across `tests/QuantumBuild.Tests.Unit`, `tests/QuantumBuild.Tests.Integration`, `tests/QuantumBuild.Tests.Common`, and `tests/QuantumBuild.Tests.E2E` returns zero files. No unit test for `AuditCorpusService`, no integration test exercising `POST /api/pipeline-audit/corpus/{id}/entries`. This mirrors exactly how the schedule bug was invisible before being hit in practice (the commit that fixed it added the first characterisation tests, `ScheduleTargetingUpdateAndRefreshTests.cs`, in the same commit as the fix).

**Entity types involved:** `AuditCorpus` (parent, `TenantEntity`), `AuditCorpusEntry` (child, `BaseEntity`, scoped via parent per CLAUDE.md Note 8).

**Handler:** `AuditCorpusService.AddEntryAsync` (`IAuditCorpusService`), invoked from `PipelineAuditController.AddCorpusEntry`.

Note: the sibling method `FreezeFromTalkAsync` in the same file (row 20 in the table above)
uses the identical `corpus.Entries.Add(entry)` idiom but is safe, because in that method
`corpus` is a fresh `new AuditCorpus{...}` that is not added to `_dbContext.AuditCorpora`
until *after* the `Entries.Add` loop completes (line 140) — i.e. it is the "create" path,
directly analogous to the never-affected `CreateToolboxTalkScheduleCommandHandler`. Only
`AddEntryAsync` (the "update an existing corpus" path, analogous to the fixed
`UpdateToolboxTalkScheduleCommandHandler`) is at risk.

---

## Summary verdict

- **Total AT RISK instances: 1** — `AuditCorpusService.AddEntryAsync` (`corpus.Entries.Add(entry)`), `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/AuditCorpusService.cs:219`.
- **Feature/handler:** Translation Pipeline Audit Controls, Phase 4 (Corpus) — "add a manual entry to an existing corpus" (`POST /api/pipeline-audit/corpus/{id}/entries`).
- **Classification is LOCALISED, with one new isolated finding, not WIDESPREAD.** The
  risky idiom (new entity, client-assigned key, attached via nav collection on an
  already-tracked/DB-loaded parent, no explicit state override) was not found to recur
  across unrelated features. Every other genuine navigation-collection `.Add()` in the
  codebase either targets a parent that is newly created and added within the same
  method (the common, safe idiom used throughout `ContentCreationSessionService`,
  `LessonGeneratorService`, seed data, and the *create* schedule/course handlers), or
  the same child instance is also explicitly added to its `DbSet` (the pattern already
  used correctly in `MarkSectionReadCommandHandler`), or the codebase avoids the nav
  collection entirely and always uses an explicit `DbSet.Add()` for children of
  already-tracked parents (the pattern used consistently for course items, quiz
  attempts, slide/slideshow translations, requirement mappings, pipeline change
  records, validation runs, corpus run results, and glossary terms).
- **Recommendation:** given the count is 1, a narrow, targeted fix mirroring the
  `5104b20` pattern (add `_dbContext.Entry(entry).State = EntityState.Added;`
  immediately after `corpus.Entries.Add(entry)` in `AuditCorpusService.AddEntryAsync`,
  plus a characterisation test analogous to `ScheduleTargetingUpdateAndRefreshTests.cs`)
  is proportionate. A broader standing audit-and-fix chunk does not appear warranted —
  the sweep found the idiom is not a recurring habit in this codebase; this is a single
  missed instance of the same underlying trap, not a systemic pattern.

---

## Search methodology

1. **Enumerated every EF navigation-collection property** on entities under
   `src/**/Domain/Entities/**/*.cs` via:
   ```
   grep -rnE "public (virtual )?(ICollection|IList|List|HashSet|IReadOnlyCollection)<" src --include="*.cs" | grep -i "Domain.Entities"
   ```
   This produced 37 distinct navigation-collection properties across Core
   (`Company.Sites`, `Company.Contacts`, `Employee.SupervisorAssignments`,
   `Employee.OperatorAssignments`, `LookupCategory.Values`,
   `LookupCategory.TenantValues`, `Permission.RolePermissions`,
   `Role.RolePermissions`, `Role.UserRoles`, `User.UserRoles`) and ToolboxTalks
   (`AuditCorpus.Entries`, `AuditCorpus.Runs`, `CorpusRun.Results`,
   `PipelineVersion.Runs`, `PipelineVersion.ChangeRecords`, `QrLocation.QrCodes`,
   `RegulatoryBody.Documents`, `RegulatoryDocument.Profiles`,
   `RegulatoryProfile.Criteria`, `RegulatoryRequirement.Mappings`,
   `RegulatoryStructureMap.Principles`, `RegulatoryStructureMapPrinciple.Standards`,
   `RegulatoryStructureMapStandard.Features`, `SafetyGlossary.Terms`,
   `ScheduledTalk.SectionProgress`, `ScheduledTalk.QuizAttempts`,
   `Sector.TenantSectors`, `SubtitleProcessingJob.Translations`,
   `ToolboxTalk.Sections`, `ToolboxTalk.Questions`, `ToolboxTalk.Translations`,
   `ToolboxTalk.VideoTranslations`, `ToolboxTalk.Slides`,
   `ToolboxTalk.SlideshowTranslations`, `ToolboxTalkCourse.CourseItems`,
   `ToolboxTalkCourse.Translations`, `ToolboxTalkCourseAssignment.ScheduledTalks`,
   `ToolboxTalkSchedule.Assignments`, `ToolboxTalkSlide.Translations`,
   `TranslationValidationResult.Flags`, `TranslationValidationRun.Results`).
2. **Grepped the whole `src/` tree** (Core, both ToolboxTalks projects, and the
   LessonParser module, plus `QuantumBuild.API`) for `.<PropertyName>.Add(` and
   `.<PropertyName>.AddRange(` for every property name from step 1 in one combined
   regex, e.g.:
   ```
   grep -rnE "\.(Sites|Contacts|SupervisorAssignments|...|Flags)\.Add(Range)?\(" src --include="*.cs" -l
   ```
   This returned 22 candidate files, each manually opened and read in full context
   (not just the matched line) to determine: (a) whether the receiver is a genuine
   EF navigation-collection property on a tracked entity vs. a `DbSet<T>` property on
   the DbContext vs. an unrelated local variable/DTO collection that happens to share
   the property name; (b) if genuine, whether the parent was loaded from the database
   (`FirstOrDefaultAsync`/`FindAsync`/`Include` etc.) earlier in the same method, or
   newly constructed (`new Entity{...}`) and not yet added to any `DbSet`; (c) where
   the parent's own `_dbContext.Add()`/`DbSet.Add()` call falls relative to the
   navigation-collection `.Add()` calls, to determine the parent's tracked `EntityState`
   at the time each child is attached and at `SaveChangesAsync` time; (d) whether an
   explicit `_dbContext.Entry(child).State = EntityState.Added` (or an explicit
   `DbSet<T>.Add(child)` on the same instance) is already present.
3. **Ruled out false positives** by reading surrounding code where a property name
   from step 1 collided with a `DbSet<T>` property name on the DbContext interface
   (e.g. `_dbContext.ScheduledTalks.Add(...)`, `_context.Sites.Add(...)`) or with an
   unrelated local variable (`newCriteria.Add(...)`, `result.Questions.Add(...)` on an
   in-memory DTO).
4. **Cross-checked entity key-generation strategy** for the one AT RISK finding by
   reading its `IEntityTypeConfiguration<T>` class
   (`AuditCorpusEntryConfiguration.cs`) and `BaseEntity.cs`, and compared it against
   the equivalent configuration for the entity type at the centre of the original bug
   (`ToolboxTalkScheduleAssignmentConfiguration.cs`) to confirm both use the same
   "no explicit `ValueGenerated*` override, `Guid` assigned client-side in code"
   convention.
5. **Searched for test coverage** of the one AT RISK finding via
   `grep -rli "corpus" tests --include="*.cs"` across all four test projects
   (`QuantumBuild.Tests.Unit`, `.Integration`, `.Common`, `.E2E`) — zero matches — and
   confirmed the fixed schedule bug now has dedicated coverage via
   `find tests -iname "*Schedule*"`, which surfaces
   `tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingUpdateAndRefreshTests.cs`
   (added in the same commit as the fix, per `git show 5104b20 --stat`).
6. **Verified reachability** of the AT RISK finding from a real, authorized API
   endpoint by grepping `AddEntryAsync` usage across `src/` and reading
   `PipelineAuditController.cs:809-821`.

Not covered / out of scope for this recon: EF Core internal source review (classification
of "SAFE - parent newly added" vs "AT RISK" relies on the documented/observed behaviour
from the `5104b20` fix commit and on standard EF Core change-tracking conventions for
mixed-state graphs, not on decompiling EF Core itself); the `web/` TypeScript frontend
(not applicable — no EF change tracking there); migration `HasData()` seed calls (not
runtime `DbContext.Add()` calls, a structurally different and non-risky mechanism not in
scope per the task's own risk-condition definition).
