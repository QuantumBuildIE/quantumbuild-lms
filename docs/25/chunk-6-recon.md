# §25 Chunk 6 — Outer Step Container Shell: Recon Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code (read-only investigation — no code changed)_

---

## 1. Legacy Shell Spec

**Source:** `web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx:449–452`

```tsx
{/* Step Content */}
<Card>
  <CardContent className="pt-6">{renderStepContent()}</CardContent>
</Card>
```

The shell is a plain shadcn `Card` with one `CardContent` child. The legacy does not use any conditional shell logic — all seven steps get exactly the same wrapper, regardless of their internal structure.

**Effective styling from `card.tsx:5–16` and `CardContent:64–71`:**

| Property | Token / Class | Resolved value |
|----------|--------------|----------------|
| Background | `bg-card` | White (light) / dark surface (dark) |
| Text colour | `text-card-foreground` | Theme text |
| Border | `border` | 1px solid `hsl(var(--border))` |
| Border radius | `rounded-xl` | 12px |
| Shadow | `shadow-sm` | Subtle drop shadow |
| Top/bottom padding | `py-6` on Card | 24px |
| Left/right padding | `px-6` on CardContent | 24px |
| Additional top padding | `pt-6` on CardContent | +24px (total 48px from top border to content) |
| Flex layout | `flex flex-col gap-6` on Card | Irrelevant — CardContent is the sole child |

**Dark mode:** No explicit `dark:` overrides — handled entirely by the `bg-card` and `border` theme tokens. Both resolve correctly in dark mode via the CSS variable system. No hard-coded slate values.

**Page background context:** `(authenticated)/layout.tsx:47` sets `bg-slate-50 dark:bg-slate-950` on the outer container. The `bg-card` shell (white in light mode) is visually distinct from `bg-slate-50` (light gray). The shell registers correctly without any page-level background adjustment.

**Contrast check:** No `max-width` or centering on the Card itself — it fills the column its parent provides. The legacy wizard's parent is `<div className="space-y-6">` at `CreateWizard.tsx:326`. The new wizard's parent is WizardLayout's `<div className="flex flex-col min-h-0">` which itself lives in `container mx-auto px-4 py-6` from the authenticated layout `<main>`. Both patterns fill the available column naturally.

---

## 2. Per-Step Current State

### WizardLayout — step content slot

`WizardLayout.tsx:55–58`:
```tsx
{/* Step content */}
<div className="flex-1 min-h-0">
  {children}
</div>
```

Bare `<div>`. No background, no border, no shadow. This is the insertion point for the shell.

### New wizard step outer wrappers (after Chunks 1–5)

| Step | File | Top-level element | Internal Cards from Chunks 1–3 |
|------|------|-------------------|-------------------------------|
| 1 — Input & Config | `InputConfigStep.tsx:425` | `<Form><form className="space-y-8">` | ✓ Audit Metadata `<Card><CardContent>` |
| 2 — Parse | `ParseStep.tsx:273` | `<div className="space-y-4">` | ✓ SectionList `<Card>` with CardHeader/CardTitle/CardAction |
| 3 — Quiz | `QuizStep.tsx:308` | `<div className="space-y-6">` | ✓ Card-lite `<div className="rounded-xl border shadow-sm bg-card overflow-hidden">` |
| 4 — Settings | `SettingsStep.tsx:172` | `<Form><form className="space-y-8">` | None — WizardSectionDividers + Switch tiles only |
| 5 — Translate | `TranslateStep.tsx:86` | `<>` fragment → `<div className="space-y-6">` | None — WizardTranslationPanel (production component) |
| 6 — Validate | `ValidateStep.tsx:197` | `<>` fragment → `<div className="space-y-6">` | None — ValidationSectionCard (production component) |
| 7 — Publish | `PublishStep.tsx:95` | `<div className="space-y-6">` | ✓ Four inner Cards (ContentSummaryPanel, three ThreeColumnSummary Cards, AuditMetadataPanel) |

