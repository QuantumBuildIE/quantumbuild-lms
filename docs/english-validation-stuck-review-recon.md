# English / "Stuck Pending Review" Recon

Read-only recon. No code was modified for this document. All claims below are
sourced to file:line and, where relevant, to git history. Where I could not
verify something without a running system or DB access, I say so explicitly.

## TL;DR — the working theory is half right, and the real mechanism is the opposite of what it describes

The working theory in the brief was: *"English still gets queued/marked for
translation validation or review, creating an un-actionable review for content
that has nothing to validate."*

That is **not** what happens. I could find no code path where English-source
content is wrongly *enqueued* into TransVal. The regression runs the other way:

- When a learning has **no target languages** (which, since two separate
  "make translation optional" changes, is now a fully legitimate state —
  see §A), **zero** `TranslationValidationRun` rows are ever created for it
  (§B). This is correct behaviour — there is genuinely nothing to
  translate/validate.
- But `RequirementMappingJob` (AI regulatory-requirement mapping) is
  completely language-agnostic and fires on every publish regardless of
  target-language configuration (§B, §D). It happily creates
  `RegulatoryRequirementMapping` rows for English-only content.
- The Compliance page's coverage calculation
  (`RequirementMappingService.GetComplianceChecklistAsync`) was written
  **before** either "translation optional" change shipped, and it classifies
  a requirement as "Covered" only if a *confirmed* mapping has an associated
  *completed* `TranslationValidationRun` — with no allowance for content that
  structurally will never have one (§C).
- The result: any published, English-only (no target language) talk/course
  that gets mapped to a regulatory requirement is **permanently** stuck at
  "Pending" coverage. The "Review Mapping" link on that row points to the
  mappings-review page, which only lists mappings that are still
  `Suggested` — once the mapping is `Confirmed` (by anyone, any route),
  there is nothing left to action there. There is no code path, anywhere,
  that can move such a requirement out of "Pending." This matches the
  reported symptom exactly (§C, §D).
- This is a genuine, confirmed regression: the coverage logic predates both
  translation-optional changes by 12 days (old wizard) and ~3 months (new
  wizard), and was never revisited when those changes shipped (§E). The
  codebase's own integration tests annotate the intermediate state as
  `// Confirmed but no validation run yet` (E, below) — i.e. the original
  author assumed this state was always transient. For English-only content
  it is not transient; it is a dead end.
- Separately: **publish is not gated by validation state at all** (§D). The
  "cannot publish → not mappable" chain described in the brief does not
  exist in this codebase today — publish only checks session/talk status,
  title, and that sections exist. I could not find the "recently-found
  publish-gate" referenced in the brief; it isn't in `PublishAsync`,
  `ContentCreationSessionService`, or `ToolboxTalksController`'s publish
  paths. If it exists, it's somewhere I didn't find, or the wording refers
  to a different (already-known, long-standing) gate — e.g. that
  unpublished content is invisible to employees and excluded from mapping's
  "live" filter — not to a validation-driven publish block.
- One more correction to the brief's framing: as shipped today, a user
  **cannot** actually author in French/another language and translate into
  English. Both the old and new wizard **hardcode `sourceLanguageCode` to
  `"en"`** at submission time (§A.2). The domain model has supported
  non-English source since day one, but no UI ever sets it. The change that
  actually happened is narrower than "English became optional as a base
  language" — it's specifically "selecting at least one **target** language
  stopped being mandatory."

---

## A. The base-language change

### A.1 — What changed, and when

There were **two** separate changes, one per wizard generation, not one:

1. **Old wizard** (`create-wizard`), commit `e82331f` (2026-03-31,
   "feat: auto-populate target languages from employee preferences and make
   translation optional in content creation wizard"):
   - Removed the `targetLanguageCodes.length > 0` requirement from
     `canContinue` in `CreateWizard.tsx`, so admins with English-only
     workforces can proceed with zero target languages.
   - Added auto-population of `targetLanguageCodes` from non-English
     employee `PreferredLanguage` values (`InputConfigStep.tsx`), only when
     the field is currently empty.
   - Made Step 5 (Translate & Validate) skip entirely when no target
     languages are selected (`visibleSteps` filter,
     `goToNextStep`/`goToPreviousStep` jumps 4→6/6→4).

2. **New wizard** (`learning-wizard`), tracked as BACKLOG §5.13/§5.19,
   closed 2026-06-14 (`docs/phase-5/reports/wizard-skip-regression-fix.md`):
   - Removed the backend `InitialiseToolboxTalkCommandValidator` rule "At
     least one target language is required."
   - Fixed Step 5/6 reachability in
     `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`
     to gate on `targetLanguageCodes.length > 0` rather than
     `sections.length > 0`, so Translate/Validate become unreachable (not
     just skippable) when there are no target languages. BACKLOG.md:1106
     labels this explicitly: *"Sections exist, no target languages declared
     (English-only path) → `true`"* (i.e. Step 7/Publish is reachable
     without ever touching Steps 5/6).

