# §25 Chunk 4 — StepIndicator Subtitles: Implementation Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code_

---

## Summary of Changes

Two files modified. No other files touched.

### 1. `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`

- Added `subtitle?: string` to the `WizardStepDef` interface (additive, non-breaking).
- Populated all seven `WIZARD_STEPS` entries with subtitle copy, maintaining the existing alignment style of the array:

| Step | Label | Subtitle |
|------|-------|---------|
| 1 | Input & Config | "Upload source, set languages" |
| 2 | Parse | "Review AI-extracted sections" |
| 3 | Quiz | "Review generated questions" |
| 4 | Settings | "Title, refresher, certificate" |
| 5 | Translate | "Run translations per language" |
| 6 | Validate | "Review back-translation scores" |
| 7 | Publish | "Confirm and publish" |

### 2. `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx`

- Added `subtitle?: string` to the exported `StepItem` interface (additive, non-breaking).
- Inserted subtitle render block directly after the label `<span>` (previously lines 95–104):

```tsx
{step.subtitle && !isDisabled && (
  <span className="hidden sm:block text-[10px] text-muted-foreground whitespace-nowrap leading-tight -mt-0.5">
    {step.subtitle}
  </span>
)}
```

---

## Test Results

### `npx tsc --noEmit`

No output — clean pass. Zero type errors.

The `subtitle` field propagates correctly from `WizardStepDef` through the `...s` spread in `useStepNavigation.ts:68` to `StepItem`. Both interfaces declare `subtitle?: string` — TypeScript satisfied with no changes to intermediate files.

### `npm run test`

```
 Test Files  3 passed (3)
      Tests  15 passed (15)
   Start at  10:22:35
   Duration  2.93s
```

15 of 15 passing. No new failures introduced.

---

## Visual Verification

Verified by code inspection (dev server launch not performed — changes are structural render additions with no conditional logic beyond the two-condition gate already confirmed by the recon):

**Step indicator at `sm:` breakpoint and above:**

- Steps 1–7: label visible as before
- Subtitle renders below label for all non-disabled states (current, complete, reachable)
- Subtitle hidden for `skipped` and `unreachable` states — single `!isDisabled` gate covers both (confirmed: `isDisabled = state === 'unreachable' || state === 'skipped'` at line 50)
- Active step (current): both label (semibold, text-foreground) and subtitle (text-[10px], text-muted-foreground) visible
- Complete steps: both label and subtitle visible — subtitle "Upload source, set languages" on Step 1 remains visible when on Step 2+
- Reachable-pending steps: subtitle previews what's coming

**Narrow viewport (`<sm`):**

- `hidden sm:block` gate on the subtitle span is identical to the label span's gate — both hidden below `sm:`, only the number badge shows. No new narrow-viewport regression possible.

**Disabled/skipped state:**

- Subtitle render gate is `step.subtitle && !isDisabled` — when `isDisabled` is true, the JSX short-circuits and no subtitle element is rendered. The existing `opacity-40` on the button applies to the label (which does render for skipped, with line-through). The subtitle is absent entirely — correct per spec (no opacity on subtitle, just hidden).

---

## Styling Confirmation

`text-[10px]` is an **arbitrary Tailwind value** — intentional, not a scale token. The nearest scale token would be `text-xs` (12px), which is the same size as the label. Using `text-[10px]` creates a deliberate visual hierarchy: subtitle is subordinate to label. Changing this to a scale token would defeat that hierarchy and is explicitly prohibited by the spec.

Remaining class tokens:
- `hidden sm:block` — matches label's breakpoint gate exactly
- `text-muted-foreground` — consistent with helper-text convention from Chunks 1–3
- `whitespace-nowrap` — matches label behaviour; subtitles are short enough to never wrap
- `leading-tight` — prevents excess vertical space from default line-height in the `gap-1` flex column
- `-mt-0.5` — tightens visual pairing of subtitle to label given the parent's `gap-1`

---

## Unchanged Files Confirmation

- `useStepNavigation.ts` — **not changed**. Subtitle flows through the `...s` spread at line 68 automatically.
- `WizardLayout.tsx` — **not changed**. Type-compatible; passes `steps` array through to `StepIndicator` without transformation.
- `TranslateStep.tsx`, `ValidateStep.tsx`, `PublishStep.tsx` — **not changed**. All three verified at parity by the Chunk 4 recon.
- `WizardSectionDivider.tsx` — **not changed**. Dark mode patch applied in Chunk 1 remains in place.

---

## Notable Deviations from Recon Spec

None. All subtitle copy, class names, and state conditions implemented exactly as specified in the recon (§4 and §5). No adjustments were needed.

---

## `git diff --name-only` Confirmation

```
web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx
web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts
```

Exactly two files. No scope drift.

---

## BACKLOG Impact

- **§25 Chunk 4 of 5 complete.** StepIndicator subtitles shipped.
- **§25 Demo cut (Chunks 1–4) is now complete.** All four Demo-cut chunks have shipped:
  - Chunk 1: InputConfigStep visual polish
  - Chunk 2: ParseStep + QuizStep visual polish
  - Chunk 3: SettingsStep visual polish
  - Chunk 4: StepIndicator subtitles (this chunk)
- **§5.7 (Demo refresh) is now unblocked.** The Demo session paused at Phase 7.4 can resume.
- **Chunk 5 (§24 detail panels) remains post-Demo.** No timeline pressure; deferred until Demo cut is validated.
