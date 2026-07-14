# §25 — New Wizard Visual Polish Recon

_Date: 2026-06-18_  
_Branch: transval_  
_Author: Claude Code (read-only investigation — no code changed)_

---

## 1. Design Target

The new wizard (`learning-wizard`) is functionally complete but visually unfinished. Side-by-side against the legacy wizard, four structural patterns are absent in most steps: **section markers** (the `WizardSectionDivider` horizontal-rule bands that group related fields within a step), **bordered tile rows** (the `rounded-lg border p-4` containers that pair a label+description on the left with a control on the right, used for every Switch-based toggle), **Card groupings** (shadcn `Card + CardContent` wrapping multi-field clusters like Audit Metadata), and **helper text** (the `text-xs text-muted-foreground` sub-labels that give each field context without consuming form real-estate). The legacy wizard also uses center-aligned icon tiles for the Content Source selector (h-6 icons, vertically stacked label + description), a `Switch` for boolean options (not raw `<input type="checkbox">`), a company dropdown for Client Name, and a preset/custom hybrid dropdown for Audit Purpose. The design target for §25 is to adopt all four structural patterns across every step of the new wizard, align specific control choices where the new and legacy diverge, and add step-indicator subtitles (short per-step descriptions not currently in the `StepItem` interface). One design tension is resolved here: `WizardSectionDivider` currently uses hard-coded `text-slate-700` / `border-slate-300` that won't theme correctly in dark mode. §25 chunks patch these to `text-foreground` / `border-border` before adopting the component in the new wizard; no structural change to the component.

---

## 2. Demo Cut

The minimum chunks to unblock §5.7 (Demo refresh closure) are **Chunks 1, 2, 3, and 4**, totalling an estimated 6.5–8 days. Chunk 1 polishes the InputConfigStep (Step 1 — the largest and most visually sparse step, and the first impression on every Demo walkthrough). Chunk 2 polishes the Parse and Quiz steps (Steps 2 and 3 — visited immediately after and currently the most structureless). Chunk 3 polishes the Settings step (Step 4 — already the closest to parity, requires a consistency pass). Chunk 4 adds StepIndicator subtitles (visible across all steps, called out in the BACKLOG entry) and does a verification pass on the Translate, Validate, and Publish steps (Steps 5–7 — these reuse production-quality components and are expected to be at parity already; the chunk documents the finding). The §24 detail panels (Chunk 5) are not part of the wizard walkthrough and can ship post-Demo without blocking §5.7.

---

## 3. Full §25 Plan

**Five chunks, estimated 7.5–9 days total.** Chunk 1: InputConfigStep (Step 1) polish — sub-section markers, Card grouping for Audit Metadata, bordered tile toggles, shadcn Switch for checkboxes, company dropdown, preset/custom Audit Purpose, drag-and-drop on dropzone, helper text, tile selector alignment (3 days). Chunk 2: ParseStep and QuizStep (Steps 2 and 3) polish — Card wrapping, section headers, stronger empty states, visual consistency (1.5 days). Chunk 3: SettingsStep (Step 4) polish — section marker consistency pass, helper text on remaining fields, minor spacing (1 day). Chunk 4: StepIndicator subtitles + Steps 5–7 verification pass (1 day). Chunk 5: §24 edit panels on the talk detail page — SectionEditPanel, QuizEditPanel, SettingsEditPanel, AddTargetLanguagePicker (1–1.5 days). Demo cut = Chunks 1–4; Chunk 5 post-Demo.

---

## 4. Design Target Detail

### 4.1 The four structural patterns

#### 4.1.1 Section markers

The legacy wizard uses `WizardSectionDivider` (in `components/ui/wizard-section-divider.tsx`) to divide a step into named sub-groups. A marker consists of a monospace numeric ID (e.g. `1a`), an uppercase tracking-widest label, and a horizontal rule to the right edge. InputConfigStep uses five: `1a Content Source`, `1b Translation Settings`, `1c Content Options`, `1d Sector`, `1e Audit Metadata`.