**No step has a background, border, shadow, or rounded-corner treatment on its outermost element.** Every step renders as a structureless div or form on the page grid. This is the gap.

---

## 3. Per-Step Compatibility Verdict

| Step | Compatible with outer shell? | Internal conflict? | Recommendation |
|------|-------|-------|------|
| 1 — Input & Config | **CLEAN** | Audit Metadata Card sits inside padded shell — same nested-card pattern the legacy wizard uses for this exact section | Keep inner Card; shell wraps cleanly |
| 2 — Parse | **CLEAN** | Section list Card has its own CardHeader identity; inner border inside shell's padding is deliberate visual hierarchy | Keep inner Card; shell wraps cleanly |
| 3 — Quiz | **CLEAN** | Card-lite's `rounded-xl border shadow-sm bg-card` inside the shell creates nested depth — same pattern the legacy uses for inner Cards | Keep Card-lite as-is; the nested shadow is consistent with legacy Card-in-Card behaviour |
| 4 — Settings | **CLEAN** | No inner Cards — WizardSectionDividers and Switch tiles sit directly in the shell surface | Shell wraps cleanly |
| 5 — Translate | **CLEAN** | React fragment top-level; WorkflowSubscriber components are invisible (no DOM output); shell wraps the visible `<div className="space-y-6">` | Shell wraps cleanly; invisible subscribers inside shell are no-op |
| 6 — Validate | **CLEAN** | Same fragment pattern as Translate | Shell wraps cleanly |
| 7 — Publish | **CLEAN** | Four inner Cards inside the shell mirrors the legacy pattern. Shell treats the whole step as one surface; inner Cards provide per-section identity | Keep all inner Cards; shell wraps cleanly |

**No step is SHELL NOT APPLICABLE.** All seven steps are clean candidates. The PublishStep — the most likely exception candidate — is actually the same pattern the legacy wizard ships: outer shell wrapping multiple inner Cards. The legacy wizard does not exempt PublishStep from the shell.

**The "double card" concern does not apply here.** Chunk 2's QuizStep avoided a double-card by using a Card-lite instead of shadcn `<Card>` for `SectionQuestionGroup` because `SectionQuestionGroup` already had its own `border rounded-lg` container. The Card-lite sits inside the outer shell just fine — it has its own header bar that gives it identity, and its inner border against the shell padding reads as a contained data region, not a duplicate frame.

---

## 4. Implementation Location Recommendation

**Shell in `WizardLayout.tsx` — single change, all steps inherit uniformly.**

Rationale:
- This is exactly what the legacy wizard does (`CreateWizard.tsx:450–452`) — one Card wrapping `renderStepContent()`.
- No per-step conditional logic needed — all seven steps are CLEAN (§3).
- WizardLayout already owns the step content slot; adding the shell there keeps the concern collocated.
- Per-step addition (option B) would scatter 7 identical wrapper additions with no benefit.
- A new `WizardStepShell` component (option C) is unnecessary overhead for a two-element wrapper that matches an existing shadcn primitive exactly.

**Change in WizardLayout:**

```tsx
{/* Step content */}
<Card>
  <CardContent className="pt-6">
    {children}
  </CardContent>
</Card>
```

This requires importing `Card` and `CardContent` from `@/components/ui/card`. Both are already used throughout the wizard step components; no new shadcn component is needed.

The existing `flex-1 min-h-0` on the current div can be dropped. The new wizard is page-per-step (not a single SPA), so flex-fill height management on the content slot is not needed. The Card's height will be driven by content.

---

## 5. Within-Shell Adjustment Recommendations

### Are any Chunks 1–3 internal Cards now redundant?