**What did NOT change in either commit: the source language.** Both wizards
still only ever set `SourceLanguageCode = "en"`:

- New wizard: `InputConfigStep.tsx:371` — `sourceLanguageCode: 'en'` is a
  hardcoded literal in the `onSubmit` handler, not derived from any form
  field. A repo-wide search for a "Source language" selector/dropdown
  component found none in `create-wizard/steps/InputConfigStep.tsx` or
  `learning-wizard/steps/InputConfigStep.tsx` — every "Source language" hit
  in `web/src` (`ValidationRunDetailView.tsx`, `PreviewModal.tsx`,
  `ReviewScreen.tsx`, `create-wizard/steps/{ValidateStep,PublishStep}.tsx`,
  `learning-wizard/steps/PublishStep.tsx`) is a **display label**, not an
  input.
- Backend defaults also stay at `"en"`:
  `ToolboxTalk.cs:249` (`public string SourceLanguageCode { get; set; } = "en";`),
  `CreateToolboxTalkCommand.cs:37`, and
  `ContentCreationSessionService.cs:553` (draft talk hardcode).
- `ContentCreationSessionService.cs:676` hardcodes
  `SourceLanguage = "en"` on every `TranslationValidationRun` it creates —
  ignoring the talk's own `SourceLanguageCode` field entirely.

