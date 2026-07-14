# §25 Chunk 6 — Outer Step Container Shell: Implementation Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code_

---

## Summary of Changes

Single file modified. ~6 lines changed.

**`web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx`**

1. Added `Card` and `CardContent` to the import from `@/components/ui/card` (line 5).
2. Replaced `<div className="flex-1 min-h-0">{children}</div>` with `<Card><CardContent>{children}</CardContent></Card>` at lines 57-61.

**Deliberate divergence from legacy:** The legacy wizard uses `<CardContent className="pt-6">` (48px total top padding). Chunk 6 deliberately omits the `pt-6` override — the Card's own `py-6` provides 24px top padding only. This was the pre-agreed design decision from the chunk prompt; no stop-and-report condition was hit.

**`flex-1 min-h-0` removal:** The div-level flex-sizing classes were dropped. Safe: the new wizard is page-per-step (one route per step), so flex-fill height management on the content slot is not needed. No visual regression observed across all seven steps.

---

## Test Results

### `npx tsc --noEmit`

```
(no output — clean pass)
```

Exit code 0. Zero TypeScript errors.

### `npm run test`

```
 RUN  v4.1.9

 Test Files  3 passed (3)
      Tests  15 passed (15)
   Start at  11:31:45
   Duration  1.46s (transform 192ms, setup 473ms, import 301ms, tests 28ms, environment 2.67s)
```

15 of 15 passing. No new failures introduced.

---

## Visual Verification

Navigation was done through the new wizard running locally on `http://localhost:3000`.

### Step 1 — Input & Config (`/admin/toolbox-talks/learnings/new`)

- Outer Card shell renders: `bg-card`, `border`, `rounded-xl`, `shadow-sm` visible against the `bg-slate-50` page background.
- 24px top padding from Card's `py-6` — reads comfortably, not cramped.
- Audit Metadata inner Card sits visually inset within the shell padding — "card within card" is legible; outer border = step boundary, inner border = section grouping. Matches the pattern the legacy ships for this section.
- WizardSectionDividers (1a–1e) render on the `bg-card` shell surface correctly.

### Step 2 — Parse (`/admin/toolbox-talks/learnings/[id]/parse`)

- Shell renders.
- Section list `<Card>` (with CardHeader "Sections (N)" and Re-parse action) is visually nested inside the shell padding. Inner border reads as a contained data region, not a duplicate frame.
- Empty-state and ready-state both render correctly inside the shell.

### Step 3 — Quiz (`/admin/toolbox-talks/learnings/[id]/quiz`)

- Shell renders.
- Card-lite (`rounded-xl border shadow-sm bg-card overflow-hidden`) renders inside the shell — consistent with the legacy Card-in-Card pattern established by the recon.
- QuizSettingsPanel (`2a Quiz Settings` divider and switch tiles) renders on the shell surface below the Card-lite.

### Step 4 — Settings (`/admin/toolbox-talks/learnings/[id]/settings`)

- Shell renders.
- Switch tiles render directly on the shell's `bg-card` surface.
- WizardSectionDividers (4a–4e), Watch % button group, and Due Days button group all render on the shell surface.

### Step 5 — Translate (`/admin/toolbox-talks/learnings/[id]/translate`)

- Shell renders.
- Fragment top-level (`<>`) — WorkflowSubscribers are invisible (no DOM output); shell wraps the visible `<div className="space-y-6">` containing WizardTranslationPanel.
- Renders correctly inside shell.

### Step 6 — Validate (`/admin/toolbox-talks/learnings/[id]/validate`)

- Shell renders.
- Same fragment pattern as Translate; ValidationSectionCards render inside the shell.

### Step 7 — Publish (`/admin/toolbox-talks/learnings/[id]/publish`)

- Shell renders.
- All four inner Cards (ContentSummaryPanel, three ThreeColumnSummary cards, AuditMetadataPanel) render inside the shell with `space-y-6` gaps between them.

### Navigation bar (border-t Back/Continue strip)

Sits **outside** the Card shell at `mt-8 pt-4 border-t`. The top margin and border-top provide clear visual separation between the Card shell above and the Back/Continue bar below. Correct behaviour confirmed.

### `flex-1 min-h-0` removal sanity check

No content collapse or unexpected height behaviour observed on any of the seven steps. The page-per-step architecture means each step is a standalone route; flex-fill sizing was not needed.

---

## 24px Top Padding Assessment

The 24px top padding (Card's `py-6` only, no `CardContent pt-6` override) reads correctly across all seven steps. Content does not feel cramped. No stop-and-report condition triggered. The deliberate divergence from legacy's 48px is confirmed as the right call — 48px would have been excessive given the step content's own internal top spacing.

---

## Side-by-Side Comparison vs Legacy Wizard

The legacy wizard was opened at `/admin/toolbox-talks/create` alongside the new wizard at `/admin/toolbox-talks/learnings/new`. Observations:

- Both wizards now render step content inside a white Card shell against the `bg-slate-50` page background.
- The new wizard's shell registers as visually equivalent: same border, same rounded corners, same subtle shadow.
- The only visible difference is the top padding: legacy has ~48px, new wizard has ~24px. The 24px reads as tighter but not cramped — an improvement over the legacy in fact.
- Overall: visual parity achieved. The outer-shell gap that motivated this chunk is closed.

---

## Notable Deviations from Recon Spec

| Item | Recon spec | Chunk 6 decision | Rationale |
|------|-----------|-----------------|-----------|
| `CardContent className="pt-6"` | Recon §4 showed `pt-6` to "match legacy exactly" | Omitted — no `pt-6` | Chunk prompt explicitly directed this. Card's `py-6` provides 24px; adding `pt-6` doubles to 48px. Deliberate tighter padding. |

No other deviations.

---

## Files Changed in Scope

- `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx`
- `docs/25/chunk-6-fix.md` (this report)

## Files Changed Outside Stated Scope

None. `git diff --name-only` shows only `WizardLayout.tsx`.

---

## Build Output

`npx tsc --noEmit`: clean (no output, exit 0)  
`npm run test`: 3 test files, 15 tests — all passing  
No new warnings introduced by this chunk.

---

## §25 Closure Summary

| Chunk | Description | Status |
|-------|-------------|--------|
| Chunk 1 | InputConfigStep visual polish | Shipped |
| Chunk 2 | ParseStep + QuizStep visual polish | Shipped |
| Chunk 3 | SettingsStep visual polish | Shipped |
| Chunk 4 | StepIndicator subtitles | Shipped |
| Chunk 5 | Detail panels (§24 carryover) | Shipped |
| **Chunk 6** | **Outer step container shell** | **Complete** |

**§25 is closed.** The new wizard now has visual parity with the legacy wizard at both the within-step level (Chunks 1–5) and the outer container level (Chunk 6).

The wizard cutover toggle (`UseNewWizard` tenant setting, per Note 29) is ready to flip for paying tenants. Demo refresh can run against a wizard with full outer-shell and within-step polish.
