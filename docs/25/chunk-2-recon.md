# §25 Chunk 2 — ParseStep + QuizStep Visual Polish: Implementation Recon

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code (read-only investigation — no code changed)_

---

## 1. Verification of current state (design recon drift check)

| Recon spec item | File:line | Maps cleanly? | Notes |
|---|---|---|---|
| ParseStep has no Card wrap on section list | `ParseStep.tsx:247–296` | ✓ | `<div className="space-y-6">` — no Card |
| ParseStep has no Re-parse button in ready state | `ParseStep.tsx:249–256` | ✓ | Right side of flex header is empty |
| ParseStep empty state uses thin `rounded-full border p-3` icon circle | `ParseStep.tsx:222` | ✓ | `border` only, no fill — upgrade target confirmed |
| QuizStep question list has no Card wrap | `QuizStep.tsx:308–337` | ✓ | Bare `<div className="space-y-6">` |
| QuizStep "Regenerate All" button is model for ParseStep Re-parse | `QuizStep.tsx:318–329` | ✓ | Button already present, `variant="outline" size="sm"`, calls `handleGenerateQuiz` |
| QuizSettingsPanel renders as a plain form block | `QuizStep.tsx:347–352` | ✓ | Naked `{talk && <QuizSettingsPanel ...>}` with no wrapper or divider above it |

No drift from design recon. Chunk 1's shipped state has no knock-on changes to ParseStep or QuizStep.

---

## 2. Spec gap resolutions

### 2.1 Re-parse button wiring

Three questions from the recon brief:

**Does ParseStep have access to a parse mutation?** Yes. `useParseTalk(talkId)` is already imported (`ParseStep.tsx:11`) and instantiated as `parseMutation` (`ParseStep.tsx:49`).

**How does the Re-parse button call the parse action?** `handleParse` at `ParseStep.tsx:93–102` is a fully wired callback. It resets `initializedRef`, clears form state, and calls `parseMutation.mutateAsync()`. The Re-parse button simply calls `handleParse` — no new mutation, no backend change, no navigation back to Step 1.

**Confirmation dialog?** The design recon does not mention one. Re-parsing does clear `form.reset({ sections: [] })` (line 97), so unsaved edits are lost. The scope spec says "add a Re-parse button" with no mention of a dialog. Recommendation: no dialog in this chunk; note as a low-priority out-of-scope concern (§4).

### 2.2 Empty state visual upgrade

**Where does the empty state render?** Both ParseStep and QuizStep have **separate inline JSX** empty states:
- ParseStep: `ParseStep.tsx:219–241`
- QuizStep: `QuizStep.tsx:278–301`

Neither is a shared component. Changes must be applied to both files separately.

**Current state:** Both use identical pattern:
```tsx
<div className="rounded-full border p-3 text-muted-foreground">
  <Wand2 className="h-6 w-6" />
</div>
```
Container is `py-16` with `flex flex-col items-center justify-center gap-4 text-center`.

**Target:** Replace `border p-3` with `bg-primary/10 p-4` (fill the circle, slightly more padding). Change outer `py-16` to `py-20`. No structural change to the copy or button below.

### 2.3 QuizSettingsPanel section divider placement — h3 conflict

**Does QuizSettingsPanel render its own title?** Yes. `QuizSettingsPanel.tsx:63`:
```tsx
<h3 className="text-sm font-semibold">Quiz Settings</h3>
```
It sits inside `<div className="border rounded-lg p-4 space-y-5 bg-card">`.

**Where in QuizStep is the panel rendered?** `QuizStep.tsx:347–352`, directly in the outer `<div className="space-y-6">`, between the `questionsError` block and the Save button. No wrapper, no divider.

**Resolution:** Adding `<WizardSectionDivider number="2a" label="Quiz Settings" />` above the panel makes the h3 redundant (both say "Quiz Settings"). The cleanest fix: add a `showTitle?: boolean` prop to `QuizSettingsPanel` (default `true`), and QuizStep passes `showTitle={false}`. This is a 3-line change to QuizSettingsPanel — it's in the design recon's "Files changed" list as an expected touch. The `border rounded-lg p-4 bg-card` container stays; only the h3 is conditionally hidden.

### 2.4 Card wrap on SectionList — confirmed no conflict