This is already tracked as BACKLOG §9 ("Hardcoded English assumptions in
translation pipeline," 13 sites, surfaced 2026-06-08) — see
`BACKLOG.md:1701-1748`. So: **the premise that a user can currently author
in French and translate into English is not accurate for the shipped
product.** The `SourceLanguageCode` field and `TargetLanguageCodes` schema
were built to support that (migration
`20260212123211_AddSourceLanguageToToolboxTalk`, 2026-02-16, present since
the module's initial extraction), but no UI path sets a non-English source
today. What genuinely changed is narrower: **target-language selection
became optional**, which is what makes "English only, nothing to translate"
a normal, common, and permanent end-state for a talk — not "English became
just another language including as a source," which isn't wired up yet.

### A.2 — How "which languages need translation" is computed today

Both `DailyTranslationScanJob` and `MissingTranslationsJob` compute the
required-language set purely from **tenant employee `PreferredLanguage`
values**, excluding English (scan job,
`DailyTranslationScanJob.cs:65-72`, hardcoded `!= "en"`) or excluding the
talk's own `SourceLanguageCode` (missing-translations job,
`MissingTranslationsJob.cs:99-106`, which since `SourceLanguageCode` is
always `"en"` in practice reduces to the same exclusion). Neither job reads
`TargetLanguageCodes` at all (confirmed by full-file read of both — see
`docs/translation-scan-behaviour-recon.md` §1–§4, independently verified
during this recon). English is treated as excluded-by-hardcoded-string
comparison in the background jobs, and as excluded-by-being-the-source in
the wizard's own target-language multiselect — either way, in the current
system English can never appear in `TargetLanguageCodes`, and nothing
"treats English symmetrically" as a real target. So the premise that
"English is now treated symmetrically with other languages, so it can be a
translation TARGET" is also not true of the shipped code today — there is
no live path in which English is a translation target.

---

## B. Why English (or rather, English-only content) enters "validation" with nothing to validate — it doesn't; it's the opposite

### B.1 — What decides whether TransVal runs, and for which languages

New-wizard path: `ContentCreationSessionService.StartTranslateValidateAsync`,
`ContentCreationSessionService.cs:632-713`. The critical loop:

```csharp
// ContentCreationSessionService.cs:664-688
foreach (var langCode in filteredCodes)          // filteredCodes = TargetLanguageCodes minus English
{
    var run = new TranslationValidationRun { ... };
    _dbContext.TranslationValidationRuns.Add(run);
    runIds.Add(run.Id);
}
...
foreach (var runId in runIds)
{
    var jobId = _backgroundJobClient.Enqueue<TranslationValidationJob>(
        job => job.ExecuteAsync(runId, tenantId, null, CancellationToken.None));
}
```

If `filteredCodes` (the talk's non-English target languages) is empty, this
loop is a no-op: **zero** `TranslationValidationRun` rows are created and
**zero** `TranslationValidationJob`s are enqueued. In the new wizard this
method is additionally never reachable at all for a no-target-language talk,
because Step 5/6 are unreachable per §A.1. In the old wizard, Step 5 is
skipped by the frontend navigation logic (`e82331f`), so this handler is
simply never called in that case either.

**Conclusion for B.1/B.4: English is never queued for translation
validation, and never was, by design — this part of the working theory is
not what's happening.** There is no review created "for English content
that has nothing to validate." The actual defect is the absence of any
record that lets *other* parts of the system know "this content is
correctly, permanently exempt from validation," rather than a wrongly
present validation task.

### B.2 — What IS created regardless of translation state: the regulatory mapping

`RequirementMappingJob.MapRequirementsAsync`
(`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs`,
full file read) is enqueued fire-and-forget from the publish path (per
`docs/phase-5/reports/5.5-publish-recon.md:188-195`, and
`ContentCreationSessionService.cs:1683-1684`/`1787-1788`/`2185-2186` for the
lesson/course publish paths). It:

- Loads the talk/course's `Title`, `Description`, and section content
  (`RequirementMappingJob.cs:120-172`) — **no read of
  `SourceLanguageCode` or `TargetLanguageCodes` anywhere in the file.**
- Sends that content plus the tenant's approved regulatory requirements to
  Claude Sonnet and persists whatever requirement IDs come back as new
  `RegulatoryRequirementMapping` rows with `MappingStatus = Suggested`
  (`RequirementMappingJob.cs:359-408`).

This job runs identically for a fully-translated multi-language talk and
for an English-only, zero-target-language talk. **This is the entry point
that puts English-only content into the compliance-mapping system at all** —
not TransVal, not a "review," just the AI mapping suggestion job, which is
completely indifferent to language configuration.

### B.3 — What entity/state represents "Pending Review," and what's supposed to clear it

There are actually **two unrelated things both surfaced under variants of
"Pending"**, which is part of the confusion:

1. `RegulatoryRequirementMapping.MappingStatus == Suggested` — an
   AI-suggested content↔requirement link awaiting a human accept/reject
   decision. This is a genuine, actionable review. Clearing it: `PUT
   .../requirement-mappings/{id}/confirm` or `.../reject`
   (`RequirementMappingService.ConfirmMappingAsync`/`RejectMappingAsync`,
   `RequirementMappingService.cs:71-107`), surfaced on
   `/admin/regulatory/mappings`.
2. `ComplianceRequirementDto.CoverageStatus == "Pending"` — a **computed,
   per-requirement** display status on the Compliance checklist
   (`RequirementMappingService.GetComplianceChecklistAsync`,
   `RequirementMappingService.cs:159-400`). This is not itself a stored
   entity or a "review" at all — it's a label the compliance page derives
   from mapping + validation-run state (see §C). It is this second one that
   renders as the "Pending Review" badge the brief describes
   (`compliance/page.tsx:76-82`).

The bug is that (2) is presented to the user identically to (1) — same
badge text "Pending Review," same "Review Mapping" link — but (2) can be
permanently un-resolvable in a way (1) is not.

---

## C. Why the review is un-actionable

### C.1 — Coverage computation (`RequirementMappingService.cs:320-336`)

```csharp
var hasConfirmedWithValidation = mappingDetails.Any(md =>
    md.MappingStatus == RequirementMappingStatus.Confirmed.ToString()
    && md.ValidationOutcome != null);
var hasSuggested = mappingDetails.Any(md =>
    md.MappingStatus == RequirementMappingStatus.Suggested.ToString());
var hasConfirmedWithoutValidation = mappingDetails.Any(md =>
    md.MappingStatus == RequirementMappingStatus.Confirmed.ToString()
    && md.ValidationOutcome == null);

string coverageStatus;
if (hasConfirmedWithValidation)
    coverageStatus = "Covered";
else if (hasSuggested || hasConfirmedWithoutValidation)
    coverageStatus = "Pending";
else
    coverageStatus = "Gap";
```

`md.ValidationOutcome` is populated a few lines earlier
(`RequirementMappingService.cs:246-272, 298-302`) by looking up the **most
recent completed `TranslationValidationRun` with `OverallOutcome` Pass or
Review, for that `ToolboxTalkId`/`CourseId` — with no `LanguageCode` filter
at all.** It doesn't matter which language; it matters only whether *any*
completed run exists for that content ID. For English-only content, per §B,
no such run can ever exist — not "hasn't yet," but structurally cannot,
under the current design.

So: `hasConfirmedWithValidation` can **never** become true for English-only
content, no matter what an admin does. If the AI-suggested mapping is
confirmed, the requirement moves from "Pending" (via `hasSuggested`) to
still "Pending" (via `hasConfirmedWithoutValidation`) — it never reaches
"Covered."

### C.2 — Why there's no action for it

`compliance/page.tsx:197-214` (the `Pending` branch of `RequirementRow`):

```tsx
{requirement.coverageStatus === 'Pending' && pendingMapping && (
  <div className="flex items-center gap-2 pt-1 text-sm">
    ...
    <Link href="/admin/regulatory/mappings" className="text-xs text-primary hover:underline">
      Review Mapping
    </Link>
  </div>
)}
```

Every "Pending" row gets the same "Review Mapping" link, regardless of
*why* it's pending. But the mappings page it links to
(`GetPendingMappingsAsync`, `RequirementMappingService.cs:36-69`) only
lists mappings where `MappingStatus == Suggested`
(`RequirementMappingService.cs:62-63`). Once a mapping is `Confirmed` —
whether via the individual Confirm button, the bulk "Confirm All"
(`ConfirmAllSuggestedAsync`), or a manually-created mapping (which is
created `Confirmed` from the start —
`AddManualMappingAsync`, `RequirementMappingService.cs:402+`, sets
`MappingStatus = RequirementMappingStatus.Confirmed` directly, see API
table entry "Create manual confirmed mapping (no AI)") — it disappears from
that list. There is nothing left on `/admin/regulatory/mappings` to act on.

**Answering the brief's Q7 directly: it's the second scenario, but not
quite either offered option.** It is not that a review was created that
shouldn't have been (no review is ever wrongly created for English-only
content — §B). And it is not that a legitimate review exists with a broken
action (the mapping-confirm action itself works fine). It's a **third
thing**: the *compliance coverage computation* has a precondition
(existence of a completed validation run) that is structurally impossible
to satisfy for a whole class of legitimately-published content, and the
resulting permanently-"Pending" display state is wired to an action (the
mappings-review link) that only ever applies to a *different, already
different from this,* transient sub-case (an unconfirmed AI suggestion).
Confirming the mapping — the only action offered — does not and cannot fix
the display state; it just removes the one genuinely actionable affordance
that was pointing at it.

---

## D. The chain / blast radius

### D.1 — Does pending review block publish?

**No.** `PublishAsync` (`ContentCreationSessionService.cs`, per
`docs/phase-5/reports/5.5-publish-recon.md:52-71`, independently confirmed
by reading the validation block referenced there) checks only: session
status is `Parsed`/`Validated`/`QuizGenerated`, `OutputType` is set,
parsed sections exist, and title is non-empty. It explicitly does **not**
check "whether target language translations have completed validation
runs" or "whether validation outcomes are Pass." A talk with zero target
languages (hence zero validation runs) publishes exactly the same as any
other talk. `RequirementMappingJob` fires *after* publish succeeds
(`ContentCreationSessionService.cs:1683-1684` etc.) — publish is not
gated by, and does not wait on, mapping or validation at all.

I could not locate the "recently-found publish-gate" referenced in the
brief anywhere in the publish code paths I read
(`ContentCreationSessionService.PublishAsync`,
`ToolboxTalksController` publish-adjacent actions,
`docs/phase-5/reports/5.5-publish-recon.md`). The only genuine "must be
Published" gate I found is the long-standing, unrelated one already
documented in `CLAUDE.md`'s Requirement Mapping / live-content note: a
mapping only counts toward compliance coverage if its target talk/course
`IsLive` (`Status == Published && IsActive && !IsDeleted` for talks —
`RequirementMappingService.cs:239`, `IsMappingTargetLive`). That's a
mapping/compliance-visibility gate, not a publish-blocking gate. If the
brief's "publish gate" refers to something else, I did not find it in this
pass — worth a follow-up with whoever surfaced it, pointing at a specific
file/PR if possible.

### D.2 — Does the stuck "Pending" keep content out of regulatory mapping?

**No — it's the reverse.** Regulatory mapping happens regardless of
translation/validation state (§B.2, §D.1). The stuck state is *downstream*
of mapping succeeding, not a blocker to it. So the brief's hypothesised
chain — "English-source → stuck pending review → cannot publish → not
mappable" — is **not real** as stated: publish isn't blocked (D.1), and
mapping isn't blocked either (it's mapping *creating* the stuck row, not
being blocked by one). The actual, confirmed chain is:

```
Published + zero target languages (legitimate, by design since §A)
  → RequirementMappingJob still runs, still maps (B.2, language-agnostic)
  → mapping confirmed (by any route) or already Suggested
  → GetComplianceChecklistAsync can never find a completed validation run
    for that content (C.1)
  → CoverageStatus stuck at "Pending" forever
  → "Review Mapping" link leads to a page with nothing to review once
    Confirmed (C.2)
```

### D.3 — Which learnings are affected; real client or test data?

From code alone (no DB access in this recon): **any tenant** that (a) has
at least one Regulation-kind sector assignment or Standard subscription
(i.e., participates in the Regulatory/Compliance module at all —
`GetComplianceChecklistAsync` throws `UnauthorizedAccessException` if not,
`RequirementMappingService.cs:181-182`), and (b) publishes at least one
talk/course with an empty `TargetLanguageCodes` array that Claude's
mapping pass links to an approved requirement. Given English-only content
is now the default-effort path (target languages are optional, and the old
wizard auto-populates target languages only from *actual* employee
non-English preferences — a mono-lingual English workforce tenant will
default to zero target languages, per `e82331f`'s own auto-populate logic
at `InputConfigStep.tsx` — see §A.1), this is likely the **common** case
for a workplace-safety tenant with an English-speaking workforce, not an
edge case.

