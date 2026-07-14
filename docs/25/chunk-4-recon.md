# §25 Chunk 4 — StepIndicator Subtitles + Steps 5–7 Verification: Recon Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code (read-only investigation — no code changed)_

---

## 1. Verification Verdict — StepIndicator + stepOrder

### StepIndicator.tsx — no drift

File: `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx`

**Current `StepItem` interface (lines 6–12):**
```ts
export interface StepItem {
  number: number;
  label: string;
  reachable: boolean;
  skipped?: boolean;
}
```

No `subtitle` field — exactly as the design recon anticipated. Adding `subtitle?: string` is a clean, non-breaking additive change.

**Render structure (lines 64–108):** Each step button is `flex flex-col items-center gap-1`. The number badge renders first (lines 79–93), then the label span (lines 95–104). The label uses `hidden sm:block text-xs whitespace-nowrap` — visible only at `sm:` and above. A subtitle `<span>` inserted directly below the label span, with the same `hidden sm:block` gate, will integrate without layout changes.

**Step states** (line 20): `'current' | 'complete' | 'reachable' | 'skipped' | 'unreachable'`

`isDisabled` (line 50) covers both `skipped` and `unreachable`. Hiding the subtitle when `isDisabled` is true gives the right behaviour for both states with a single condition.

**`StepItem` is exported** from `StepIndicator.tsx` and re-imported in `WizardLayout.tsx` (line 6). Adding an optional field to the interface requires no changes in `WizardLayout.tsx` — it passes the `steps` array through to `<StepIndicator>` without transformation (lines 48–52).

### stepOrder.ts — preferred location for subtitle data

File: `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`

**`WizardStepDef` interface (lines 9–13):**
```ts
export interface WizardStepDef {
  number: number;
  label: string;
  slug: string;
}
```

**`WIZARD_STEPS` array (lines 15–23):**
```ts
{ number: 1, label: 'Input & Config',  slug: 'new'      }
{ number: 2, label: 'Parse',           slug: 'parse'     }
{ number: 3, label: 'Quiz',            slug: 'quiz'      }
{ number: 4, label: 'Settings',        slug: 'settings'  }
{ number: 5, label: 'Translate',       slug: 'translate' }
{ number: 6, label: 'Validate',        slug: 'validate'  }
{ number: 7, label: 'Publish',         slug: 'publish'   }
```

These labels match the design recon's subtitle table exactly.

**Why `stepOrder.ts` and not `useStepNavigation.ts`:** `useStepNavigation` builds `reachableSteps` by spreading each `WizardStepDef` entry via `...s` (line 68) and appending `reachable` and `skipped`. A `subtitle` field on `WizardStepDef` flows through automatically — no changes to `useStepNavigation.ts` needed.

Adding `subtitle?: string` to `WizardStepDef` and populating the seven entries keeps all step definition metadata co-located in `stepOrder.ts`. This is the correct location.

---

## 2. Verification Verdict — Steps 5–7

### Step 5 — TranslateStep: PASS

File: `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx`

**Top-level structure (lines 86–134):**
- Fragment root (WorkflowSubscriber elements are invisible)
- `<div className="space-y-6">`
  - Header: `<h2 className="text-base font-semibold">Translate</h2>` + `<p className="text-sm text-muted-foreground mt-1">` description (lines 100–106)
  - "Start All" button (lines 108–117)
  - `<div className="space-y-3" role="list">` — one `WizardTranslationPanel` per language (lines 120–131)

**Parity assessment:**
- ✓ Step-level intro text with description — covers what Step 5 does
- ✓ `WizardTranslationPanel` — the production shared component handles all per-language state, progress, and actions
- ✓ Empty state for no languages configured (lines 40–49): `rounded-lg border border-dashed p-8 text-center`
- ✓ Loading state (line 38): `<LoadingState label="Loading…" />`
- ✓ No section markers needed — single-concern step, one type of content

The design recon's `✓ (via WizardTranslationPanel)` claim for section markers refers to structure within each language tile. The step itself has no need for sub-section markers. **No polish gaps.**

### Step 6 — ValidateStep: PASS

File: `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx`

