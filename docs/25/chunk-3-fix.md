# §25 Chunk 3 — SettingsStep Visual Polish: Implementation Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code_

---

## Summary of changes

One file modified, in scope:

| File | Change |
|------|--------|
| `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx` | Visual polish: five WizardSectionDividers, four helper texts, two preset button groups |

---

## Test results

**`npx tsc --noEmit`:** Passed — exit code 0, no output (zero errors)

**`npm run test`:**
```
Test Files  3 passed (3)
Tests       15 passed (15)
```

No new failures introduced. Pre-existing test count unchanged.

---

## Visual verification

Dev server running at `http://localhost:3000`. Verified Step 4 (Settings) in the new wizard with a fresh learning (no PDF attached, so Slideshow section hidden).

**Section markers:**
- `4a Details` — WizardSectionDivider with `firstSection` renders at the top of the step with `mt-2` (reduced top margin). Monospace "4a", uppercase "DETAILS", horizontal rule to edge. Correct.
- `4b Cover Image` — Divider renders between Details and Cover Image blocks. The existing description paragraph "Displayed on the employee training card. Optional." sits immediately below the divider (wrapped in a plain div alongside CoverImageUpload to prevent space-y-8 from expanding that gap). Correct.
- `4c Behaviour` — Divider renders before Refresher Frequency. Correct.
- `4d Auto-assign` — Divider renders before the Auto-assign toggle. Correct.
- `4e Slideshow` — Not rendered (no PDF on the test learning). Verified with a learning that has a PDF attached: divider renders and the "Generate slideshow from PDF" tile renders below it inside a React fragment. Correct.

**Helper text additions (all confirmed visible):**
- Category field: "Used to group learnings in reports and filters." — appears below the Select + FormMessage, `text-xs text-muted-foreground`.
- Refresher Frequency field: "Employees will be reminded to retake this learning at the selected interval." — appears below the Select + FormMessage.
- Minimum Watch % field: "Employees must watch at least this percentage of the video to proceed." — appears below the button group and FormMessage.
- Auto-assign Due Days field: "New employees will be assigned this learning with this many days to complete it." — appears below the button group and FormMessage, inside the conditional `{autoAssign && ...}` block. Correct.

**Minimum Watch % button group:**
- Six buttons: 50%, 60%, 70%, 80%, 90%, 100%
- Default active value (90%) shows `variant="default"` (filled primary), all others `variant="outline"` (bordered). Correct.
- Clicking any button: active state shifts to clicked button immediately. `saveField` fires and the Saving… indicator briefly appears in the footer.
- "Currently: X%" hint: triggered by setting form value to a non-preset via RHF devtools (e.g. 75). Hint "Currently: 75%" appears between the button group and the helper text. Not shown when value matches a preset.

**Auto-assign Due Days button group:**
- Visible only when the "Auto-assign to new employees" toggle is on.
- Five buttons: 7 days, 14 days, 30 days, 60 days, 90 days.
- Default active value (30 days) shows `variant="default"`. Correct.
- Clicking any button: active state and save fire correctly.
- "Currently: X days" hint: triggered when a non-preset value (e.g. 45) is set via RHF devtools. Hint "Currently: 45 days" appears correctly.

---

## Form schema verification

`settingsSchema.ts` confirms:
- `minimumWatchPercent`: `z.number().int().min(50).max(100)` — all six presets (50, 60, 70, 80, 90, 100) are valid integers within range. ✓
- `autoAssignDueDays`: `z.number().int().min(1).max(90)` — all five presets (7, 14, 30, 60, 90) are valid integers within range. ✓

No schema changes required.

---

## Match against legacy BehaviourPanel button group pattern

Legacy `BehaviourPanel.tsx` uses for Watch %:
- `flex flex-wrap gap-1.5` wrapper div
- Buttons: `h-8 min-w-[3.5rem] text-xs tabular-nums` + `pointer-events-none` on active
- `variant={settings.minimumWatchPercent === pct ? 'default' : 'outline'}`

Implementation matches:
- Same `flex flex-wrap gap-1.5` layout
- Same `h-8 min-w-[3.5rem] text-xs tabular-nums` classes (minus `pointer-events-none` — omitted to allow re-clicking the current selection which triggers an idempotent save; not a visual regression)
- Same `variant` toggle logic using `field.value === preset`

For Due Days: legacy uses a +/- stepper (not a preset group). The preset button group for Due Days is a new convention introduced in this chunk — not a port from legacy. The same styling pattern as Watch % was applied for consistency: `h-8 text-xs tabular-nums` (no `min-w` since "X days" text naturally sizes the button).

---

## Notable deviations from the recon spec

1. **Recon spec proposed keeping `<section aria-labelledby>` wrappers with `WizardSectionDivider aria-hidden`** — not implemented. Instead, the `<section>` wrappers and bare `<h2>` headings are removed, following the Chunk 1 / Chunk 2 convention (direct `WizardSectionDivider` rendering). Rationale documented in `docs/25/chunk-3-recon.md §2.3`.

2. **Recon spec proposed `max-w-[8rem]` Input with `%` suffix for Watch %** — not implemented. The prompt post-recon upgraded this to a preset button group (the same approach as the legacy wizard). The Input is fully replaced; no `%` suffix needed.

3. **Recon spec proposed fixing `autoAssignDueDays` as an Input width issue** — not implemented as an Input fix. The prompt post-recon upgraded Due Days to a preset button group matching the Watch % treatment. The Input is fully replaced.

4. **Cover Image `<p>` wrapped in a div** — The description paragraph and `CoverImageUpload` are wrapped in a plain `<div>` to prevent the form's `space-y-8` from adding 2rem between them after the `<section>` wrapper was removed. Visually equivalent to the original, structurally cleaner.

5. **"Currently: X%" hint logic** — Uses `WATCH_PRESETS.includes(field.value)` with `WATCH_PRESETS` declared as a plain `number[]` (not `as const`), avoiding the TypeScript constraint that `Array.includes()` on a `readonly [50, 60, ...]` tuple only accepts the literal union as its argument. Same for `DUE_DAYS_PRESETS`.

---

## BACKLOG impact

- **§25 status:** Open — Chunk 3 of 5 complete. Chunk 4 (StepIndicator subtitles + Steps 5–7 verify) and Chunk 5 (§24 detail panels) remain.
- **§5.7 status:** Still blocked — unblocks when Chunk 4 ships.
- **No new BACKLOG entries** — the Watch % and Due Days button group upgrades landed within this chunk's scope. No items deferred.

---

## Files changed in scope

- `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`

## Files changed outside stated scope

None.

## Build output

`npx tsc --noEmit`: pass (exit 0, no output)  
`npm run test`: 3/3 test files, 15/15 tests, no new failures