The new wizard uses bare `<h3 className="text-sm font-medium">` headings in InputConfigStep (no markers) and `<section aria-labelledby> + <h2>` in SettingsStep (correct accessibility, no visual marker). The target is to adopt section markers throughout while preserving the `<section aria-labelledby>` semantic structure.

**Dark mode fix required before adoption:** `WizardSectionDivider` uses `text-slate-700 dark:text-slate-300` for the label and `border-slate-300 dark:border-slate-600` for the rule — hardcoded slate, not theme tokens. Fix: replace with `text-foreground` and `border-border`. This is a one-line patch in `wizard-section-divider.tsx`; it has no runtime risk and improves the component for all callers including the legacy wizard.

#### 4.1.2 Bordered tile rows

The legacy wizard renders each Switch-based setting as a `div.rounded-lg.border.p-4` with label + description on the left and `<Switch>` on the right. Examples from InputConfigStep: Include Quiz, Preserve Source Wording. From SettingsStep: Generate Certificate, Active on Publish, Auto-assign, Generate Slideshow.

The new wizard uses this pattern correctly in SettingsStep but uses raw `<input type="checkbox">` (not the shadcn `Checkbox` component, not a `Switch`) in InputConfigStep for Include Quiz and Preserve Source Wording — without the bordered tile wrapper.

**Target:** every boolean control that affects wizard behavior uses either shadcn `Switch` (for settings that feel like on/off config) or shadcn `Checkbox` (for opt-in choices at creation time). All are wrapped in a bordered tile row. InputConfigStep's Include Quiz and Preserve Source Wording should become Switch + bordered tile (matching the legacy's pattern for those exact fields).

#### 4.1.3 Card groupings

The legacy wizard wraps the Audit Metadata grid in `<Card><CardContent>`. Other multi-field groups (Translation Settings, the Language+Threshold pair) use a `flex items-start gap-3 rounded-lg border p-4` container — a Card-lite with no header.

The new wizard has no Card wrapping in Steps 1–4. Step 7 (PublishStep) uses Cards extensively. The gap is Steps 1–4; Step 4 (SettingsStep) is the closest since it uses bordered tiles per row but not Card wrappers per section.

**Target:** Audit Metadata in InputConfigStep wraps in a Card. Multi-field clusters in ParseStep and QuizStep (the section list, the question list, QuizSettingsPanel) wrap in a Card. SettingsStep sections stay at the bordered-tile-per-row level — adding a Card per section there would add too much visual weight and the current approach is acceptable.

#### 4.1.4 Helper text

The legacy wizard provides `text-xs text-muted-foreground` sub-labels below or beside controls. Examples: "Content will be translated and validated for each selected language" under Target Languages; "Min score to pass" under Pass Threshold; "Select the sector this document belongs to" below the sector dropdown; character-count display under the source text textarea.

The new wizard omits most of these. They are low-effort additions (one `<p>` per field) that significantly reduce user uncertainty on a first visit.

**Target:** every non-obvious control in Steps 1–4 gets helper text. The exact copy can be ported from the legacy wizard where the same field exists, or written fresh for fields the new wizard adds.

---

### 4.2 Specific control divergences to resolve in Chunk 1

| Control | Legacy | New wizard | Target |
|---------|--------|------------|--------|
| Include Quiz | `Switch` + bordered tile + description | `<input type="checkbox">` + plain label | `Switch` + bordered tile |
| Preserve Source Wording | `Switch` + bordered tile | `<input type="checkbox">` + plain label | `Switch` + bordered tile |
| Video rights checkbox | `Checkbox` (shadcn) | `<input type="checkbox">` | `Checkbox` (shadcn) |
| Content source tiles | Centered icon (h-6), vertically stacked label + description | Left-aligned (items-start), small icon (h-4) | Center-aligned, h-6 icons |
| Target Languages + Pass Threshold | Paired in `flex items-start gap-3 border p-4` container | Separate stacked FormItems | Paired container |
| Client Name | `Select` populated from company list | `<Input>` free text | `Select` from company list |
| Audit Purpose | Preset `<Select>` with "Other…" → free `<Input>` fallback | `<Textarea>` free text | Preset/custom hybrid |
| File dropzone | Drag-and-drop (`onDragOver` + `onDrop`) + "or click to browse" | Click-to-select only | Add drag-and-drop |

