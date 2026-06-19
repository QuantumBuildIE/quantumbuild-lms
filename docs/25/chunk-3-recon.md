# §25 Chunk 3 Recon — SettingsStep Visual Polish

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code (read-only investigation — no code changed)_

---

## 1. Verification of Current State — No Drift

All four spec items from the design recon §7 Chunk 3 map cleanly to current code. No knock-on drift from Chunks 1 or 2 (they touched different files).

| Recon spec item | File:line | Maps cleanly? | Notes |
|---|---|---|---|
| Section markers — bare `<h2>` inside `<section aria-labelledby>` | SettingsStep.tsx:177, 265, 279, 401, 462 | ✓ | Five sections confirmed. No `WizardSectionDivider` imported or used. |
| Helper text missing on Refresher Frequency | SettingsStep.tsx:285–317 | ✓ | `<FormItem>` renders label + Select + `<FormMessage>` — no description text. |
| Helper text missing on Minimum Watch % | SettingsStep.tsx:320–344 | ✓ | `<FormItem>` renders label + Input + `<FormMessage>` — no description text. |
| Helper text missing on Category | SettingsStep.tsx:229–260 | ✓ | `<FormItem>` renders label + Select + `<FormMessage>` — no description text. |
| Minimum Watch % width is `w-24` (orphaned) | SettingsStep.tsx:338 | ✓ | `className="w-24"`. No `%` suffix. |

**Additional finding:** The `autoAssignDueDays` field (line 439–455, conditionally rendered when `autoAssign` is true) also carries `className="w-24"` with no unit label — a parallel issue to Minimum Watch %. Not explicitly in the recon spec; flagged in Out-of-Scope below.

### Five section structure confirmed

| Section heading text | `aria-labelledby` id | Lines |
|---|---|---|
| Details | `core-fields-heading` | 177–262 |
| Cover Image | `cover-image-heading` | 265–276 |
| Behaviour | `behaviour-heading` | 279–398 |
| Auto-assign | `auto-assign-heading` | 401–459 |
| Slideshow (conditional on `talk?.pdfUrl`) | `slideshow-heading` | 462–491 |

### WizardSectionDivider confirmed dark-mode patched

`web/src/components/ui/wizard-section-divider.tsx` reads `text-foreground` and `border-border` — the Chunk 1 patch is confirmed in place.

### Chunks 1 and 2 convention confirmed

`InputConfigStep.tsx:25` imports `WizardSectionDivider`. Five callsites at lines 461, 674, 735, 822, 826 — all rendered directly between field groups with no `<section aria-labelledby>` wrappers. This is the established pattern Chunk 3 follows.

---

## 2. Spec Gap Resolutions

### 2.1 Helper text wording

The legacy wizard's sub-panels (`RefresherPanel.tsx`, `BehaviourPanel.tsx`, `CategoryPanel.tsx`) do not have helper text for these fields — the panels use only labels and controls. The recon's proposed wording is fresh copy, not ported from legacy. **Use the recon's proposed wording verbatim:**

| Field | Helper text |
|---|---|
| Refresher frequency | "Employees will be reminded to retake this learning at the selected interval." |
| Minimum video watch percentage | "Employees must watch at least this percentage of the video to proceed." |
| Category | "Used to group learnings in reports and filters." |
| Auto-assign due days | "New employees will be assigned this learning with this many days to complete it." |

**Placement:** `<p className="text-xs text-muted-foreground mt-1">` as a sibling to `<FormMessage>` inside each `<FormItem>`. Auto-assign due days helper text is inside the conditional block alongside the field.

### 2.2 Section divider numbering — Recommend 4a through 4e

**Decision: 4a through 4e.**

Rationale: Chunks 1 and 2 use `1a–1e` and `2a` respectively. Treating SettingsStep as Step 4 with letter suffixes for sub-sections is consistent. The legacy wizard's SettingsStep also uses `4a–4d` for its four sections. Five dividers: `4a Details`, `4b Cover Image`, `4c Behaviour`, `4d Auto-assign`, `4e Slideshow`. The `4e Slideshow` divider sits inside the `{talk?.pdfUrl && (...)}` conditional block — correct, since the section only renders with a PDF.

