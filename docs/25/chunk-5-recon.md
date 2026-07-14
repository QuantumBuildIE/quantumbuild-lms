# §25 Chunk 5 — §24 Detail Panels: Recon Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code (read-only investigation — no code changed)_

---

## 1. Pre-flight Checks

### Files confirmed present

All four panel files exist at the expected paths — no renames since the §25 design recon was written:

- `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx` ✓
- `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx` ✓
- `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx` ✓
- `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx` ✓

### Host page unchanged

`web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` confirmed. Panels render in the "Overview" tab as `SettingsEditPanel → SectionEditPanel → QuizEditPanel` (in that order). `AddTargetLanguagePicker` renders at the top of the "Translations" tab, above the reused `TranslateStep`. No structural changes since Chunk 4 shipped.

### WizardSectionDivider dark mode patch

Confirmed applied (Chunk 1). `text-foreground` / `border-border` in place. No action in Chunk 5.

---

## 2. Per-Panel Verification

### 2.1 SectionEditPanel — PASS

File: `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx`

**Structural inventory:**
- ✓ Full Card: `<Card>` → `<CardHeader>` → `<CardTitle>` + `<CardDescription>` → `<CardContent>`
- ✓ `CardDescription`: "Content sections for this learning"
- ✓ `CardTitle` has icon: `<FileTextIcon className="h-5 w-5" />`
- ✓ Edit button only shown when `canManage && !isEditMode`
- ✓ Edit mode: delegates to `<SectionList>` (production-quality wizard component)
- ✓ View mode: Accordion per section — number badge, title, acknowledgment badge, content in `rounded-lg bg-muted/50 p-4`
- ✓ Dirty-state guard: `sectionsEqual` comparison, `AlertDialog` for discard confirmation
- ✓ Save disabled when `!isDirty` or `updateMutation.isPending`

**Parity assessment against the four structural patterns:**
| Pattern | Status | Notes |
|---------|--------|-------|
| Card grouping | ✓ | Full Card with header + description |
| Bordered tiles | N/A | Content panel; accordion items use muted bg, not bordered tiles |
| Helper text | ✓ | CardDescription covers top-level; SectionList has its own per-field help |
| Switch for booleans | N/A | The `requiresAcknowledgment` toggle is inside SectionList — not this component's concern |

**Verdict: PASS — no work needed.** Confirmed at parity as the original recon stated.

---

### 2.2 QuizEditPanel — PASS

File: `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx`

**Structural inventory:**
- ✓ Full Card: `<Card>` → `<CardHeader>` → `<CardTitle>` + `<CardDescription>` → `<CardContent>`
- ✓ `CardDescription`: "Passing score: {talk.passingScore}%" — data-derived, relevant
- ✓ `CardTitle` has icon: `<HelpCircleIcon className="h-5 w-5" />`
- ✓ Disabled-quiz state: separate early-return Card with informational message ("Quiz is disabled for this learning. Enable it in Settings to add questions.")
- ✓ Edit mode: delegates to `<SectionQuestionGroup>` with `onSaveQuestion`, `onDeleteQuestion`, `onAddQuestion` handlers
- ✓ View mode: each question tile uses `rounded-lg border p-4` — the bordered tile pattern
  - Question number badge (`Q{n}`)
  - Question text as `font-medium`
  - Type + points as `text-sm text-muted-foreground`
  - Options with lettered labels (A/B/C/D); correct answer highlighted `text-green-600 font-medium` + `CheckCircle2Icon`
  - Short answer shows expected answer in green
- ✓ Dirty-state guard: `questionsEqual`, `AlertDialog` for discard confirmation
- ✓ Add Question handler wires to `handleAddQuestion` → new blank question appended

**Parity assessment:**
| Pattern | Status | Notes |
|---------|--------|-------|
| Card grouping | ✓ | Full Card with header + description |
| Bordered tiles | ✓ | View-mode question rows use `rounded-lg border p-4` |
| Helper text | ✓ | CardDescription shows passing score; question type/points visible as muted text |
| Switch for booleans | N/A | No boolean settings in this panel |

**Verdict: PASS — no work needed.**

---

### 2.3 SettingsEditPanel — PARTIAL

File: `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`

**Structural inventory:**
- ✓ Full Card: `<Card>` → `<CardHeader>` → `<CardTitle>` → `<CardContent>`
- ✓ Uses `Switch` via `ToggleRow` sub-component defined in the same file
- ✓ `ToggleRow` uses `rounded-lg border p-3` with label + description on left, `Switch` on right — the bordered tile pattern
- ✓ Form: React Hook Form + Zod (`settingsEditSchema`)
- ✓ Dirty-state guard: `form.formState.isDirty`, `AlertDialog` for discard
- ✓ Conditional reveal: `requiresQuiz` controls display of quiz sub-options; `requiresRefresher` controls interval field
- ✓ View mode: `ViewRow` sub-component with muted label + semibold value, laid out in `grid gap-3 sm:grid-cols-2`
- ✓ View-mode section headers: `text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3`