---

### 4.3 Design tension resolved: WizardSectionDivider vs semantic headings

The legacy `WizardSectionDivider` renders `<span>` elements — not heading tags. The new wizard's SettingsStep correctly uses `<section aria-labelledby> + <h2>`. These are compatible:

- Keep `WizardSectionDivider` as the **visual** section marker (patched for dark mode)
- Wrap each section in `<section aria-labelledby="xxx">` with an `id` on the WizardSectionDivider's container or a hidden `<h2>` sibling
- OR: Mark `WizardSectionDivider` itself with `aria-hidden="true"` and rely on the `<section aria-labelledby>` structure for screen readers

Recommendation for §25 chunks: use `aria-hidden="true"` on `WizardSectionDivider` and keep `<section aria-labelledby>` wrapping the content block (matching what SettingsStep already does). This avoids changing the component's output shape while ensuring screen readers navigate section structure correctly.

---

## 5. Inventory Table

### New wizard steps — current visual state vs target

| Step | Route | Component | Section markers | Bordered tiles | Card groupings | Helper text | Other gaps |
|------|-------|-----------|----------------|----------------|----------------|-------------|------------|
| 1 — Input & Config | `/learnings/new` | `InputConfigStep.tsx` | ✗ (bare `<h3>`) | ✗ (raw checkboxes) | ✗ | Partial | Tile alignment, Client dropdown, Audit Purpose hybrid, drag-and-drop |
| 2 — Parse | `/learnings/[id]/parse` | `ParseStep.tsx` | ✗ | N/A | ✗ | Partial | Re-parse button in ready state |
| 3 — Quiz | `/learnings/[id]/quiz` | `QuizStep.tsx` | ✗ | N/A | ✗ | Partial | QuizSettingsPanel grouping |
| 4 — Settings | `/learnings/[id]/settings` | `SettingsStep.tsx` | ✗ (bare `<h2>`) | ✓ (Switch tiles) | ✗ | Partial | Section marker visual, remaining field help text |
| 5 — Translate | `/learnings/[id]/translate` | `TranslateStep.tsx` | ✓ (via WizardTranslationPanel) | ✓ | ✓ | ✓ | Likely at parity — verify only |
| 6 — Validate | `/learnings/[id]/validate` | `ValidateStep.tsx` | ✓ (via ValidationSectionCard) | ✓ | ✓ | ✓ | Likely at parity — verify only |
| 7 — Publish | `/learnings/[id]/publish` | `PublishStep.tsx` | ✓ (Card headers) | ✓ | ✓ | ✓ | At parity — minor pass only |

### New wizard shared infrastructure

| Component | Gap |
|-----------|-----|
| `StepIndicator.tsx` | No `subtitle` field — steps show number + label only |
| `WizardLayout.tsx` | No gap |
| `WizardSectionDivider` (ui/) | Dark mode colors hardcoded to slate — patch before adopting in new wizard |

### §24 detail panels — current state (talk detail page)

| Panel | File | Card usage | Polish state |
|-------|------|------------|-------------|
| `SectionEditPanel` | `components/detail/SectionEditPanel.tsx` | ✓ full Card | Good — likely at parity |
| `QuizEditPanel` | `components/detail/QuizEditPanel.tsx` | Unknown — not read | Needs assessment |
| `SettingsEditPanel` | `components/detail/SettingsEditPanel.tsx` | Unknown — not read | Needs assessment |
| `AddTargetLanguagePicker` | `components/detail/AddTargetLanguagePicker.tsx` | Unknown — not read | Needs assessment |

> **Note:** Chunk 5 opens each detail panel component before making assumptions. `SectionEditPanel` is confirmed at parity from code read; the others are flagged for assessment.

---

## 6. Reference Points