### 2.3 `<section aria-labelledby>` resolution — Recommend Option B (remove wrappers)

**Decision: Remove the `<section aria-labelledby>` wrappers and bare `<h2>` headings. Render `WizardSectionDivider` directly between content blocks.**

Rationale:
- Matches the pattern Chunk 1 established (InputConfigStep deviation note 2 in chunk-1-fix.md explicitly chose this approach over the `aria-hidden` variant).
- No accessibility regression: the section element IDs (`core-fields-heading`, etc.) are only referenced by `aria-labelledby` on their own containing `<section>` — no external JS, no focus management hooks, no other callers found. The outer `<form aria-label="Learning settings">` provides screen-reader context for the step.
- `WizardSectionDivider` renders visible `<span>` text that is readable by screen readers even without heading semantics.
- Option A (keep `<section>` + make `<h2>` `sr-only`) would create asymmetry: SettingsStep would be the only wizard step with implicit heading navigation. Not worth it for a polish chunk.

### 2.4 Minimum Watch % — exact width and suffix

**Width:** Use `max-w-[8rem]` (recon proposal). The legacy wizard's BehaviourPanel uses a **button group** for this field (six preset buttons: 50%, 60%, 70%, 80%, 90%, 100%) — not an Input at all. There is no legacy Input with a direct width to port. `max-w-[8rem]` (128px) is the right choice for a numeric Input — wider than `w-24` (96px), less orphaned.

**Suffix:** Add `<span className="text-sm text-muted-foreground">%</span>` in a `<div className="flex items-center gap-2">` wrapper around the Input. The helper text goes below the wrapper inside the FormItem, after FormMessage.

### 2.5 CoverImageUpload scope confirmation

`CoverImageUpload.tsx` is already polished: drag-and-drop (`onDragOver`, `onDragLeave`, `onDrop`), confirmation dialog for removal, busy states, proper `role="button"` and keyboard accessibility. No internal visual debt.

The Cover Image section in SettingsStep already has a description paragraph at line 269–271:
```
<p className="text-sm text-muted-foreground mb-4">
  Displayed on the employee training card. Optional.
</p>
```
This description stays as-is after the `<h2>` heading is removed — it becomes a natural sub-label under the `4b Cover Image` divider. No changes inside `CoverImageUpload`.

---

## 3. Sized Implementation Chunk

**One file changed:** `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`

**Estimated diff size:** ~45 lines changed. Smallest §25 chunk confirmed.

### Change list

1. **Import** — add `WizardSectionDivider` to the existing import block (one line after the existing `LoadingState` import).

2. **Details section** — Remove `<section aria-labelledby="core-fields-heading">` and `<h2 id="core-fields-heading" className="text-base font-semibold mb-4">Details</h2>` and closing `</section>`. Replace with `<WizardSectionDivider number="4a" label="Details" firstSection />` before the `<div className="space-y-4">` content block. Add `<p className="text-xs text-muted-foreground mt-1">Used to group learnings in reports and filters.</p>` inside the Category `<FormItem>` after `<FormMessage>`.

3. **Cover Image section** — Remove `<section aria-labelledby="cover-image-heading">`, `<h2 id="cover-image-heading" className="text-base font-semibold mb-1">Cover Image</h2>`, and closing `</section>`. Replace heading with `<WizardSectionDivider number="4b" label="Cover Image" />`. The existing `<p className="text-sm text-muted-foreground mb-4">Displayed on the employee training card. Optional.</p>` and `<CoverImageUpload>` remain unchanged.

4. **Behaviour section** — Remove `<section aria-labelledby="behaviour-heading">`, `<h2 id="behaviour-heading" className="text-base font-semibold mb-4">Behaviour</h2>`, and closing `</section>`. Replace with `<WizardSectionDivider number="4c" label="Behaviour" />`. Add helper text on Refresher Frequency and Minimum Watch % fields. Change Minimum Watch % Input: wrap in `<div className="flex items-center gap-2">`, set `className="max-w-[8rem]"`, add `<span className="text-sm text-muted-foreground">%</span>`.

