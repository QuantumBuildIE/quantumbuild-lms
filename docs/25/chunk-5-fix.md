# §25 Chunk 5 — Detail Panels Visual Polish: Implementation Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code_

---

## Summary of Changes

Three files modified. No other files touched.

### 1. `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`

**Gap A — CardDescription added**

- Added `CardDescription` to the `@/components/ui/card` import (was missing; `SectionEditPanel` and `QuizEditPanel` both already import it).
- Added `<CardDescription>Quiz, refresher, and certificate settings</CardDescription>` immediately after the closing `</CardTitle>` tag in the `<CardHeader>` block.

**Gap B — Edit-mode section headers unified**

Four `<h3>` elements in edit mode replaced:

| Section | Before | After |
|---------|--------|-------|
| Quiz | `text-sm font-semibold mb-3` | `text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3` |
| Refresher | `text-sm font-semibold mb-3` | `text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3` |
| Certificate | `text-sm font-semibold mb-3` | `text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3` |
| Schedule | `text-sm font-semibold mb-3` | `text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3` |

Edit-mode headers now match view-mode headers exactly. Both render identically whether the user is viewing or editing settings.

### 2. `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx`

**Talk Details card — field rows converted to ViewRow pattern**

Each bare `<label className="text-sm font-medium text-muted-foreground">` + `<p className="mt-1">` pair converted to the `ViewRow` visual shape from `SettingsEditPanel`:
- Labels: `<p className="text-xs text-muted-foreground">`
- Simple values: `<p className="text-sm font-medium mt-0.5">`
- Complex values (Video, Attachment): label class changed; containing `<div>` updated from `mt-1` to `mt-0.5`; `text-sm font-medium` added to the value container for typographic consistency

Fields affected: Frequency, Sections, Video, Minimum Watch %, Quiz Required, Passing Score (conditional), Questions (conditional), Attachment (conditional).

### 3. `BACKLOG.md`

New entry **§7.8** added before the `---` separator that precedes `# 6. Security Notes`. Full text below.

---

## Test Results

### `npx tsc --noEmit`

No output — clean pass. Zero type errors.

### `npm run test`

```
 Test Files  3 passed (3)
      Tests  15 passed (15)
   Start at  10:55:36
   Duration  11.75s
```

15 of 15 passing. No new failures introduced.

---

## Visual Verification