| Purpose | Path |
|---------|------|
| Legacy InputConfigStep (design reference) | `web/src/features/toolbox-talks/components/create-wizard/steps/InputConfigStep.tsx` |
| New wizard InputConfigStep (subject) | `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` |
| New wizard SettingsStep (most polished step — pattern reference) | `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx` |
| New wizard PublishStep (Card usage reference) | `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx` |
| WizardSectionDivider component | `web/src/components/ui/wizard-section-divider.tsx` |
| StepIndicator component | `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx` |
| WizardLayout component | `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx` |
| SectionEditPanel (§24, confirmed at parity) | `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx` |
| PHASE_5_STANDARDS — accessibility §9 | `docs/PHASE_5_STANDARDS.md` |

---

## 7. Chunk Specifications

### Chunk 1 — InputConfigStep (Step 1) Visual Polish

**Effort:** ~3 days  
**Demo cut:** Yes — first impression, largest visual debt  
**Files changed (frontend only):**
- `web/src/components/ui/wizard-section-divider.tsx` — patch dark mode tokens (one-line prerequisite)
- `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` — primary work
- No backend changes

**Scope:**

1. **Dark mode patch on WizardSectionDivider** — replace `text-slate-700` with `text-foreground`, `border-slate-300` with `border-border`. Required before any adoption.

2. **Section markers (1a–1e)** — Add `WizardSectionDivider` before each logical group: `1a Content Source`, `1b Translation Settings`, `1c Content Options`, `1d Sector`, `1e Audit Metadata`. Each group wrapped in `<section aria-labelledby>` with `WizardSectionDivider` marked `aria-hidden="true"`.

3. **Content source tile selector** — Change from `items-start` + `h-4` icons to `items-center` + `h-6` icons, vertically stacked (matching legacy). Active state unchanged (`border-primary bg-primary/5`).

4. **Translation Settings group** — Move Target Languages and Pass Threshold into a `flex items-start gap-3 rounded-lg border p-4` paired container matching the legacy pattern. Add helper text under Target Languages: "Content will be translated and validated for each selected language."

5. **Content Options toggles** — Replace `<input type="checkbox">` with shadcn `Checkbox` for video rights. Replace `<input type="checkbox">` with shadcn `Switch` for Include Quiz and Preserve Source Wording. Wrap each Switch row in `rounded-lg border p-4` with label + description on the left and Switch on the right (matching `SettingsStep.tsx:351-398` pattern exactly).

6. **Audit Metadata section** — Wrap the grid in `<Card><CardContent>`. Add company `<Select>` for Client Name (reuse `useAllCompanies` hook, same as legacy). Restore the preset/custom hybrid for Audit Purpose: `<Select>` populated from `auditPurposes` derived from tenant settings, with an "Other…" option that swaps to a free `<Textarea>` plus a "Presets" button to swap back.

7. **File dropzone** — Add `onDragOver={(e) => e.preventDefault()}` and `onDrop` handlers to both PDF and video dropzone containers (same pattern as legacy InputConfigStep lines 455–514).

8. **Helper text pass** — Add helper text under Source Text ("Minimum 50 words recommended" / character/word count), Pass Threshold ("Minimum score to pass validation"), Sector dropdown ("Used for regulatory scoring and compliance reporting"), Reviewer Name / Organisation / Role ("Pre-populated from your profile — edit if different").

**Pre-flight items:**
- `useAllCompanies` hook exists at `@/lib/api/admin/use-companies` — confirmed from legacy import
- `WizardSectionDivider` is in `components/ui/` — accessible from any path without new imports beyond existing
- `auditPurposes` preset logic exists in legacy `InputConfigStep` (lines 124–134) — port verbatim

---

### Chunk 2 — ParseStep + QuizStep (Steps 2+3) Visual Polish

**Effort:** ~1.5 days  
**Demo cut:** Yes  
**Files changed:**
- `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/components/QuizSettingsPanel.tsx` — section grouping

**ParseStep scope:**