**Outer container:** `SectionList.tsx:355` — `<div className="space-y-2">`. No border, no margin, no hard-coded padding.

**`className` prop?** No — `SectionListProps` (`SectionList.tsx:45–49`) exposes only `sections`, `onChange`, `disabled`. But since Card wrapping is done from ParseStep's `<CardContent>` — not by passing className into SectionList — this is irrelevant. Card's `py-6` + `CardContent`'s `px-6` provides the outer spacing; SectionList's `space-y-2` stacks the item cards inside. Clean, no conflict. The design recon's risk note ("SectionList takes className props") was incorrect in its premise but correct in its mitigation.

### 2.5 SectionQuestionGroup — SPEC GAP: hard-coded container conflicts with Card wrap

**This is the most material gap.** `SectionQuestionGroup.tsx:28`:
```tsx
<div className="border rounded-lg overflow-hidden">
```
The component has its own `border rounded-lg` outer container AND its own collapsible "Quiz Questions (N)" header at `SectionQuestionGroup.tsx:30–47`.

Wrapping it in a shadcn `<Card>` would create:
- **Double border**: Card's `border rounded-xl` wrapping SectionQuestionGroup's `border rounded-lg`
- **Duplicate heading**: CardTitle "Quiz Questions" + SectionQuestionGroup's internal "Quiz Questions (N)" button

**Recommendation:** Do not use the shadcn `<Card>` component for the question list. Instead, use a plain wrapper div that matches Card's visual weight without the component's structural primitives:

```tsx
<div className="rounded-xl border shadow-sm bg-card overflow-hidden">
  {/* header bar — matches Card's px-6 py-4 feel */}
  <div className="flex items-center justify-between px-6 py-4 border-b">
    <div>
      <p className="font-semibold leading-none">Quiz Questions</p>
      <p className="text-sm text-muted-foreground mt-1">
        {questions.length} question{questions.length !== 1 ? 's' : ''} — edit, reorder, or delete before continuing.
      </p>
    </div>
    <Button variant="outline" size="sm" ...>Regenerate All</Button>
  </div>
  {/* SectionQuestionGroup fills the card body — p-0 so its overflow-hidden applies */}
  <div className="p-0">
    <SectionQuestionGroup ... />
  </div>
</div>
```

This removes the existing outer `<div className="flex items-center justify-between">` header (lines 309–329) and the naked SectionQuestionGroup render, replacing both with the wrapper above. SectionQuestionGroup's internal border and header sit flush inside, no double border. **No changes to SectionQuestionGroup.tsx.**

**ParseStep can still use the shadcn Card** (SectionList has no conflicting container).

---

## 3. Sized implementation chunk

### ParseStep — 3 changes

**a. Card wrap + Re-parse button (replaces lines 247–296 ready-state block)**

- Wrap `<SectionList>` in shadcn Card with CardHeader/CardTitle/CardDescription/CardAction/CardContent
- CardTitle: "Sections"
- CardDescription: `{sections.length} section{...} — reorder, edit, or delete before continuing.`
- CardAction: `<Button variant="outline" size="sm" onClick={handleParse} disabled={isParsing || isSaving}>Re-parse</Button>` (add `RefreshCw` or `Wand2` icon)
- CardContent contains `<SectionList>` and the sectionsError paragraph
- Save & Continue button stays outside the Card in the outer `<div className="space-y-4">` (was `space-y-6`)
- New imports: `Card, CardHeader, CardTitle, CardDescription, CardAction, CardContent` from `@/components/ui/card`

**b. Empty state visual upgrade (lines 219–241)**

- `py-16` → `py-20` on outer container
- `<div className="rounded-full border p-3 text-muted-foreground">` → `<div className="rounded-full bg-primary/10 p-4 text-primary">`

**c. Convention confirmation:** Follow Chunk 1's pattern — no `<section aria-labelledby>` wrapper, direct `WizardSectionDivider` rendering (not used in ParseStep, but confirm the empty-state card button wording matches convention).

---

### QuizStep — 3 changes

**a. Question list "Card-lite" wrap (replaces lines 307–337)**

- Replace outer `<div className="flex items-center justify-between">` header (lines 309–329) + raw `<SectionQuestionGroup>` (lines 331–337)
- Use plain wrapper div: `<div className="rounded-xl border shadow-sm bg-card overflow-hidden">`
  - Header bar: `<div className="flex items-center justify-between px-6 py-4 border-b">` with title text left and Regenerate All button right
  - Question content: `<SectionQuestionGroup>` fills below the header bar, no extra padding