5. **Auto-assign section** — Remove `<section aria-labelledby="auto-assign-heading">`, `<h2 id="auto-assign-heading" className="text-base font-semibold mb-4">Auto-assign</h2>`, and closing `</section>`. Replace with `<WizardSectionDivider number="4d" label="Auto-assign" />`. Add helper text on the `autoAssignDueDays` `<FormItem>` (inside the conditional block) after `<FormMessage>`.

6. **Slideshow section** — Inside the `{talk?.pdfUrl && (...)}` conditional: remove `<section aria-labelledby="slideshow-heading">`, `<h2 id="slideshow-heading" className="text-base font-semibold mb-4">Slideshow</h2>`, and closing `</section>`. Replace with `<WizardSectionDivider number="4e" label="Slideshow" />`.

**No other files changed.** WizardSectionDivider is already patched (Chunk 1). No backend changes. No test file changes required — changes are purely visual structural.

---

## 4. Out-of-Scope Concerns

| Item | Reason | Suggested handling |
|---|---|---|
| `autoAssignDueDays` field also uses `w-24` with no unit label | Parallel issue to Minimum Watch %. Not explicitly in the Chunk 3 recon spec. Trivial ~3-line fix within the same file already being edited. | Recommend including in Chunk 3 under the same scope: wrap in `flex items-center gap-2`, change to `max-w-[8rem]`, add `<span className="text-sm text-muted-foreground">days</span>`. No separate BACKLOG entry needed — it's the same pattern as Minimum Watch %. |
| Legacy wizard uses button group for Minimum Watch % (Chunk 3 keeps it as Input) | Control-type divergence not in scope for Chunk 3. The recon only scoped width fix. | Flag for future backlog consideration if UX feedback confirms the preset-button approach is preferred. Not blocking. |
| No E2E or unit tests cover SettingsStep visual structure | Chunk 3's changes are visual only — no behavioral change, no API change. Existing 3/3 test files / 15/15 tests unaffected. | No action needed for Chunk 3. |
| Cover Image section already has a description paragraph at line 269–271 | This is fine — it's a sub-label that reads naturally under the WizardSectionDivider. No conflict with the chunk's approach. | Note for implementer: leave the paragraph in place; don't remove it when removing the `<h2>`. |

---

## 5. Files Read

| File | Purpose |
|---|---|
| `docs/25/recon.md` | §25 design recon — Chunk 3 spec (§7) |
| `docs/25/chunk-1-fix.md` | Chunk 1 implementation report — convention carry-forward |
| `docs/25/chunk-2-fix.md` | Chunk 2 implementation report — convention confirmed |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx` | Subject of Chunk 3 |
| `web/src/features/toolbox-talks/components/create-wizard/steps/SettingsStep.tsx` | Legacy wizard SettingsStep (design reference) |
| `web/src/features/toolbox-talks/components/create-wizard/steps/settings/RefresherPanel.tsx` | Legacy helper text reference — Refresher Frequency |
| `web/src/features/toolbox-talks/components/create-wizard/steps/settings/BehaviourPanel.tsx` | Legacy helper text reference — Min Watch %, Auto-assign |
| `web/src/features/toolbox-talks/components/create-wizard/steps/settings/CategoryPanel.tsx` | Legacy helper text reference — Category |
| `web/src/features/toolbox-talks/components/learning-wizard/components/CoverImageUpload.tsx` | Sub-component — confirm scope ("visual pass around it only") |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` | Polished Chunk 1 output — pattern reference (lines 25, 461, 674, 735, 822, 826) |
| `web/src/components/ui/wizard-section-divider.tsx` | Confirmed dark-mode patch in place (`text-foreground`, `border-border`) |
| `BACKLOG.md` (§25 entry, line 1936) | Status: Open — confirmed no amendment since 2026-06-18 |

---

## 6. Report Written

`docs/25/chunk-3-recon.md`