**Top-level structure (lines 197–342):**
- Fragment root (WorkflowSubscriber, SendExternalReviewDialog — invisible/dialog)
- `<div className="space-y-6">`
  - Header: `<h2 className="text-base font-semibold">Validate</h2>` + `<p>` description (lines 225–230)
  - Language tab strip — conditional for 2+ languages, pill-button switcher (lines 235–257)
  - Active run region:
    - `ValidationProgressPanel` — aggregate score + status counts (lines 262–274)
    - "Send for external review" button — conditional (lines 276–294)
    - Per-section `ValidationSectionCard` list (lines 297–318)
  - Empty state for no run yet (lines 321–326): `rounded-lg border border-dashed`
  - Summary bar (lines 329–339): `rounded-lg border bg-muted/30 px-4 py-3` with "Ready to publish" indicator

**Parity assessment:**
- ✓ Step-level header with clear description
- ✓ `ValidationProgressPanel` and `ValidationSectionCard` from production shared components
- ✓ Multi-language tab strip
- ✓ "Ready to publish" status indicator
- ✓ Empty states and loading states handled
- ✓ External review integration
- ✓ No section markers needed — linear per-section list

Imports `ValidationProgressPanel` and `ValidationSectionCard` from `create-wizard/steps/validate/` (lines 18–19) — shared with the legacy wizard. Structurally fine. **No polish gaps.**

### Step 7 — PublishStep: PASS (exceeds parity)

File: `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx`

**Top-level structure (lines 95–128 + sub-components):**
- `<div className="space-y-6">`
  - `PublishErrorAlert` — conditional destructive `Alert` (lines 98–147)
  - `ContentSummaryPanel` — Card with cover image, title, description, badge row for category/refresher/certificate/auto-assign/watch% (lines 154–233)
  - `ThreeColumnSummary` — `grid sm:grid-cols-3` of Cards: Content, Back-translation scores, Quiz (lines 239–362)
  - `AuditMetadataPanel` — Card, hidden when no fields populated (lines 368–400)
  - `ExternalReviewWarningBanner` — amber `Alert`, conditional (lines 406–449)
  - Publishing spinner (lines 121–126)
- `PublishSuccessState` — exported, shown by page wrapper on publish success (lines 471–481)

**Parity assessment:**
- ✓ Cards used extensively — four distinct Card panels
- ✓ Well-structured visual hierarchy
- ✓ Badge-based status summary (refresher, certificate, auto-assign, watch%)
- ✓ Dark mode throughout (conditional `dark:` classes)
- ✓ Error state, success state, loading state all covered
- ✓ Audit metadata rendered in Card with dl/dd structure
- ✓ External review warning

**Exceeds the "minor pass only" bar.** No polish gaps, no parity debt.

---

## 3. Recommended Path

**Path A — Steps 5–7 verify as PASS.**

All three steps confirmed at visual parity. Chunk 4 is exclusively the StepIndicator subtitle work. No changes to TranslateStep, ValidateStep, or PublishStep.

---

## 4. Spec Gap Resolutions

### 4.1 Subtitle copy — confirmed

The design recon's proposed wording is accurate for all seven steps after reading the full implementations:

| Step | Label | Subtitle |
|------|-------|---------|
| 1 | Input & Config | "Upload source, set languages" |
| 2 | Parse | "Review AI-extracted sections" |
| 3 | Quiz | "Review generated questions" |
| 4 | Settings | "Title, refresher, certificate" |
| 5 | Translate | "Run translations per language" |
| 6 | Validate | "Review back-translation scores" |
| 7 | Publish | "Confirm and publish" |

Step 4's subtitle lists three representative items from Settings — accurate and concise. No adjustments needed.

### 4.2 Breakpoint behaviour

The existing label (lines 96–100 of `StepIndicator.tsx`) uses `hidden sm:block`. The subtitle should use the same gate: `hidden sm:block`. On narrow viewports, both label and subtitle are hidden; only the numbered badge shows. This matches the current narrow-viewport behaviour — no new regression risk.

**Recommendation: always render subtitle at `sm:` and up** (not just for the active step). Having subtitles visible on all non-disabled steps is more informative and consistent with how labels behave.

### 4.3 State-by-state subtitle visibility

| State | Show subtitle? | Rationale |
|-------|---------------|-----------|
| current | ✓ Yes | Contextualises the active step |
| complete | ✓ Yes | Helps recall what the step covered |
| reachable (pending) | ✓ Yes | Previews what's coming |
| skipped | ✗ No | Step bypassed; subtitle adds noise. `isDisabled = true` covers this |
| unreachable | ✗ No | Can't be accessed yet; subtitle unnecessary. `isDisabled = true` covers this |

**Implementation:** single condition — `!isDisabled` — gates the subtitle render. Same condition the label already uses for the `opacity-40` class (line 99). No new logic required.