- questionsError paragraph (`<p role="alert">`) moves outside the wrapper (stays in outer `space-y-6` after the card)
- New import: none (Button already imported; no Card imports needed for this approach)

**b. WizardSectionDivider above QuizSettingsPanel (QuizStep lines 347–352)**

- Insert `<WizardSectionDivider number="2a" label="Quiz Settings" />` between questionsError and `{talk && <QuizSettingsPanel ...>}`
- Add `showTitle={false}` to the `<QuizSettingsPanel>` call
- New import: `WizardSectionDivider` from `@/components/ui/wizard-section-divider`

**c. Empty state visual upgrade (lines 278–301)**

- Same changes as ParseStep 2.2 above

---

### QuizSettingsPanel — 1 change

- Add optional `showTitle?: boolean` prop (default `true`)
- Wrap `<h3 className="text-sm font-semibold">Quiz Settings</h3>` (line 63) in `{showTitle !== false && ...}`

---

### Files affected

| File | Change type |
|------|------------|
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | Card wrap + Re-parse button + empty state upgrade |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx` | Card-lite wrap + WizardSectionDivider + empty state upgrade |
| `web/src/features/toolbox-talks/components/learning-wizard/components/QuizSettingsPanel.tsx` | `showTitle` prop |

**Not changed:** `SectionList.tsx`, `SectionQuestionGroup.tsx`, `wizard-section-divider.tsx`, `card.tsx`.

**Estimated diff size:** ~55–70 lines net across 3 files (Chunk 1 was ~200 lines). Chunk 2 is roughly one-third the scope.

---

## 4. Out of scope concerns

| Item | Reason |
|------|--------|
| Re-parse confirmation dialog | Recon spec doesn't mention one. Calling `handleParse` directly drops unsaved section edits without warning. Low-urgency UX gap — post-Chunk 2 backlog candidate if users report confusion. |
| SectionQuestionGroup has duplicate "Quiz Questions" heading relative to QuizStep's outer header today | Pre-existing before Chunk 2. The Card-lite approach in §3 removes the outer header div, collapsing to one heading location (SectionQuestionGroup's internal header). Net improvement. |
| `SectionList`'s `SectionCard` items each render `rounded-lg border bg-card` — inside a Card they become nested card-within-card visually | Acceptable — common list-in-card pattern; SectionList items are individually bordered row-items, not semantic sub-cards. Not a regression. |
| SectionQuestionGroup has no `className` or `noWrapper` prop | Card-lite approach avoids the need. If SectionQuestionGroup ever needs a Card-native wrap, it would require its own structural refactor — Chunk 5 or later. |
| `QuizStep`'s "Regenerate All" button today is disabled while `isGenerating` but not while `isSaving` | Pre-existing; not introduced by Chunk 2. Out of scope. |
| No test files for ParseStep or QuizStep | Confirmed by glob — no `*.test.tsx` or `*.spec.tsx` in `learning-wizard/steps/`. Chunk 2's changes are purely structural/visual; no new logic paths that require unit tests. |

---

## 5. Files read

| File | Purpose |
|------|---------|
| `docs/25/recon.md` | Design recon — authoritative spec |
| `docs/25/chunk-1-fix.md` | Chunk 1 implementation report — convention carry-forward |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | Subject — full read |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx` | Subject — full read |
| `web/src/features/toolbox-talks/components/learning-wizard/components/QuizSettingsPanel.tsx` | Subject — full read |
| `web/src/features/toolbox-talks/components/learning-wizard/components/SectionList.tsx` | Shared component — Card wrap compatibility check |
| `web/src/features/toolbox-talks/components/learning-wizard/components/SectionQuestionGroup.tsx` | Shared component — Card wrap compatibility check (lines 1–60) |
| `web/src/components/ui/card.tsx` | shadcn Card primitives — CardAction slot confirmed |
| `web/src/components/ui/wizard-section-divider.tsx` | Dark mode patch confirmed in place |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx:340–398` | Switch + bordered tile pattern reference |

---

## 6. Report written

`docs/25/chunk-2-recon.md`
