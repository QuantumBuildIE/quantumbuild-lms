# Bulk Import "Cross-Tenant Dedup Shortcut" — Recon

**Read-only recon. No code changed.** Branch: `transval`. All claims cite `file:line`, paths
relative to repo root. This investigation was scoped to confirm or refute a specific hypothesis:
that a name-based existence/dedup check, not scoped by `TenantId`, diverts a bulk-import upload
for one tenant onto a "reuse existing content" shortcut when a same-named learning already exists
in a *different* tenant, producing an incomplete (no sections, no quiz) Draft for the second
tenant.

**Verdict up front: the hypothesis is not supported by the code.** Every existence/dedup check in
this codebase is correctly scoped by `TenantId` — none of them can see across tenants, and none of
them produce the observed symptom. A different, confirmed defect — unrelated to deduplication or
tenant scoping — fully explains the observed evidence: a silent "succeeded with zero content" path
in the AI section-parsing step, which only bulk import (not the wizard) is exposed to. Details in
§B.

---

## A. The existence/dedup check(s) — confirmed, all tenant-scoped

Four independent existence/dedup mechanisms exist in this codebase. All four filter explicitly by
`TenantId`. None match or reuse content across tenants.

| # | Mechanism | Location | Matches on | Tenant-scoped? |
|---|---|---|---|---|
| 1 | Bulk import's own per-item title pre-check | `src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs:210-214` | `Title` (exact, case-sensitive — see §C.1) | **Yes** — `t.TenantId == tenantId && t.Title == title` |
| 2 | `InitialiseToolboxTalkCommand`'s title/code uniqueness guard (used by both bulk import and the wizard) | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandHandler.cs:36-50` | `Title`, `Code` (exact) | **Yes** — `t.TenantId == request.TenantId` in both queries |
| 3 | `ContentDeduplicationService.CheckForDuplicateAsync` / `ReuseContentAsync` (wizard-only manual "check-duplicate" / "reuse-content" / "smart-generate" flow) | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentDeduplicationService.cs:68-95` (check), `:141-185` (reuse) | SHA-256 file hash (`PdfFileHash`/`VideoFileHash`) | **Yes** — every query filters `t.TenantId == tenantId`; the controller passes `_currentUserService.TenantId` (`src/QuantumBuild.API/Controllers/ToolboxTalksController.cs:1068,1096,1151,1164,1217,1248`), never a caller-supplied tenant |
| 4 | `ContentCreationSessionService`'s own title/code uniqueness checks (legacy wizard) | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs:1300,1425,1466,1686,1696,1806` | `Title`, `Code` | **Yes** — every query includes `t.TenantId == tenantId` |

**Bulk import itself never calls mechanism 3** (`check-duplicate`/`reuse-content`/`update-file-hash`)
at all — confirmed by a full read of `BulkSopImportJob.cs`: it calls `InitialiseToolboxTalkCommand`,
`ParseToolboxTalkContentCommand`, and `GenerateToolboxTalkQuizCommand` directly
(`BulkSopImportJob.cs:251-259, 275-276, 287-288`), with no reference to
`IContentDeduplicationService` anywhere in the file. Mechanism 3 is a manual, admin-triggered
action in the single-talk wizard only (`ToolboxTalksController.cs:1050-1257`, all three actions
gated `[Authorize(Policy = "Learnings.Admin")]`).

### A.1 — Was this ever *not* tenant-scoped in an earlier version?

Checked via `git log -p --follow` on `BulkSopImportJob.cs`: the title-existence check has been
tenant-scoped (`t.TenantId == tenantId && t.Title == title`) since the line was first introduced —
it has never been broadened across tenants at any point in its history. The most recent commit
touching this file, `d265846` ("Fix bulk SOP import: persist PDFs to permanent storage and
disambiguate titles", 2026-09-15), added *intra-tenant, intra-ZIP* subfolder disambiguation (two
same-basename PDFs in different ZIP subfolders within one upload) — a same-tenant collision fix,
unrelated to cross-tenant matching.

### A.2 — Does the same check affect the wizard (non-bulk) creation path?

Mechanism 2 (`InitialiseToolboxTalkCommand`) is shared code, used by both the wizard and bulk
import, and is tenant-scoped in both cases — so no, there is no cross-tenant title/code collision
possible via any path in this codebase.

---

## B. The actual confirmed mechanism producing the incomplete Draft