**Gaps found:**

#### Gap A — Missing `CardDescription` in CardHeader

`SectionEditPanel` and `QuizEditPanel` both have `<CardDescription>` in their headers. `SettingsEditPanel.CardHeader` has only `<CardTitle>` — no description. Minor inconsistency at the panel level.

```tsx
// Current (line 234–237):
<CardTitle className="flex items-center gap-2">
  <SlidersHorizontalIcon className="h-5 w-5" />
  Settings
</CardTitle>
// Missing: <CardDescription>…</CardDescription>
```

#### Gap B — Edit-mode section headers inconsistent with view-mode

View mode (lines 502–504, 540–542, 558–560, 572–574) uses the clean uppercase-tracking style:
```tsx
<h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">
  Quiz
</h3>
```

Edit mode (lines 279, 402, 451, 472) uses a plainer variant:
```tsx
<h3 className="text-sm font-semibold mb-3">Quiz</h3>
```

Two different section-header styles for the same sections in the same component. The view-mode style is more polished and matches the established convention for read-only section labels in detail views.

#### Gap C — Numeric inputs lack suffix labels (low priority)

Three numeric fields use `className="w-24"` without a unit suffix label:
- `passingScore`: FormLabel "Passing score (%)" includes the unit in the label — acceptable
- `refresherIntervalMonths`: "Refresher interval (months)" — acceptable
- `autoAssignDueDays`: "Auto-assign due within (days)" — acceptable

This is the same `w-24` orphan issue flagged in Chunk 3's SettingsStep, but the labels here already contain the unit strings in parentheses, so the urgency is lower. No `FormDescription` helper text provided, but the labels are clear enough.

**Parity assessment:**
| Pattern | Status | Notes |
|---------|--------|-------|
| Card grouping | ✓ | Full Card — missing CardDescription only |
| Bordered tiles | ✓ | ToggleRow uses `rounded-lg border p-3` |
| Helper text | ✓ (partial) | Toggle descriptions are good; numeric fields rely on label-only |
| Switch for booleans | ✓ | All boolean fields use Switch via ToggleRow |

**Verdict: PARTIAL — two targeted fixes needed (Gaps A and B). Gap C is low priority / skip.**

---

### 2.4 AddTargetLanguagePicker — PASS

File: `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx`

**Structural inventory:**
- Inline widget — no Card wrapper. Correct: it sits inside the Translations tab, above `TranslateStep`; a Card would add visual weight where none is warranted.
- Layout: `flex flex-wrap items-end gap-3` — adapts to narrow containers
- `Label` with `htmlFor` linking to `SelectTrigger id="add-language-picker"` — correct accessibility
- `Select` with `SelectValue` placeholder, sorted `SelectItem` list, empty-state message in SelectContent
- `Button` with `PlusIcon`, disabled when no selection or mutation pending
- ✓ Permission gate: `if (!canManage) return null`
- ✓ Language deduplication: filters `existingLanguages` before rendering the Select options
- ✓ Error handling: toast on failure

**Gap assessment:** None. The widget is appropriately minimal for its role as a compact inline control.

**Verdict: PASS — no work needed.**

---

## 3. Convention Carry-Forward Confirmation

| Convention | Applies? | Status |
|-----------|----------|--------|
| `WizardSectionDivider` for section markers | No — detail panels use `<h3>` section headers; WizardSectionDivider is wizard-specific | N/A |
| Bordered tile rows (`rounded-lg border p-4`) | Yes — QuizEditPanel question tiles ✓; SettingsEditPanel ToggleRow (`p-3`) ✓ | Carried ✓ |
| Card groupings | Yes — all three edit panels use full Card | Carried ✓ |
| Helper text (`text-xs text-muted-foreground`) | Yes — ToggleRow descriptions, CardDescriptions, ViewRow labels | Carried ✓ |
| `Switch` for boolean fields | Yes — SettingsEditPanel ToggleRow | Carried ✓ |
| `Checkbox` for opt-in fields | N/A — no opt-in checkboxes in these panels | N/A |
| `p-3` vs `p-4` for ToggleRow padding | SettingsEditPanel uses `p-3`, SettingsStep wizard uses `p-4` | Minor; not correcting — detail panel vs wizard context makes the difference acceptable |
| `CardDescription` on CardHeader | SectionEditPanel ✓, QuizEditPanel ✓, SettingsEditPanel ✗ (Gap A) | Fix in Chunk 5 |

---

## 4. Spec Gap Resolutions

### 4.1 SectionEditPanel confirmed at parity

The original recon flagged `SectionEditPanel` as "confirmed at parity from code read." This recon confirms that finding — full Card, CardDescription, accordion view, SectionList delegation, dirty-state guard all present. No action.