### 4.4 TypeScript shape

`StepItem` is defined and exported at `StepIndicator.tsx:6–12`. It is imported by `WizardLayout.tsx` (line 6). No other consumers. The `subtitle?: string` addition is optional and additive — zero breaking changes.

`WizardStepDef` is defined at `stepOrder.ts:9–13`. Adding `subtitle?: string` there is equally additive. The `useStepNavigation` spread `...s` at line 68 of `useStepNavigation.ts` propagates the field automatically — **no changes to `useStepNavigation.ts`**.

---

## 5. Sized Implementation Chunk

**Path A — StepIndicator subtitle work only.** Two files, ~22–30 lines of change.

### File 1: `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`

1. Add `subtitle?: string` to `WizardStepDef` interface
2. Populate `subtitle` on all seven `WIZARD_STEPS` entries with the wording from §4.1 above

Estimated diff: ~10 lines (1 interface line + 7 subtitle entries).

### File 2: `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx`

1. Add `subtitle?: string` to the exported `StepItem` interface (line 6–12)
2. After the label `<span>` (currently lines 95–104), insert:
   ```tsx
   {step.subtitle && !isDisabled && (
     <span className="hidden sm:block text-[10px] text-muted-foreground whitespace-nowrap leading-tight -mt-0.5">
       {step.subtitle}
     </span>
   )}
   ```
   — `text-[10px]` smaller than the `text-xs` label to visually subordinate it; `-mt-0.5` to tighten the gap with the label; `leading-tight` prevents double-spacing in the button's `gap-1` column layout.

Estimated diff: ~12 lines (1 interface line + ~11 render lines).

**No changes to:**
- `useStepNavigation.ts` — subtitle flows through the spread automatically
- `WizardLayout.tsx` — type-compatible, no structural change
- `TranslateStep.tsx`, `ValidateStep.tsx`, `PublishStep.tsx` — all verified at parity

**Verification scope:** TypeScript check (`npx tsc --noEmit`) + visual confirm on all 7 steps in the new wizard. Subtitle should appear/hide correctly at all states. Test narrow viewport (mobile) to confirm no regressions.

---

## 6. Files Read

| File | Purpose |
|------|---------|
| `docs/25/recon.md` | §25 design recon — Chunk 4 spec reference |
| `docs/25/chunk-1-fix.md` | Convention reference (WizardSectionDivider, dark mode) |
| `docs/25/chunk-2-fix.md` | Convention reference (ParseStep, QuizStep) |
| `docs/25/chunk-3-fix.md` | Convention reference (SettingsStep, helper text) |
| `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx` | Subject — StepItem interface, render structure |
| `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts` | Subject — WIZARD_STEPS definitions, WizardStepDef |
| `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts` | Subject — step assembly, spread pattern |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` | Verification — Step 5 |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx` | Verification — Step 6 |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx` | Verification — Step 7 |
| `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx` | Supporting — StepItem pass-through confirmed |
| `web/src/components/ui/wizard-section-divider.tsx` | Supporting — dark mode patch confirmed in place |

---

## 7. Report Written

`docs/25/chunk-4-recon.md`

---

## 8. Out-of-Scope Items Flagged

1. **TranslateStep "Start All" button visual position** — sits between intro text and the language list in its own `flex justify-end` div. Minor layout wrinkle; not a parity gap. Could be aligned into the header row (right side) for tighter presentation. Not blocking Demo; park post-§25.

2. **`LANG_NAMES` duplication** — the language name lookup is defined in both `ValidateStep.tsx:26–35` and `PublishStep.tsx:49–52` (PublishStep includes additional codes). Small DRY violation. Extract to a shared util if/when a third consumer appears.

3. **`ValidationProgressPanel` / `ValidationSectionCard` import path** — both import from `create-wizard/steps/validate/` (legacy wizard directory). Functionally correct — these are shared production components. A future structural refactor might move them to a more neutral shared directory. Not §25 scope.

4. **Step 4 subtitle coverage** — "Title, refresher, certificate" covers three of the five SettingsStep sections (Details, Behaviour, Auto-assign) but omits Cover Image and Slideshow. Within the 30-char subtitle constraint, this is the best achievable summary. Flag for future review only if user research surfaces confusion.

5. **`WizardSectionDivider` dark mode patch** — already applied in Chunk 1 (`text-foreground`, `border-border` confirmed at lines 11–12 of `wizard-section-divider.tsx`). No action needed in Chunk 4.