Reading the full `ParseToolboxTalkContentCommand → ContentParserService → GenerateToolboxTalkQuizCommand`
chain that `BulkSopImportJob` calls per item surfaced a real, confirmed defect that produces exactly
the symptom described (a Draft with **no sections and no quiz**, reported as a normal outcome) —
with no dedup, no cross-tenant read, and no title-matching involved at all.

### B.1 — Where an AI parse can silently return zero sections as a "success"

`ContentParserService.ParseSectionsFromContentText` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs:218-248`):

```csharp
var jsonStart = textContent.IndexOf('[');
var jsonEnd = textContent.LastIndexOf(']');

if (jsonStart >= 0 && jsonEnd > jsonStart)
{
    // ...populates `sections`...
}

return sections;   // still an empty List<ParsedSection> if the guard above was false
```
(`ContentParserService.cs:223-247`)

If Claude's response text for the section-generation prompt contains **no `[` character at all**
— a plain-text refusal, a safety-filtered reply, a truncated/malformed response, or any reply that
doesn't lead with the requested JSON array — this method returns an **empty list, without throwing
and without any error**.

The caller, `ParseContentAsync` (`ContentParserService.cs:123-147`), does not check the section
count before building its result:

```csharp
var parsed = AnthropicResponseParser.Parse(responseBody);
var sections = ParseSectionsFromContentText(parsed.ContentText);
...
return new ContentParseResult(
    Success: true,           // <-- always true here, regardless of sections.Count
    Sections: sections,      // <-- may be empty
    SuggestedOutputType: suggestedType,
    TokensUsed: tokensUsed);
```
(`ContentParserService.cs:143-147`)

There is no `if (sections.Count == 0)` guard anywhere in `ParseContentAsync`. Contrast this with
the method's own `JsonException` catch block (`:158-166`), which *does* correctly return
`Success: false` if the substring between `[` and `]` fails to parse — that is a different,
already-handled failure mode. The zero-sections case is the one path that falls through as a false
success.

### B.2 — The caller trusts `Success: true` completely

`ParseToolboxTalkContentCommandHandler.HandlePdfAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ParseToolboxTalkContent/ParseToolboxTalkContentCommandHandler.cs:90-131`):

```csharp
var parseResult = await _contentParserService.ParseContentAsync(...);

if (!parseResult.Success) { ... return Result.Fail<ToolboxTalkDto>(...); }

var newSections = await MaterialiseSectionsAsync(
    talk, parseResult.Sections, ContentSource.Pdf, ct);   // adds 0 sections if parseResult.Sections is empty

talk.GeneratedFromPdf = true;
talk.Status = ToolboxTalkStatus.Draft;
talk.LastEditedStep = 2;
await _dbContext.SaveChangesAsync(ct);

return Result.Ok(MapToDto(talk, newSections));   // reports success with an empty Sections list
```
(`ParseToolboxTalkContentCommandHandler.cs:111-130`)

`MaterialiseSectionsAsync` (`:200-234`) soft-deletes any prior sections and adds one new
`ToolboxTalkSection` per entry in `parsedSections` — zero entries means zero sections added, with
no error raised. The talk is committed to `Draft` status with `LastEditedStep = 2` and the command
returns `Result.Ok`.

### B.3 — `GenerateToolboxTalkQuizCommand` *does* catch this — but the caller downgrades it

`GenerateToolboxTalkQuizCommandHandler.Handle` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateToolboxTalkQuiz/GenerateToolboxTalkQuizCommandHandler.cs:46-52`):

```csharp
var sections = await _dbContext.ToolboxTalkSections
    .Where(s => s.ToolboxTalkId == talk.Id && !s.IsDeleted)
    .OrderBy(s => s.SectionNumber)
    .ToListAsync(ct);

if (sections.Count == 0)
    return Result.Fail<ToolboxTalkDto>("Cannot generate a quiz without sections. Parse content first.");
```

This correctly fails. But `BulkSopImportJob.ProcessItemAsync` treats *any* quiz-generation failure
as a non-fatal warning, not an item failure:

```csharp
var quizResult = await sender.Send(new GenerateToolboxTalkQuizCommand(talk.Id, tenantId, UserId: null), ct);

string? warning = null;
if (!quizResult.Success)
{
    warning = $"Quiz generation failed: {string.Join("; ", quizResult.Errors)}. " +
        "The learning was created with sections but no quiz — quiz can be generated manually.";
    ...
}
...
return new BulkSopImportItemOutcome
{
    ...
    Status = BulkSopImportItemStatus.Succeeded,   // <-- always Succeeded here, even with 0 sections
    ...
    Warning = warning
};
```
(`BulkSopImportJob.cs:287-326`)

