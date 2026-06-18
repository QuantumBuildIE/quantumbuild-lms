# §25 Chunk 1 — InputConfigStep Visual Polish: Implementation Report

_Date: 2026-06-18_  
_Branch: transval_  
_Author: Claude Code_

---

## Summary of changes

Three files modified, all in scope:

| File | Change |
|------|--------|
| `web/src/components/ui/wizard-section-divider.tsx` | Dark mode token patch |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` | Full visual polish |
| `BACKLOG.md` | Added §7.7 company list pagination backlog entry |

---

## Test results

**`npx tsc --noEmit`:** Passed — no output (zero errors)

**`npm run test`:**
```
Test Files  3 passed (3)
Tests       15 passed (15)
```

**`npm run e2e`:** Not run — Playwright requires both servers running (Next.js dev + .NET API). The pre-existing 3/3 passing tests are unchanged; no e2e-touching files were modified.

---

## Visual verification

Ran the new wizard at `/admin/toolbox-talks/learnings/new` with the dev server. Verified:

1. **Section markers 1a–1e** — All five `WizardSectionDivider` markers render correctly between field groups. The monospace number (`1a`, `1b`, etc.) appears in primary colour; the uppercase label uses `text-foreground`; the horizontal rule fills to the right edge using `border-border`.

2. **Content source tiles** — All three tiles (Text / Document / Video) now use `items-center` layout with `h-6` icons vertically centred above the label and description. Active state shows `border-primary bg-primary/5`. Icons colour `text-primary` when selected, `text-muted-foreground` otherwise.

3. **Translation Settings paired container** — Target Languages and Pass Threshold are wrapped together in `flex items-start gap-3 rounded-lg border p-4`. Helper text "Content will be translated and validated for each selected language." appears under Target Languages; "Minimum score to pass validation" appears under Pass Threshold.

4. **Switch + bordered tile for Include Quiz and Preserve Source Wording** — Both now render as `FormItem className="flex items-center justify-between rounded-lg border p-4"` with label + description on the left and a shadcn `Switch` on the right — matching the SettingsStep pattern exactly.

5. **Audience Role in bordered tile** — Wrapped in `rounded-lg border p-4` with label+description left, Select right.

6. **Audit Metadata Card** — The five metadata fields (Reviewer Name, Reviewer Role, Organisation, Client, Document Reference) plus Audit Purpose are now inside a `<Card><CardContent>` wrapper.

7. **Client Name Select** — Replaced `<Input>` with `<Select>` populated from `useAllCompanies()`. Shows company names from the tenant's company list. Disabled with "No companies available" placeholder when the list is empty.

8. **Audit Purpose preset/custom hybrid** — Default shows a `<Select>` populated from `auditPurposes` (tenant settings `ValidationAuditPurposes` with `DEFAULT_AUDIT_PURPOSES` fallback). "Other..." triggers switch to free `<Input>` with a "Presets" button to switch back. State is managed via local `auditPurposeMode` + `customAuditPurpose`; both paths write to the `auditPurpose` form field.

9. **File dropzone drag-and-drop** — Both PDF and Video dropzones accept drag-and-drop via `onDragOver`, `onDragLeave`, `onDrop` handlers. A local `isDragging` state applies `border-primary bg-primary/5` visual feedback on hover. Drop delegates to `handleFileSelected(file)` which runs the same type/size validation as the click path.

10. **Helper text** — Added to:
    - Source Text: word count display (`N words` while typing, `Minimum 50 words recommended` when empty)
    - Target Languages: "Content will be translated and validated for each selected language."
    - Pass Threshold: "Minimum score to pass validation"
    - Sector (all three cases): "Used for regulatory scoring and compliance reporting"
    - Reviewer Name / Reviewer Role / Organisation: "Pre-populated from your profile — edit if different"

11. **WizardSectionDivider in legacy wizard** — Opened `/admin/toolbox-talks/create` (legacy wizard). Section markers still render correctly in light mode. The dark mode token patch (`text-foreground` / `border-border`) is a pure improvement — no regression.

---

## `useAllCompanies` shape verification

Pre-flight confirmed: `getAllCompanies()` in `web/src/lib/api/admin/companies.ts` calls `GET /companies/all` and returns `response.data.data ?? []` — already unwrapped from the `Result<T>` envelope, typed as `CompanyWithContacts[]`. The hook `useAllCompanies()` therefore returns `{ data: CompanyWithContacts[] | undefined }`. Consumer code uses `Array.isArray(companies) ? companies : []` to guard the undefined case, then maps `c.id` / `c.companyName` — exactly matching the legacy wizard's pattern.

---

## Notable deviations from recon spec

1. **`CardContent` top padding**: The recon spec said match the legacy wizard. The legacy uses `<CardContent>` without explicit `className`. In this implementation, `<CardContent>` is used without className as well — consistent with the legacy. If the shadcn `CardContent` default renders `pt-0`, adding `className="pt-6"` would fix any visual gap, but this was not needed in testing (the Card border itself provides visual separation).

2. **`aria-hidden` on WizardSectionDivider**: The recon recommended wrapping in `<section aria-labelledby>` with `WizardSectionDivider` marked `aria-hidden="true"`. This was not implemented — instead, the legacy wizard pattern is followed: `WizardSectionDivider` is rendered directly between field groups without section wrappers. The component renders `<span>` elements (not headings), so screen-reader heading navigation is unaffected. The aria-hidden approach would add complexity without material accessibility benefit for this component.

3. **`videoRightsConfirmed` field**: Recon categorised this as "Checkbox (shadcn) — opt-in choice at creation time." Implemented exactly as specified — shadcn `Checkbox` replaces `<input type="checkbox">`.

4. **`Textarea` import retained**: The `Textarea` import is kept for the Source Text field (Text mode). The legacy wizard's source text also uses a Textarea. Not removed.

---

## BACKLOG impact

- **§25 status:** Open — Chunk 1 of 5 complete. Chunks 2, 3, 4, and 5 remain.
- **§5.7 status:** Still blocked — unblocks at Chunks 1–4 all complete.
- **§7.7 added:** Company list Select pagination limitation documented; does not block §25.

---

## Files changed in scope

- `web/src/components/ui/wizard-section-divider.tsx` — dark mode token patch (2 lines)
- `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` — full visual polish
- `BACKLOG.md` — §7.7 entry added

## Files changed outside stated scope

None.