| Step | Internal Card | Source | Still needed inside shell? | Action |
|------|---------------|--------|---------------------------|--------|
| 1 — Input & Config | Audit Metadata `<Card>` | Chunk 1 | **Yes** — the inner Card groups five related fields (Reviewer Name, Role, Org, Client, Audit Purpose) visually. The shell provides the step surface; the inner Card provides section-level grouping. Same pattern the legacy ships. | Keep |
| 2 — Parse | Section list `<Card>` with CardHeader | Chunk 2 | **Yes** — the inner Card's CardHeader provides the "Sections (N)" title and Re-parse action. Without the Card, these header elements have no enclosing container. | Keep |
| 3 — Quiz | Card-lite wrapper | Chunk 2 | **Yes** — the Card-lite's header bar provides "Quiz Questions (N)" title and Regenerate All action. It also manages the `overflow-hidden` for SectionQuestionGroup's own border radius. | Keep |
| 4 — Settings | None | — | N/A | N/A |
| 5–7 | Production components / inner Cards | Prior art | **Yes** — production components own their styling; inner Cards in PublishStep provide per-section identity | Keep all |

**No Chunks 1–5 changes are needed.** The internal Card structures remain appropriate and intentional inside the outer shell. Chunk 6 does not require undoing any prior work.

### Visual hierarchy check

Shell (outer): `bg-card border rounded-xl shadow-sm` + `px-6 py-6` padding  
Inner Cards: `bg-card border rounded-xl shadow-sm` (same tokens)

This is "card within card" — both surfaces use the same base tokens. This is not a visual bug: the inner Cards are inset within the shell's padding, so the outer border registers as the step boundary and the inner border registers as a section grouping. The legacy wizard uses exactly this pattern for Audit Metadata in InputConfigStep.

If at any point the nested depth feels too heavy, the inner Cards can be converted to Card-lite (no shadow) in a separate future chunk. That is not part of Chunk 6's scope.

---

## 6. Sized Implementation Chunk

### Scope

**One file changed:** `WizardLayout.tsx`

**Change:**
1. Add `Card` and `CardContent` to the import from `@/components/ui/card`.
2. Replace the `<div className="flex-1 min-h-0">` step content wrapper with `<Card><CardContent className="pt-6">{children}</CardContent></Card>`.

```diff
-import { ChevronLeft } from 'lucide-react';
-import { Button } from '@/components/ui/button';
+import { ChevronLeft } from 'lucide-react';
+import { Button } from '@/components/ui/button';
+import { Card, CardContent } from '@/components/ui/card';

...

-      <div className="flex-1 min-h-0">
-        {children}
-      </div>
+      <Card>
+        <CardContent className="pt-6">
+          {children}
+        </CardContent>
+      </Card>
```

**Files affected:** 1  
**Lines changed:** ~6 (import line + 3 lines wrapper → 4 lines Card wrapper)  
**Effort:** < 0.5 day (implementation is ~10 minutes; the work is verification — run the wizard across all seven steps and visually confirm the shell)

### Verification checklist

- Step 1 (Input & Config): Shell renders. Audit Metadata inner Card sits inside shell with padded inset. WizardSectionDividers render inside the shell surface.
- Step 2 (Parse): Shell renders. Section list Card has a nested border inside shell. Re-parse button and Regenerate All button render inside the shell surface.
- Step 3 (Quiz): Shell renders. Card-lite container renders inside shell. QuizSettingsPanel divider and content render on the shell surface below the Card-lite.
- Step 4 (Settings): Shell renders. Switch tiles render directly on the shell surface. WizardSectionDividers render on the shell surface.
- Step 5 (Translate): Shell renders. WizardTranslationPanel renders inside the shell. WorkflowSubscribers are invisible — no visual effect.
- Step 6 (Validate): Shell renders. ValidationSectionCards render inside the shell.
- Step 7 (Publish): Shell renders. All four inner Cards (ContentSummary, ThreeColumnSummary × 3, AuditMetadata) render inside the shell with space-y-6 gaps.
- Navigation bar (border-t strip at the bottom of WizardLayout): renders outside the Card shell — the border-t separates the Card shell above from the Back/Continue bar below. Confirm visual separation is correct.
- Dark mode: `bg-card` and `border` tokens resolve correctly — verify if dark mode is testable in the dev environment.

### BACKLOG update