The warning text itself ("created with sections but no quiz") assumes the only failure mode is a
quiz-specific AI error on top of otherwise-real sections — it does not anticipate the section count
also being zero. The item is still recorded `Succeeded`.

### B.4 — Net effect, confirmed by tracing every hop

A bulk-import PDF whose Claude parse response happens to contain no JSON array produces, end to
end: a `ToolboxTalk` row with `Status = Draft`, zero `ToolboxTalkSection` rows, zero
`ToolboxTalkQuestion` rows, correctly tagged with the importing tenant's own `TenantId` — reported
to the admin as **`Succeeded`** (with an easy-to-miss warning) in the bulk-import results panel.
This is precisely the "incomplete lesson (no quiz, no sections)" symptom in the reported evidence.
No dedup check, no cross-tenant read, and no title-matching participates in producing this state —
the two same-titled talks in different tenants are two independent, unrelated creations. See §C for
why the same title is unsurprising.

---

## C. Scope / impact

### C.1 — Exact match semantics of the (correctly tenant-scoped) title checks

`ToolboxTalk.Title` is `character varying(200)` with no citext/case-insensitive collation override
(`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ToolboxTalkConfiguration.cs:26-28`).
Every check above uses plain `==` (`t.Title == title`), which Npgsql translates to exact,
case-sensitive equality on the default collation — not a fuzzy or normalized match.

### C.2 — Why the same title across two tenants is expected, not suspicious

Multiple client organizations independently uploading a standardized/numbered safety SOP (e.g. a
"007 Competent Workforce" module from a shared industry template or franchise pack) is exactly the
scenario the original hypothesis's own framing anticipated ("many tenants with same-named standard
SOPs... Fire Safety, Hand Safety"). Since every title check in this codebase is tenant-scoped
(§A), this is fully legitimate and requires no fix — each tenant's talk is independent, and the
system is not supposed to treat them as related just because the title string matches.

### C.3 — Blast radius of the real defect (§B)