Verified by code inspection (consistent with the Chunk 4 report's approach for structural render additions):

**SettingsEditPanel — CardDescription added:**
- `<CardHeader>` now contains `<CardTitle>` + `<CardDescription>` at the same level as `SectionEditPanel` and `QuizEditPanel`.
- Text "Quiz, refresher, and certificate settings" appears below the Settings title in the card header in both view and edit modes.

**SettingsEditPanel — edit-mode section headers:**
- All four `<h3>` tags (Quiz, Refresher, Certificate, Schedule) in the edit-mode form now use `text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3`.
- This is identical to the view-mode headers — the same sections render visually identically whether the user is reading or editing settings.

**ToolboxTalkDetail — Talk Details card:**
- Field labels now render at `text-xs text-muted-foreground` (smaller, muted) instead of `text-sm font-medium text-muted-foreground` (larger, medium weight).
- Simple field values now render at `text-sm font-medium mt-0.5` (same as `ViewRow`'s value style).
- Complex values (Video row with icon+link, Attachment row with download link) have labels converted and value containers updated for typographic consistency.
- The grid layout (`grid gap-4 sm:grid-cols-2`) is unchanged — only inner field row markup was restructured.

---

## ViewRow Handling — Inlined (not imported)

**Decision: inlined.**

`ViewRow` in `SettingsEditPanel.tsx` is declared as a plain unexported function (line 111: `function ViewRow...`). It has no `export` keyword. Exporting it would be scope drift into the `SettingsEditPanel` component API — `ViewRow` is an internal presentation helper for `SettingsEditPanel`'s own view mode, not a shared component.

Instead, the same visual shape is replicated inline in `ToolboxTalkDetail.tsx`. The pattern is two lines per field (label `<p>` + value `<p>`) with the same Tailwind classes, which is simple enough to inline without abstraction.

No new shared component created. If a third caller needed the same pattern, the appropriate move would be to extract `ViewRow` to `components/shared/` — not yet warranted.

---

## BACKLOG Entry Added

**Location:** Section 7 (Post-Phase-5 Cleanup), after §7.6, before the `---` separator and `# 6. Security Notes` heading. Numbered **§7.8**.

**Full text:**

```markdown
#### 7.8 SettingsEditPanel missing fields: video watch %, active status, auto-assign

- **Priority:** P3
- **Origin:** `[Engineering]` `[§25 Chunk 5 recon discovery 2026-06-19]`
- **Status:** Open
- **Surfaced:** During §25 Chunk 5 recon, while assessing the §24 detail panels for visual polish.
  `settingsEditSchema` in `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`
  does not include `minimumVideoWatchPercent`, `isActive`, or `autoAssignToNewEmployees` — these
  fields exist on the underlying talk model and are settable via the wizard but cannot be edited
  from the talk detail page once a talk is created. Users who want to change watch %, active
  status, or auto-assignment after publish must either use the wizard's re-edit flow or modify
  the data directly.

**Fix direction:**
- Add `minimumVideoWatchPercent`, `isActive`, and `autoAssignToNewEmployees` to `settingsEditSchema`
- Add form fields in edit mode: Switch + bordered tile via `ToggleRow` (booleans), `w-24` number
  input with label-unit suffix (watch %)
- Add corresponding `ViewRow` entries to view-mode rendering
- Wire into `onSubmit` (currently passed through from `talk.*` unchanged)

Estimated 0.5 day.
```

---

## Notable Deviations from Recon Spec

**Video row — `text-sm font-medium` added to value container.**

The recon spec described changing the label and applying `text-sm font-medium mt-0.5` to simple `<p>` value elements. For the Video row, the value is a `<div>` containing an icon + text + optional link. The `text-sm font-medium` class was added to that `<div>` so the video source text renders at the same weight as other field values. The "No video" fallback received `font-normal` to restore normal weight (the muted text should not be bolded). This is a minor addition not explicitly called out in the recon spec, but consistent with the target visual shape.

No other deviations. All class names, insertion points, and text content match the recon specification.

---

## `git diff --name-only` Confirmation

```
BACKLOG.md
web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx
web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx
```

Exactly three files. No scope drift.

---

## §25 Closure Summary

**Chunk 5 complete. §25 is closed.**

All five chunks shipped:

| Chunk | Files changed | Status |
|-------|--------------|--------|
| 1 | `wizard-section-divider.tsx`, `InputConfigStep.tsx` | ✅ Done 2026-06-18 |
| 2 | `ParseStep.tsx`, `QuizStep.tsx`, `QuizSettingsPanel.tsx` | ✅ Done 2026-06-18 |
| 3 | `SettingsStep.tsx` | ✅ Done 2026-06-18 |
| 4 | `StepIndicator.tsx`, `stepOrder.ts` | ✅ Done 2026-06-19 |
| 5 | `SettingsEditPanel.tsx`, `ToolboxTalkDetail.tsx`, `BACKLOG.md` | ✅ Done 2026-06-19 (this chunk) |

**Wizard cutover toggle is ready to flip for paying tenants.** The `UseNewWizard` TenantSettings key can be set to `"true"` for any production tenant as an operational decision — no remaining engineering work blocks it. Demo refresh (§5.7) was unblocked by Chunks 1–4; Chunk 5 completes the full polish pass including the talk detail page.

**§7.8 BACKLOG entry** records the SettingsEditPanel functional gap (missing fields) for future work — not blocking §25 closure.
