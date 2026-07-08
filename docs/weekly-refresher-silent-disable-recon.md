# Recon: Weekly frequency silent-disable via RefresherFrequencyMapper

Date: 2026-07-08
Scope: read-only recon, no code changes. Verified against code at HEAD on `transval`.

## Headline

**Bounded to pre-existing Weekly data — no live path can create a new Weekly-frequency talk — but that pre-existing data is reachable through a currently-active edit route.** `ToolboxTalkForm.tsx` (the main "Edit Learning" form, still the sole edit UI for every talk regardless of which wizard created it) omits `requiresRefresher`/`refresherIntervalMonths` from its save payload on every submit, which routes straight into `RefresherFrequencyMapper.ToCanonicalFields`'s Weekly branch whenever the talk's `Frequency` is `Weekly` — silently writing `RequiresRefresher = false`, no error, no warning, on **every** save through that form, not just an accidental one. Whether any talk currently in Development/Demo actually carries `Frequency = Weekly` (and whether any such talk also currently has `RequiresRefresher = true`) could not be confirmed — no DB client was available in this session (see §5). The defensive-fix decision hinges heavily on that unknown.

## 1. Mapper behaviour (quoted, `RefresherFrequencyMapper.cs`)

```csharp
/// <summary>
/// Convert the legacy Frequency enum to canonical refresher fields.
/// Weekly has no months equivalent and was never functional for refresher scheduling
/// (RefresherSchedulingService uses integer months). Maps to no-refresher, preserving
/// the existing interval so a subsequent new-wizard Step 4 edit can restore it cleanly.
/// </summary>
public static (bool RequiresRefresher, int RefresherIntervalMonths) ToCanonicalFields(
    ToolboxTalkFrequency frequency,
    int existingIntervalMonths = 12)
{
    return frequency switch
    {
        ToolboxTalkFrequency.Monthly => (true, 1),
        ToolboxTalkFrequency.Annually => (true, 12),
        // Weekly: no months equivalent; preserve existing interval so Step 4 values survive
        ToolboxTalkFrequency.Weekly => (false, existingIntervalMonths),
        _ => (false, existingIntervalMonths), // Once
    };
}
```
[RefresherFrequencyMapper.cs:43-55](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Helpers/RefresherFrequencyMapper.cs#L43)

Confirmed exactly as the prior recon described: `Weekly` and `Once` both map to `(RequiresRefresher: false, RefresherIntervalMonths: existingIntervalMonths)`. The interval itself is preserved (not reset to a default) — only `RequiresRefresher` is what silently flips to `false`.

## 2. Callers of `ToCanonicalFields` — exhaustive

Grepped for `ToCanonicalFields(` across `src/`. **Exactly one call site exists in the whole codebase:**

| Caller | File:line | Reachable from a real user path? |
|---|---|---|
| `UpdateToolboxTalkCommandHandler.Handle` | [UpdateToolboxTalkCommandHandler.cs:148-149](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs#L148) | **Yes — the only live edit route for any talk** (see §3). |

This is a narrower finding than the prior recon implied by saying "enumerate all others" — there are no others. `ToLegacyFrequency` (the reverse direction, canonical → legacy) has many call sites ([InitialiseToolboxTalkCommandHandler.cs:74](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandHandler.cs#L74), [UpdateToolboxTalkSettingsCommandHandler.cs:86,116](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSettings/UpdateToolboxTalkSettingsCommandHandler.cs#L86), [ContentCreationSessionService.cs:1457,1733](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1457)) but none of them can ever produce `Weekly` as an output — its switch only covers `Once`/`Monthly`/`Annually` (line 23-35), so it is architecturally impossible for any canonical-to-legacy write to reintroduce `Weekly`. Only `ToCanonicalFields` (legacy → canonical) has a Weekly branch, and it has one caller.

### Important nuance not visible from the mapper file alone: a guard already exists upstream of it

`UpdateToolboxTalkCommandHandler.cs:141-150` (added 2026-06-18, per `docs/24/backend-fix-allowretry-refresher.md`, two days after the mapper itself was introduced):

```csharp
// Honor explicit refresher fields from new-wizard and detail-page payloads;
// fall back to the Frequency mapper only for legacy edit-form submissions that
// omit these fields (they arrive as the DTO defaults: false and 12).
if (request.RequiresRefresher || request.RefresherIntervalMonths != 12)
{
    toolboxTalk.RequiresRefresher = request.RequiresRefresher;
    toolboxTalk.RefresherIntervalMonths = request.RefresherIntervalMonths;
}
else
{
    (toolboxTalk.RequiresRefresher, toolboxTalk.RefresherIntervalMonths) =
        RefresherFrequencyMapper.ToCanonicalFields(request.Frequency, toolboxTalk.RefresherIntervalMonths);
}
```

This means `ToCanonicalFields` (and its Weekly branch) is reached **only** when the incoming request's `RequiresRefresher` is `false` **and** `RefresherIntervalMonths` is exactly `12` — i.e., only when the caller omitted both fields and they arrived as command-DTO defaults. This guard was added to stop a *different* bug (panels silently corrupting `AllowRetry`/refresher settings when re-saving from the detail page) — it does **not** close the Weekly path, because the one client that omits these fields on every request is exactly the client this recon is about (§3).

## 3. Is the legacy edit form still a live path, and does it actually round-trip through the mapper on save?

**Yes, confirmed directly — not inferred.** This directly answers the "be skeptical" instruction: the form does not pass canonical fields to the backend; it only sends `frequency`, and the mapper genuinely runs at save time, not just at load time.

- `ToolboxTalkForm.tsx`'s save payload ([ToolboxTalkForm.tsx:276-299](../web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L276)) includes `frequency: values.frequency` but **never** includes `requiresRefresher` or `refresherIntervalMonths` — those two keys are absent from the object entirely, for both create and edit submits.
- Because they're absent from the JSON body, ASP.NET model binding falls back to the `UpdateToolboxTalkCommand` DTO's declared defaults: `RequiresRefresher = false`, `RefresherIntervalMonths = 12` (confirmed in `docs/24/backend-fix-allowretry-refresher.md` §2, itself sourced from reading the command DTO).
- Feeding `(false, 12)` into the §2 guard's condition `request.RequiresRefresher || request.RefresherIntervalMonths != 12` evaluates to `false || false` → **always false** for this form → **always** takes the `else` branch → **always** calls `ToCanonicalFields(request.Frequency, ...)` on every single save through this form, for every talk, regardless of whether the talk's `Frequency` changed in that edit or not.
- `ToolboxTalkForm.tsx` is rendered from **one single edit route that applies to every talk regardless of which wizard created it**: [`web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx:86`](../web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx#L86) passes the loaded `talk` straight into `<ToolboxTalkForm talk={talk} .../>` with no wizard-version branching. There is no separate "new wizard" edit page distinct from this — Note 29's cutover concerns talk *creation* only (`/admin/toolbox-talks/create` vs `/admin/toolbox-talks/learnings/**`); post-publish editing of full talk properties (not the narrower `SettingsEditPanel`/`SectionEditPanel`/`QuizEditPanel` on the detail page) goes through this one form for both wizard lineages.
- A duplicate employee-facing edit route exists at `web/src/app/(authenticated)/toolbox-talks/talks/[id]/edit/page.tsx` (found via the same grep) — not read in full, but it's the same `ToolboxTalkForm` component and the same conclusion applies if reachable.

So: for any talk currently sitting at `Frequency = Weekly` in the database, **every** save through the main Edit Learning form — even one where the admin changes nothing but the description — silently sets `RequiresRefresher = false` (while leaving `RefresherIntervalMonths` untouched, since it's preserved). This is a live, ongoing, repeatable behavior of the current codebase, not a dormant one-time landmine in old data alone.

## 4. UI paths currently exposing Weekly

Grepped `Weekly` across all of `web/src` — five matches total, no others:

| File:line | Role |
|---|---|
| [`web/src/types/toolbox-talks.ts:5`](../web/src/types/toolbox-talks.ts#L5) | Type union includes `'Weekly'` — display/typing only. |
| [`web/src/lib/constants/frequency.ts:3,7,14`](../web/src/lib/constants/frequency.ts#L3) | `FREQUENCY_VALUES`/`FREQUENCY_OPTIONS` constants still list `Weekly` (kept deliberately per the §5.24 fix report, for display lookups on existing data). |
| [`ToolboxTalkForm.tsx:441-443`](../web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L441) | The only selectable dropdown — filters out `Weekly` **unless** `talk?.frequency === 'Weekly'` already. |

Confirmed: this is the only surface. On **create** (`talk` is `undefined`), the filter condition `talk?.frequency === 'Weekly'` is always false, so `Weekly` is unconditionally excluded from the dropdown for new talks — a fresh talk cannot be given `Frequency = Weekly` through this UI. On **edit** of an already-Weekly talk, the option reappears (so the admin isn't blocked from re-saving it as-is), which is precisely the path that re-triggers the mapper's Weekly branch on every save (§3).

No other component, wizard step, or panel offers Weekly as an option anywhere in `web/src` — the new wizard's `RefresherFrequency` string enum (`Once/Monthly/Quarterly/Annually`) and its backend counterpart never include Weekly at all (confirmed via `FromWizardFrequencyString`, §1 of prior recon, unchanged).

## 5. Does any DB row currently carry `Frequency = Weekly`?

**Not confirmed — no DB client was reachable in this session.** No `psql`, `pg_isready`, or similar client was found on `PATH`, and the app's own `dotnet ef`/Postgres tooling wasn't available as a direct ad-hoc query mechanism within this recon's scope. This check needs to happen in the fix chunk, ideally with two questions answered, not just one:

1. Does any row have `Frequency = Weekly` (enum value `2`)?
2. Of those, does any row also have `RequiresRefresher = true`? (If none do, the practical impact of the silent-disable is currently zero even though the code path is live — see the historical analysis below.)

### Why question 2 matters and what the migration history suggests (not a substitute for the live check)

`RequiresRefresher`/`RefresherIntervalMonths` were added by migration `20260211093655_AddRefresherSystem` with `defaultValue: false` / `12` and **no backfill SQL** ([AddRefresherSystem.cs:14-28](../src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260211093655_AddRefresherSystem.cs#L14)) — every pre-existing row, including any already carrying `Frequency = Weekly` from before this date, started at `RequiresRefresher = false` regardless of its `Frequency` value. Every write path that can flip `RequiresRefresher` to `true` also mirrors `Frequency` to `Monthly` or `Annually` in the same operation (via `ToLegacyFrequency`, which has no Weekly output) — **except** the pre-`§5.24-fix` (before 2026-06-16) version of `UpdateToolboxTalkSettingsCommandHandler`, which per the fix report's "Before" block set `RequiresRefresher`/`RefresherIntervalMonths` directly from the wizard's `RefresherFrequency` **without** touching `Frequency` at all. So a `Frequency = Weekly` + `RequiresRefresher = true` combination could only exist today if: a talk had `Frequency = Weekly` set through the legacy form before 2026-02-11, and was later (between 2026-02-11 and 2026-06-16) given `RequiresRefresher = true` through the new wizard's pre-fix Settings step without ever re-touching `Frequency`. This is a narrow, specific historical window — plausible but not something code-reading alone can confirm or rule out. A live query is the only way to know if the bug has any actual victims today.

## 6. Fix options

| Option | Admin experience | What survives (data / config / audit) | Effort shape |
|---|---|---|---|
| **A — Log a structured warning** | No change to save behavior; admin sees no indication anything happened. `RequiresRefresher` still silently flips to `false`. | Data: refresher config still lost. Audit: a log line exists (structured, e.g. `ILogger` warning with `TalkId`, tenant) for whoever monitors logs — not visible to the admin. | Smallest: one `_logger.LogWarning(...)` call in the `else` branch of §2's guard, or inside the mapper itself (mapper is `static` with no logger — would need a logger param or move the check to the caller). No test changes beyond asserting the log fires. |
| **B — Throw an exception** | Save is rejected outright. Admin sees an error and cannot save *any* change to that talk (even an unrelated field like description) until they pick a different frequency. | Data: nothing lost — refresher config untouched because the save never commits. Audit: the rejection itself is the record (implicit); no explicit log needed but one is easy to add. | Small-to-moderate: guard clause before the `else` branch in `UpdateToolboxTalkCommandHandler`, throwing (likely) `InvalidOperationException` consistent with the handler's existing validation style (lines 90, 104, 71, 77). Needs a new test asserting the throw, and needs the frontend to surface the resulting 400/500 sensibly — `ToolboxTalkForm.tsx`'s existing catch block (line 316+) already renders `error.response`/`message`, so no new frontend plumbing, but the message text needs to be written clearly ("This learning uses the legacy Weekly frequency setting, which cannot be saved with the current refresher system. Select a different frequency before saving."). Blocks legitimate unrelated edits (e.g. fixing a typo in the description) until the admin also resolves the frequency — a real UX cost for a field they may not have been trying to touch. |
| **C — Change the mapping to Monthly (or another concrete interval)** | Silent, same as today — admin still sees nothing, but `RequiresRefresher` becomes `true` instead of `false`. Arbitrary "1 month" (or whichever bucket chosen) may not match what the admin actually wants; could just as easily be *wrong* in the opposite direction (a talk that shouldn't refresh monthly starts doing so). | Data: a refresher cadence "survives" but is fabricated, not preserved — this isn't recovering lost intent, it's substituting a guess for it. Audit: none inherently. | Trivial in the mapper itself (`ToolboxTalkFrequency.Weekly => (true, 1)`), **but breaks an existing, explicit test**: `RefresherFrequencyMapperTests.ToCanonicalFields_Weekly_ReturnsFalseAndPreservesInterval` ([RefresherFrequencyMapperTests.cs:90-97](../tests/QuantumBuild.Tests.Unit/ToolboxTalks/RefresherFrequencyMapperTests.cs#L90)) asserts today's `(false, existingInterval)` behavior verbatim. Changing the mapping requires deliberately rewriting that test to assert the new value — the test isn't "arguably wrong," it's a faithful lock on the currently-documented, intentional behavior; changing it is a behavior change to sign off on, not a bug fix to a broken assertion. |
| **D — Guard at the caller: reject/skip the mapper call if `Frequency == Weekly` and `RequiresRefresher` was previously `true`** | Conditional: if the talk's *prior* state had `RequiresRefresher = true`, the save is rejected (or the refresher fields are left untouched rather than overwritten) — but only when frequency drift would actually cause data loss. If the talk was already `RequiresRefresher = false` (the common case per §5's historical analysis), the save proceeds exactly as today with no visible change at all. | Data: fully preserves refresher config in the one scenario where the bug can visibly destroy something; leaves harmless no-op cases untouched. Audit: easy to add a log line alongside the guard for the rejected/skipped case. | Moderate: needs to read `toolboxTalk.RequiresRefresher` **before** the mapper runs (already available — `toolboxTalk` is loaded at line 67, and the assignment happens at line 141+, so the "before" value is trivially available without an extra query) and branch on it specifically for the `Weekly` case. More surgical than B (doesn't block edits to *harmless* Weekly talks) but requires a new decision point and corresponding test coverage for: Weekly+RequiresRefresher=false (no-op, proceeds), Weekly+RequiresRefresher=true (guarded), non-Weekly (unaffected). |
| **E — Combination (B or D) + A** | Combine a structural guard (reject/skip only where destructive) with a log line for observability regardless of outcome. | Best of both: real protection against data loss, plus an audit trail even for the harmless no-op case so the tail-of-old-data question in §5 becomes self-answering over time (every future encounter gets logged). | Moderate: sum of whichever structural option is chosen (B or D) plus a one-line logger call — no meaningfully higher cost than the structural option alone. |

## Recommended shape (not a fix prompt)

Given §5's finding that this is very likely a zero-current-victims situation (no write path other than a narrow, closed historical window could have produced `Weekly` + `RequiresRefresher = true` together) but §3's finding that the trigger path is *live and repeatable* rather than a one-time legacy artifact, a pure logging fix (A) under-protects against the case where the DB check in the fix chunk turns up a live counter-example, while an unconditional throw (B) is disproportionate — it would block routine edits (e.g., a description typo fix) on any Weekly talk even when `RequiresRefresher` is already `false` and nothing would actually be lost. Option D (guard specifically on "would this save actually erase a `true` value") is the best fit: it's a no-op for the common case, it only intervenes when the save would be destructive, and it's cheap to implement given `toolboxTalk`'s prior state is already loaded in scope before the mapper runs. Pairing it with a log line (E) costs almost nothing extra and closes the audit-trail gap the prior recon flagged. The DB check from §5 should still happen first in the fix chunk — if it turns up zero Weekly rows in Development/Demo entirely, the fix may be lower priority than the guard's implementation cost suggests, though the guard is cheap enough that building it regardless is reasonable insurance against Weekly rows appearing in Production (not checked here, per the "do not read production data" boundary of this recon).