1. **Section-ready state Card wrap** — When `hasSections`, wrap the section list header + `<SectionList>` in a Card (`<Card><CardContent>`). The "Sections (N) — reorder, edit, or delete" header becomes `<CardHeader><CardTitle>`.

2. **Re-parse button in section-ready state** — Legacy shows a "Regenerate" / "Re-parse" button in the top-right of the sections header when sections exist. Add an `<Button variant="outline" size="sm">` "Re-parse" button to the card header (right side), mirroring `QuizStep`'s "Regenerate All" button shape.

3. **Empty state visual weight** — The current `rounded-full border p-3` icon container is functional but thin. Upgrade to match the legacy wizard's empty state visual: centered vertically with slightly more vertical padding (`py-20`), the icon in a muted bg-primary/10 rounded circle.

**QuizStep scope:**

1. **Question list Card wrap** — Wrap the question list header + `<SectionQuestionGroup>` in a Card.

2. **QuizSettingsPanel section marker** — `QuizSettingsPanel` currently renders as a plain form block. Add a `WizardSectionDivider` before it labeled "Quiz Settings" so it reads as a distinct sub-section rather than trailing content.

3. **Empty state consistency** — Match ParseStep's empty state visual upgrade.

---

### Chunk 3 — SettingsStep (Step 4) Visual Polish

**Effort:** ~1 day  
**Demo cut:** Yes  
**Files changed:**
- `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`

**Scope:**

SettingsStep is the most polished new wizard step — Switch tiles and `<section>` structure are already in place. Work is a consistency and completeness pass.

1. **Section markers** — Add `WizardSectionDivider` above each `<section>`: `Details`, `Cover Image`, `Behaviour`, `Auto-assign`, `Slideshow`. The `<h2>` headings stay inside the `<section aria-labelledby>` block; the divider is purely visual (`aria-hidden`).

2. **Helper text on remaining fields:**
   - Refresher frequency: "Employees will be reminded to retake this learning at the selected interval."
   - Minimum video watch percentage: "Employees must watch at least this percentage of the video to proceed to the quiz."
   - Auto-assign due days: "New employees will be assigned this learning with this many days to complete it."

3. **Category helper text** — Add "Used to group learnings in reports and filters." below the Category `<Select>`.

4. **Minimum watch percent field width** — Current `className="w-24"` leaves the Input visually orphaned. Give it a `max-w-[8rem]` with a `%` suffix label, matching the legacy wizard's presentation.

---

### Chunk 4 — StepIndicator Subtitles + Steps 5–7 Verification Pass