### 4.2 QuizEditPanel — better than expected

The recon flagged QuizEditPanel as "Unknown — not read." After reading: it's structurally solid. The view-mode question tiles use the bordered tile pattern, the edit mode delegates cleanly to `SectionQuestionGroup`, and the disabled-quiz state is handled gracefully.

### 4.3 SettingsEditPanel — two cosmetic fixes needed

Both gaps are small and cosmetic. Neither represents a broken feature, missing validation, or inaccessible UI — they're visual consistency issues only.

**Gap A fix** (`CardDescription`): One line:
```tsx
<CardDescription>Quiz, refresher, and certificate settings</CardDescription>
```

**Gap B fix** (edit-mode section headers): Four identical substitutions — change `text-sm font-semibold` to `text-xs font-semibold uppercase tracking-wide text-muted-foreground` on the four `<h3>` tags in edit mode (lines 279, 402, 451, 472).

### 4.4 AddTargetLanguagePicker — no Card needed

The widget is a compact inline control for the Translations tab. Its parent tab content (`ToolboxTalkDetail.tsx` line 368–383) provides the structural context. A Card wrapper would be over-engineering.

---

## 5. Sized Implementation Chunk

**Chunk 5 is lighter than the 1–1.5 day envelope.** Three of the four panels are at parity. Only `SettingsEditPanel` needs work.

**Estimated diff: ~6 lines changed. Actual implementation time: under 0.5 day.**

### File: `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`

**Change 1 — Add `CardDescription` (line ~237, after the closing `</CardTitle>` tag):**
```tsx
<CardDescription>Quiz, refresher, and certificate settings</CardDescription>
```

**Change 2 — Unify edit-mode section headers (4 locations):**

Replace each of the four edit-mode `<h3>` elements:
```tsx
// Before (4 instances — lines ~279, ~402, ~451, ~472):
<h3 className="text-sm font-semibold mb-3">Quiz</h3>
<h3 className="text-sm font-semibold mb-3">Refresher</h3>
<h3 className="text-sm font-semibold mb-3">Certificate</h3>
<h3 className="text-sm font-semibold mb-3">Schedule</h3>

// After (matching view-mode style):
<h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">Quiz</h3>
<h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">Refresher</h3>
<h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">Certificate</h3>
<h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">Schedule</h3>
```

**No other files changed.** TypeScript check and existing tests cover this — no test additions needed for cosmetic class changes.

---

## 6. Files Read

| File | Purpose |
|------|---------|
| `docs/25/recon.md` | §25 design recon — Chunk 5 spec reference |
| `docs/25/chunk-4-recon.md` | Convention reference and confirmation of Chunk 4 work |
| `docs/25/chunk-4-fix.md` | Confirmation Chunks 1–4 complete; pre-flight baseline |
| `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx` | Panel 1 — primary subject |
| `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx` | Panel 2 — primary subject |
| `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx` | Panel 3 — primary subject |
| `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx` | Panel 4 — primary subject |
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Host component — panel composition and tab context |
| `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/page.tsx` | Page shell — confirms routing and `ToolboxTalkDetail` usage |

---

## 7. Report Written

`docs/25/chunk-5-recon.md`

---

## 8. Out-of-Scope Items Flagged

1. **`ToolboxTalkDetail` "Talk Details" card (Overview tab, line 247–325)** — Uses `<label className="text-sm font-medium text-muted-foreground">` (not `<Label>` from shadcn) and bare `<div>` wrappers rather than a `ViewRow`-style sub-component. Minor inconsistency with the `ViewRow` pattern in `SettingsEditPanel`. Not §25 scope — the Detail card is not one of the four named Chunk 5 panels, and the inconsistency predates §24.

2. **`ToggleRow` padding `p-3` vs SettingsStep wizard `p-4`** — `SettingsEditPanel`'s `ToggleRow` uses `p-3`. SettingsStep wizard tiles use `p-4`. Both are functionally correct bordered tiles; the 1px difference is acceptable between a detail panel and a full wizard step. Not correcting — would require updating `ToolboxTalkDetail`'s visual density to match wizard scale, which is a separate judgment call.

3. **"Recent Completions" placeholder card** — `ToolboxTalkDetail.tsx:328–340` renders a card that says "View completions in the Assignments tab" with an 8-unit padding placeholder. This is a deferred feature stub, not a polish gap. Out of §25 scope.

4. **`minimumVideoWatchPercent` not editable in `SettingsEditPanel`** — The panel's schema (`settingsEditSchema`) does not include `minimumVideoWatchPercent` or `isActive` or `autoAssignToNewEmployees` — these are passed through from `talk.*` unchanged in `onSubmit`. A user who wants to change the video watch percentage must use the legacy edit page or the wizard. Not a §25 polish gap; this is a functional scope decision for §24.