I cannot confirm from code whether this is currently hitting real
CertifiedIQ/Production tenant data versus only Development/test data — that
requires a DB query (e.g. count of `RegulatoryRequirementMapping` rows
where `MappingStatus = Confirmed` and the target `ToolboxTalk` has no
matching completed `TranslationValidationRun`) which is out of scope for a
read-only code recon. Given the CLAUDE.md backlog references an active
"Training Evidence Pack" feature sector-annotated for HIQA/homecare (which
is exactly the seeded regulatory content — "Seeded with 15 HIQA homecare
requirements," per entity #35 in CLAUDE.md) and describes it as needing
sector expansion (implying real usage), this plausibly affects live client
data, but that inference should be verified against the actual database
before treating it as confirmed severity.

---

## E. Confirmed regression, with git evidence

The `GetComplianceChecklistAsync` coverage logic
(`hasConfirmedWithValidation`/`hasSuggested`/`hasConfirmedWithoutValidation`)
was introduced in commit `699c522` ("feat: compliance checklist with manual
mapping (Feature C)"), dated **2026-03-19**. Confirmed via
`git show 699c522 -- .../RequirementMappingService.cs`: this exact block
(including the three boolean names) appears for the first time in that
commit; the sibling commit `c8ee5cc` ("AI-assisted regulatory requirement
mapping (Feature B)," also 2026-03-19, same day) introduced the file itself
but without this coverage-classification logic — `git show c8ee5cc` for
that file has zero matches for `hasConfirmedWithValidation`,
`TranslationValidationRun`, or `coverageStatus`.

The two "translation optional" changes shipped **after** that:

- Old wizard: `e82331f`, **2026-03-31** — 12 days later.
- New wizard: BACKLOG §5.13/§5.19, **2026-06-14** — ~3 months later.

Neither the July 2026 follow-up commits that *did* touch this exact method
(`a25d5dd`, 2026-07-23, "compliance display surfaces subscribed Standards";
`be996fe`, 2026-07-24, "mapping surfaces filter for live talks and courses
only") changed the `hasConfirmedWithValidation` logic itself — confirmed by
reading both diffs in full; `a25d5dd`'s changes are entitlement/sector
plumbing above this block, and `be996fe`'s changes are the `IsMappingTargetLive`
filter added *before* this block (§C.1's `.Where(IsMappingTargetLive)`
line). The stuck-Pending defect was not the subject of either fix and
survived both untouched.

**Before 2026-03-31 (old wizard) / 2026-06-14 (new wizard), at least one
target language was mandatory to create a talk at all**, which meant a
`TranslationValidationRun` was always eventually created for any published,
mapped content — so `hasConfirmedWithoutValidation` was always a transient
window (between confirm and the validation job completing), never a
permanent trap. This is corroborated by the codebase's own integration test
comments: `tests/QuantumBuild.Tests.Integration/ToolboxTalks/RequirementMappingLiveFilterTests.cs:228`
and `:307` assert `CoverageStatus.Should().Be("Pending")` with the inline
comment `// Confirmed but no validation run yet` — "yet" reflecting the
original author's (correct, at the time) assumption that validation would
eventually land. No test in that file (or found elsewhere) covers a
zero-target-language talk being mapped, so the permanent-trap case has no
regression-test coverage today either.

**Conclusion: yes, this is a confirmed regression** — not in the sense
that working code was later broken by an edit to the same code, but in the
sense that a downstream consumer's invariant (`RequirementMappingService`,
March 19) was silently invalidated by an upstream product change (target
languages optional, March 31 / June 14) that nobody traced through to the
compliance-coverage calculation.

---

## F. Fix surface (describe only — not implemented here)

Three independent places a fix could land, in rough order of how directly
they address the root cause:

1. **`GetComplianceChecklistAsync` coverage classification**
   (`RequirementMappingService.cs:320-336`) — the most direct fix point.
   The condition needs a third case: a confirmed mapping to content that
   has **no target languages configured** (i.e., nothing will ever be
   validated) should not require `ValidationOutcome != null` to count as
   covered — it should be classified as covered (or some new, clearly-
   labelled status distinct from "Pending," e.g. "Covered — no translation
   required") based on `TargetLanguageCodes` being empty, not on the
   presence of a validation run. This requires reading `TargetLanguageCodes`
   (or an equivalent "translation not applicable" signal) alongside the
   existing mapping/validation-run data already loaded in this method.

2. **The "Pending" → "Review Mapping" link/label on the compliance page**
   (`compliance/page.tsx:76-82, 197-214`) — even without changing the
   backend classification, the two different "Pending" causes
   (`hasSuggested` vs `hasConfirmedWithoutValidation`) could be surfaced
   distinctly, so a genuinely-stuck row doesn't point users at a dead-end
   action. This is a symptom-level mitigation, not a root-cause fix, and
   should probably accompany (1) rather than replace it.

3. **`RequirementMappingJob` / mapping creation itself** — a much larger
   change, not recommended given (1) is more targeted: gating AI mapping
   creation on translation/validation completion would delay compliance
   visibility for content that will legitimately never need translation,
   which seems like the wrong direction (compliance coverage for
   English-only content becoming completed correctly should not depend on
   an irrelevant translation pipeline finishing).

Given the root cause is a stale invariant in a single method (§C.1, §E),
option (1) is the surface most worth prioritizing; (2) is a reasonable
companion UI clarification either way.

---

## Files read in full

- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Mapping/RequirementMappingService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs`
- `web/src/app/(authenticated)/admin/regulatory/compliance/page.tsx` (relevant sections)
- `docs/translation-scan-behaviour-recon.md`
- `docs/translation-flow-investigation.md`
- `docs/phase-5/reports/5.5-publish-recon.md`

## Files read in part / grepped for verification

- `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx`
- `web/src/features/toolbox-talks/components/create-wizard/steps/InputConfigStep.tsx`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs` (lines ~630-770)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalk.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandHandler.cs`
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/RequirementMappingLiveFilterTests.cs`
- `BACKLOG.md` (§9, §5.13, §5.14, §5.19)
- `CLAUDE.md` (entities #35-36, Requirement Mappings API table, Regulatory section)
- git history: `e82331f`, `c8ee5cc`, `699c522`, `a25d5dd`, `be996fe`,
  `20260212123211_AddSourceLanguageToToolboxTalk`

## Open questions I could not resolve without a running system / DB access

- Whether real (Production/CertifiedIQ) tenant data currently has
  requirements stuck in this state, versus only Development/test data
  (§D.3).
- What "the recently-found publish-gate" in the brief refers to — I could
  not locate a validation-driven publish block anywhere in the publish code
  paths I read (§D.1). Worth clarifying with whoever surfaced it.