**Effort:** ~1 day  
**Demo cut:** Yes (StepIndicator subtitles visible on every step)  
**Files changed:**
- `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts` — add subtitles to step definitions
- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx` — no change needed (WizardLayout passes steps from useStepNavigation)
- (Zero changes expected on) `TranslateStep.tsx`, `ValidateStep.tsx`, `PublishStep.tsx`

**StepIndicator subtitles scope:**

1. Add `subtitle?: string` to `StepItem` interface.
2. In `StepIndicator`, render `subtitle` below the step label at `sm:` breakpoint only (same visibility gate as the label), in `text-xs text-muted-foreground` — hidden when the step is unreachable or skipped.
3. Update `useStepNavigation` step definitions:

| Step | Subtitle |
|------|---------|
| 1 — Input & Config | "Upload source, set languages" |
| 2 — Parse | "Review AI-extracted sections" |
| 3 — Quiz | "Review generated questions" |
| 4 — Settings | "Title, refresher, certificate" |
| 5 — Translate | "Run translations per language" |
| 6 — Validate | "Review back-translation scores" |
| 7 — Publish | "Confirm and publish" |

**Verification pass scope (Steps 5–7):**

Read `TranslateStep.tsx`, `ValidateStep.tsx`, and confirm `PublishStep.tsx` (already read). Document visual state in Chunk 4's report. Expected finding: all three are at visual parity with or exceed the legacy wizard (they reuse `WizardTranslationPanel`, `ValidationSectionCard`, `Card` components from production surfaces). If any gap is found, add a targeted fix within this chunk's scope. If a gap exceeds the chunk's effort envelope, stop and report.

---

### Chunk 5 — §24 Detail Panels Visual Polish

**Effort:** ~1–1.5 days  
**Demo cut:** No — §24 panels not part of wizard walkthrough  
**Files assessed (read first, then fix as needed):**
- `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx` — confirmed at parity from recon
- `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx`
- `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`
- `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx`

**Scope:**

Open each panel. For each, assess against the same four structural patterns (section markers, bordered tiles, Card groupings, helper text). `SectionEditPanel` is already at parity; document this and move on. For the remaining three panels: apply the same design target as the wizard steps — Card wrapping for groups, Switch for boolean fields, helper text on non-obvious controls. No backend changes expected.

Do not invest more than 1.5 days total on this chunk — these panels are post-Demo and the scope is a polish pass, not a redesign.

---

## 8. Demo Cut Detail

| Chunk | Steps covered | Effort | Order |
|-------|--------------|--------|-------|
| **Chunk 1** | Step 1 — Input & Config | 3 d | First |
| **Chunk 2** | Steps 2+3 — Parse, Quiz | 1.5 d | Second (after 1) |
| **Chunk 3** | Step 4 — Settings | 1 d | Third (can parallel with 2) |
| **Chunk 4** | StepIndicator subtitles + Steps 5–7 verify | 1 d | Fourth (after 1–3) |
| **Total** | | **6.5–7 days** | |

Chunks 2 and 3 can run in parallel since they touch different files. Chunk 4 gates on Chunks 1–3 being merged to ensure the StepIndicator subtitle render is tested against the polished step content.

Toggle-flip readiness: all five chunks (including post-Demo Chunk 5) must land before the `UseNewWizard` toggle is flipped for paying tenants, per BACKLOG §25 definition of done. Demo itself unblocks at Chunks 1–4.

---

## 9. Cross-Cutting Concerns

### WizardSectionDivider dark mode patch

This patch (replacing hard-coded slate tokens with `text-foreground` / `border-border` in `wizard-section-divider.tsx`) affects the legacy wizard too — it uses the component in `create-wizard/steps/InputConfigStep.tsx`. The change is an improvement there (better dark mode), not a regression. Verify visually in both wizards after the patch.

### shadcn Checkbox import

The new wizard's `InputConfigStep` imports `MultiSelectCombobox`, `Form`, `Select`, etc. from shadcn but does not import `Checkbox`. After Chunk 1, it must add:

```tsx
import { Checkbox } from '@/components/ui/checkbox';
```

`Checkbox` is confirmed present at `web/src/components/ui/checkbox.tsx`.

### Switch import (new wizard InputConfigStep)

`Switch` is imported in `SettingsStep` but not `InputConfigStep`. Chunk 1 adds the import. Confirmed at `web/src/components/ui/switch.tsx`.

### Company list for Client Name

The legacy wizard imports `useAllCompanies` from `@/lib/api/admin/use-companies` and uses `companyList.map((c) => <SelectItem value={c.companyName}>)`. The new wizard has no companies query. Chunk 1 adds this import and the `Select` control. No backend change required — `GET /api/companies` is already wired for `Core.ManageCompanies`; admins using the wizard will have that permission.

> **Edge case:** `useAllCompanies` returns a paginated response. Check whether it uses the `Result<T>` envelope (reads `.data.data`) or the direct DTO convention (reads `.data`) before wiring in Chunk 1. Per Note 18 (CLAUDE.md), the `CompaniesController` is a Core CRUD controller → `Result<T>` envelope → frontend reads `response.data.data`.

### StepItem interface change in Chunk 4

`StepItem` is defined in `StepIndicator.tsx` and re-exported via `WizardLayout.tsx`. The `subtitle` field is optional, so existing callers (the page components that pass `steps` from `useStepNavigation`) require no immediate update — they simply don't pass the field and subtitles don't render. Chunk 4 adds subtitles to `useStepNavigation`'s step definitions.

### Audit Purpose preset data dependency

The legacy wizard reads `ValidationAuditPurposes` from tenant settings (via `useTenantSettings`). The new wizard's `InputConfigStep` already imports `useTenantSettings` and reads `ValidationPassThresholds`. The preset extraction logic can be ported verbatim from the legacy (lines 124–134). No backend change.

---

## 10. Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `WizardSectionDivider` dark mode patch introduces regressions in legacy wizard | Low | Legacy wizard rarely used in dark mode; visual regression test in both wizards after Chunk 1 |
| Company list `Select` pagination silently truncates tenants with >50 companies | Low–Medium | Legacy wizard has same issue and ships; flag for post-§25 backlog item. Does not block §25 |
| Chunk 2 `SectionList` reuse creates styling conflicts (SectionList is shared between ParseStep and SectionEditPanel) | Low | SectionList takes `className` props — add wrapper styling in ParseStep's Card rather than inside SectionList |
| Chunk 4 verification reveals TranslateStep or ValidateStep needs non-trivial polish | Medium | Chunk 4 has a 1-day envelope; if a gap exceeds it, stop and report as per the standard scope-discipline trigger |
| §24 detail panels (Chunk 5) reveal deeper structural issues | Low | Panels are post-Demo; stop and report if out-of-scope structural work is needed |
| `StepIndicator` subtitle rendering breaks on narrow viewports | Low | Subtitles gated at `sm:` breakpoint, same as step labels; no new narrow-viewport issue |

---

## 11. Out-of-Scope Items Flagged

| Item | Status |
|------|--------|
| Mobile audit (§7.2) | Out of scope per recon brief — cross-reference only |
| Accessibility audit | Changes in §25 must not degrade WCAG 2.1 AA compliance (PHASE_5_STANDARDS §9); no separate audit scoped here |
| Legacy wizard removal | Separate workstream; §25 does not touch the legacy wizard except the `WizardSectionDivider` dark mode patch (which improves it) |
| Wizard cutover toggle flip decision | Separate decision (per CLAUDE.md Note 29); §25 delivers the polish; the flip timing is the operator's call |
| §5.7 Demo refresh operational steps | Separate workstream; §25 is the unblocking prerequisite |
| Playwright E2E coverage for wizard visual state | Not in scope per PHASE_5_STANDARDS §11.3 |
| Cover image upload visual polish in SettingsStep | The `CoverImageUpload` component is a sub-component; Chunk 3 does not redesign it — visual pass only |
| BACKLOG §25 has been amended since 2026-06-18 | No amendment found — BACKLOG ends with `§25` as the last entry before `_End of BACKLOG.md._` |

---

## 12. Files Read

| File | Purpose |
|------|---------|
| `BACKLOG.md` | §25 full entry, including scope and definition of done |
| `docs/PHASE_5_STANDARDS.md` | Standards bar for all Phase 5 chunks |
| `docs/24/chunk-sizing-recon.md` | §24 recon — file inventory, component reusability classification |
| `web/src/components/ui/wizard-section-divider.tsx` | Shared component — current implementation |
| `web/src/components/ui/*.tsx` (glob) | shadcn component inventory |
| `web/src/features/toolbox-talks/components/**/*.tsx` (glob) | Full feature component inventory |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/**/*.tsx` (glob) | New wizard page shells |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx` | Step 1 page shell |
| `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx` | Layout |
| `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx` | Step indicator |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` | New wizard Step 1 (subject) |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | New wizard Step 2 (subject) |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx` | New wizard Step 3 (subject) |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx` | New wizard Step 4 (subject) |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx` | New wizard Step 7 (subject) |
| `web/src/features/toolbox-talks/components/create-wizard/steps/InputConfigStep.tsx` | Legacy wizard Step 1 (design reference) |
| `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx` | §24 detail panel |

_TranslateStep and ValidateStep not read — subject of Chunk 4's verification pass. §24 panels QuizEditPanel, SettingsEditPanel, AddTargetLanguagePicker not read — subject of Chunk 5's pre-flight assessment._

---

## 13. Report Written

`docs/25/recon.md`