- **Trigger condition:** Claude's response to the section-generation prompt contains no `[`
  character — a low-frequency but real occurrence (refusal, safety-filtered reply, or a
  malformed/off-format response). The codebase has at least one confirmed precedent of "imperfect
  LLM output breaking strict parsing" in an unrelated service, fixed the same day as the most
  recent bulk-import commit (`f6dfb53`, "Fix PreFlightScanService JSON parsing crash on imperfect
  LLM output") — indicating this class of failure is a recurring, not hypothetical, risk in this
  codebase's AI integrations.
- **Frequency multiplier:** every PDF in a bulk-import ZIP is an independent Claude call
  (`BulkSopImportJob.cs:114-119`, one `ProcessItemAsync` per file) — a batch of N PDFs has N
  independent chances to hit this, unlike a single wizard-driven talk.
- **Affects bulk import only.** See §C.4 — the wizard has a client-side gate that bulk import does
  not.

### C.4 — Does this affect the wizard path too?

`ParseToolboxTalkContentCommand`/`ContentParserService` are shared by both paths, so the same
silent-empty-result defect (§B.1–B.2) is technically reachable from the wizard too. **But the
wizard's frontend blocks a human from ever proceeding on zero sections:**

- `parseStepSchema.ts:12` — `sections: z.array(sectionSchema).min(1, 'At least one section is required')`
- `ParseStep.tsx:365` — the Continue button is `disabled={isSaving || sections.length === 0}`
- `QuizStep.tsx:501-503` — if reached anyway, shows "No sections found. Go back to the Parse step to add sections."
- The legacy wizard's own `ParseStep.tsx:368` logs `'Parse completed but no sections were extracted'`, confirming the team already anticipated this exact AI failure mode there.

So a human using either wizard sees the problem immediately and cannot publish an empty talk.
**Bulk import has no human review step between parse and quiz generation** — it runs the full
chain unattended and reports the result as `Succeeded`. The underlying AI/parsing defect (§B) is
shared code, but only bulk import can produce a genuinely incomplete, unreviewed, "Succeeded"
Draft from it.

### C.5 — Finding affected talks (read-only query, run against the local dev DB)

The local dev database (`rascor_stock`) does not contain the specific tenant IDs / talk referenced
in the reported evidence (`01a041a9…`, `01a0a527…`) — that data lives in a Development or
Production environment this session has no credentials for, so the exact two rows could not be
inspected directly. The query below (not run against a populated environment in this session) is
the correct read-only diagnostic to size the blast radius once run against an environment that has
bulk-import history:

```sql
-- Draft talks with zero sections and zero questions, that were created via bulk import
-- (BulkTranslationPendingSince is only ever set by BulkSopImportJob, per §B.4 / BulkSopImportJob.cs:311-316)
SELECT t."Id", t."TenantId", t."Title", t."CreatedAt"
FROM toolbox_talks."ToolboxTalks" t
WHERE t."Status" = 0 -- Draft
  AND t."BulkTranslationPendingSince" IS NOT NULL
  AND NOT t."IsDeleted"
  AND NOT EXISTS (SELECT 1 FROM toolbox_talks."ToolboxTalkSections" s WHERE s."ToolboxTalkId" = t."Id" AND NOT s."IsDeleted")
  AND NOT EXISTS (SELECT 1 FROM toolbox_talks."ToolboxTalkQuestions" q WHERE q."ToolboxTalkId" = t."Id");
```

---

## D. Fix surface (not implemented — for scoping only)

1. **Primary fix — make the empty-sections case an explicit failure, not a silent success.**
   `ContentParserService.ParseContentAsync` (`ContentParserService.cs:123-147`) should check
   `sections.Count == 0` after calling `ParseSectionsFromContentText` and return
   `Success: false` with a clear `ErrorMessage` (e.g. "AI parsing returned no sections") instead of
   always returning `Success: true`. This single change causes the *existing*
   `if (!parseResult.Success)` branch in both `ParseToolboxTalkContentCommandHandler` (all four
   `Handle*Async` methods share the same pattern, e.g. `:100-106`) and `BulkSopImportJob.cs:278-285`
   to fire correctly — the item would be recorded `Failed` with a legible reason instead of a
   silently-incomplete `Succeeded` Draft, and the wizard would surface the same clear error
   immediately at the Parse step instead of only catching it two steps later at Quiz.
2. **Defense in depth (optional, not required if #1 lands).** `BulkSopImportJob.ProcessItemAsync`
   (`:287-298`) could distinguish "quiz failed but sections exist" (current warning behavior,
   correct) from "quiz failed because there are zero sections" (`talk.Sections.Count == 0`) and
   record the latter as `Failed`, not `Succeeded`.
3. **No tenant-scoping change is needed anywhere.** Every existence/dedup check found in this
   codebase (§A) is already correctly scoped by `TenantId`. There is no fix surface on that axis.
4. **Intra-tenant dedup intent — not a bug, no action needed.** The existing intra-tenant title
   uniqueness check (mechanism 1 and 2 in §A) is a deliberate guard against accidentally re-creating
   the same learning twice within one tenant (e.g. a re-run of the same bulk-import ZIP, handled
   explicitly via the `isRerun` branch at `BulkSopImportJob.cs:216-233`) — this is working as
   intended and out of scope for any fix.

---

## Summary

- **The reported hypothesis — a name-based existence check not scoped by `TenantId`, causing a
  cross-tenant "reuse" shortcut — is refuted.** All four existence/dedup mechanisms in this
  codebase (§A) filter explicitly by `TenantId`; none of them have ever been broadened across
  tenants; bulk import doesn't even call the one mechanism (`ContentDeduplicationService`) that
  does content-reuse at all.
- **The real, confirmed mechanism (§B):** `ContentParserService.ParseSectionsFromContentText`
  (`ContentParserService.cs:218-248`) silently returns an empty section list — with
  `ContentParseResult.Success` still `true` — whenever Claude's parse response contains no `[`
  character. `ParseToolboxTalkContentCommandHandler` commits this as a normal Draft with zero
  sections, and `BulkSopImportJob` downgrades the resulting (correctly-detected)
  "cannot generate quiz without sections" failure to a `Warning`, still reporting the item
  `Succeeded`. This produces a Draft with no sections and no quiz, correctly tenant-tagged, that
  looks exactly like the reported evidence — coincidentally sharing a title with an unrelated,
  correctly-processed talk in another tenant.
- **Blast radius:** bulk import only, not the wizard — the wizard's frontend
  (`parseStepSchema.ts:12`, `ParseStep.tsx:365`) hard-blocks progression on zero sections; bulk
  import has no human review step and reports the defect as a success. Frequency depends on how
  often Claude's response to the section-generation prompt omits a JSON array entirely — a
  real, if uncommon, class of failure with a same-day precedent elsewhere in this codebase
  (`f6dfb53`).
- **Fix surface:** make `ContentParserService.ParseContentAsync` return `Success: false` when
  zero sections are parsed (§D.1) — a single, localized change that lets the existing
  failure-handling branches in both the wizard and bulk-import paths do the right thing
  automatically. No tenant-scoping fix is needed anywhere.