Chunk 5's report declared "§25 is closed." Chunk 6 re-opens it with a single implementation item. After Chunk 6 ships, §25 is closed again. BACKLOG should be updated in the implementation report (not in this recon).

---

## 7. Files Read

| File | Purpose |
|------|---------|
| `docs/25/recon.md` | Original §25 design recon — confirmed outer shell was not in scope for Chunks 1–5 |
| `docs/25/chunk-1-fix.md` | Chunk 1 report — confirmed no outer shell work done |
| `docs/25/chunk-2-fix.md` | Chunk 2 report — confirmed Card-lite pattern for QuizStep |
| `docs/25/chunk-3-fix.md` | Chunk 3 report — confirmed no outer shell work done |
| `docs/25/chunk-4-fix.md` | Chunk 4 report — confirmed no outer shell work done |
| `docs/25/chunk-5-fix.md` | Chunk 5 report — declared §25 closed; confirmed this chunk reopens it |
| `web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx` | Legacy wizard — authoritative shell spec (`<Card><CardContent className="pt-6">`) |
| `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx` | New wizard shared layout — shell insertion point (`<div className="flex-1 min-h-0">`) |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` | Step 1 — top-level wrapper and inner Card confirmed |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | Step 2 — top-level wrapper and inner Card confirmed |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx` | Step 3 — top-level wrapper and Card-lite confirmed |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx` | Step 4 — top-level wrapper confirmed (no inner Cards) |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` | Step 5 — fragment top-level and inner structure confirmed |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx` | Step 6 — fragment top-level and inner structure confirmed |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx` | Step 7 — top-level wrapper and four inner Cards confirmed |
| `web/src/components/ui/card.tsx` | shadcn Card primitive — exact Tailwind tokens confirmed |
| `web/src/app/(authenticated)/layout.tsx` | Page background — `bg-slate-50 dark:bg-slate-950` confirmed, shell visible |
| `web/src/app/(authenticated)/admin/layout.tsx` | Admin layout — bare `<div>{children}</div>`, no interfering background |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx` | Step 1 page shell — WizardLayout usage confirmed |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/parse/page.tsx` | Step 2 page shell — WizardLayout usage confirmed |
| `BACKLOG.md` | Confirmed §25 is the relevant backlog entry; Chunk 6 adds a sub-item |

---

## 8. Report Written

`docs/25/chunk-6-recon.md`

---

## 9. Out of Scope Items Flagged

| Item | Reason out of scope |
|------|---------------------|
| Page-level background colour (`bg-slate-50` uses hard-coded slate, not a theme token) | The `bg-card` shell is visually distinct against `bg-slate-50` — no change needed to make the shell visible. The hard-coded slate in the authenticated layout is a pre-existing convention unrelated to Chunk 6. |
| `flex-1 min-h-0` removal correctness | Dropping the flex-fill div is low-risk in the page-per-step architecture, but if visual regression appears (e.g. content collapsing in a flex context), restoring it as `<Card className="flex-1 min-h-0">` is a one-word fix. Flag for implementation to verify. |
| Inner Card shadow de-escalation (removing `shadow-sm` from inner Cards to reduce nesting depth) | Not needed to ship Chunk 6. If the nested depth reads as visually heavy in practice, raise as a follow-up after Chunk 6 lands. |
| Mobile breakpoint treatment for the shell | The shell is a full-width container with padding; `px-6` inside CardContent may be tight on very small viewports. Out of scope per §25 recon (mobile audit is §7.2 not §25). Flag if UAT feedback surfaces this. |
| The `pt-6` double-top-padding (48px total top spacing) | Matches the legacy wizard exactly. If it reads as excessive in practice, adjusting to `pt-0` on CardContent (relying solely on Card's `py-6`) is a one-class change — separate concern from the shell's existence. |
| Legacy wizard `WizardSectionDivider` dark mode patch | Already applied in Chunk 1. No further action. |
| BACKLOG §25 status update | Must happen in the implementation report, not this recon. |
