# Wizard Toggle Retirement — Recon Report

**Date:** 2026-07-09
**Scope:** Read-only investigation. No code changes.
**Purpose:** Determine the fix shape for hiding the `UseNewWizard` toggle UI and flipping its default to "new wizard", per product decision to retire the legacy wizard as a supported (but not deleted) path.

---

## Headline

**Retirement with caveats — 2 file changes for the literal ask, but the "new wizard is exclusive" framing does not hold for two real corners of the product: Courses (no new-wizard flow exists) and a dormant Edit-button regression on the talk detail page. Both are pre-existing conditions the fix should account for, not new risks the fix introduces.**

No migration is required — the field has no DB-level default or column; it is a sparse key-value row (`TenantSettings` table) that is only ever created when a tenant explicitly saves a value. Locally, **0 of 7 tenants** have ever done so, so every tenant today rides the in-code default. Flipping that one line is instant and safe for all of them. The complexity in this recon is entirely in the read-side behavior, not the storage.

---

## Part 1 — Field storage findings

### What carries it

There is **no entity field or DB column** for `UseNewWizard`. It is a row in the generic `TenantSettings` key-value table:

```
Table "public.TenantSettings"
  Column   |           Type           | Nullable | Default
-----------+--------------------------+----------+---------
 Id        | uuid                     | not null |
 TenantId  | uuid                     | not null |
 Module    | character varying(100)   | not null |
 Key       | character varying(200)   | not null |
 Value     | character varying(2000)  | not null |
 ...
Indexes: UNIQUE ("TenantId", "Module", "Key")
```

- **Key:** `TenantSettingKeys.UseNewWizard = "UseNewWizard"` — `src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingKeys.cs:13`
- **Value:** string `"true"` / `"false"`, compared via `=== 'true'` on the frontend
- **DB-level default:** none — `Value` is `NOT NULL` with no column default; a row simply doesn't exist until `SetSettingAsync` is called (i.e. until an admin toggles the Switch in Settings → General, or someone calls the API directly)
- **Entity-level / application default:** `TenantSettingsService.GetAllSettingsAsync()` seeds a dictionary with `[TenantSettingKeys.UseNewWizard] = "false"` (`TenantSettingsService.cs:56`) before overlaying any saved rows. This is the **only** place a default exists.
- **Factory / seed code:** none. `DataSeeder.cs` does not seed any `TenantSettings` rows, and `TenantsController` / the tenant-creation flow does not write a `UseNewWizard` row either (confirmed by grep — the only `TenantSettings` write site anywhere in the backend besides the Settings UI's own PUT endpoint is `EmployeeService.cs`, and that's for the unrelated `QrLocationTrainingEnabled` key). **New tenants created after any fix ships will also rely on the in-code default** — there is no seeding path that needs separate handling, but it does mean the in-code default is not optional even if a backfill migration is also run (see Part 4).

### Local Dev DB query (`rascor_stock` @ 127.0.0.1:5432)

Railway CLI is unauthenticated this session (`invalid_grant: grant request is invalid` on token refresh — matches the account-scoping issue noted in BACKLOG). Development/Production Railway databases were **not reachable**; all counts below are from the local Dev DB only.

```
SELECT t."Id", t."Name", ts."Value" AS "UseNewWizardValue"
FROM "Tenants" t
LEFT JOIN "TenantSettings" ts ON ts."TenantId" = t."Id" AND ts."Key" = 'UseNewWizard'
```

| Tenant | UseNewWizard row |
|---|---|
| E2E Tenant 1781963764736 | *(no row)* |
| E2E Tenant 1781963767555 | *(no row)* |
| E2E Tenant 1781964094748 | *(no row)* |
| E2E Tenant 1781964097229 | *(no row)* |
| E2E Tenant 1782138856198 | *(no row)* |
| E2E Tenant 1782138859264 | *(no row)* |
| RASCOR | *(no row)* |

**7 of 7 tenants: no row (implicit default = "false"/old). 0 explicit `true`. 0 explicit `false`.** No tenant has ever touched this setting locally.

No test/seed code was found setting `UseNewWizard=false` explicitly (checked `DataSeeder.cs`, `web/e2e/**`) — the absence of any row across all 7 tenants, including the 6 Playwright E2E tenants, confirms this.

**Caveat:** this is Dev-DB-only. The task's Part 4.8 asks about a specific trial customer — see Part 4 below; that tenant (if real) would live on Railway, not locally, and could not be checked this session.

---

## Part 2 — Read sites

All reads are **frontend-only**. The backend has zero behavior branches on this key — its only appearance server-side is the default-value dictionary entry in `TenantSettingsService.cs:56`, which is data supplied to the frontend, not a backend decision point. This matches and reconfirms the original §5.27 recon's "Step 8" finding.

| # | File : Line | Reads via | Behavior on `false`/absent | Behavior on `true` | Null-safe? |
|---|---|---|---|---|---|
| 1 | `web/src/features/toolbox-talks/hooks/useWizardPreference.ts:30` | Canonical hook — `settings?.['UseNewWizard'] === 'true'` | Returns `'old'` | Returns `'new'` (unless URL override) | Yes — optional chaining + strict `===` means `undefined`/missing key/loading state all safely resolve to `'old'` |
| 2 | `web/src/features/toolbox-talks/components/settings/wizard-toggle-section.tsx:18` | Direct read (bypasses hook) — `settings?.[SETTINGS_KEY] === 'true'` | Switch shows off | Switch shows on | Yes, same pattern |
| 3 | `web/src/features/toolbox-talks/components/ToolboxTalkList.tsx:57,358` | Via hook | "Create New" button → `${basePath}/create` (legacy wizard) | → `/admin/toolbox-talks/learnings/new` (new wizard) | Yes (inherited from hook) |
| 4 | `web/src/features/toolbox-talks/components/create-wizard/steps/PublishStep.tsx:93,226` | Via hook | "Create Another" (shown after publishing via the legacy wizard itself) → `/admin/toolbox-talks/create` | → `/admin/toolbox-talks/learnings/new` | Yes |
| 5 | `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx:64,151` | Via hook | Detail-page **"Edit" button is shown** (`canManage && wizardPreference === 'old'`) | **Edit button is hidden** | Yes, but see stale-assumption finding below |

### Stale assumption found — read site #5

`ToolboxTalkDetail.tsx:151`:
```tsx
{canManage && wizardPreference === 'old' && (
  <Button onClick={() => router.push(`${basePath}/${talk.id}/edit`)}>
    <PencilIcon .../> Edit
  </Button>
)}
```

This routes to `ToolboxTalkForm` (the full legacy edit form), which is the **only** UI surface covering `title`, `code`, `description`, `category`, `attachmentUrl`, and video/PDF upload-replace. The §24 inline edit panels rendered lower on the same page (`SettingsEditPanel`, `SectionEditPanel`, `QuizEditPanel` — `ToolboxTalkDetail.tsx:358-364`) cover quiz settings, section content, and quiz questions **only** — none of those core metadata/file fields. This gap is already known and accepted by the team: BACKLOG §27 finding A2/B8 explicitly states *"the new wizard's detail-page edit has no PDF re-upload affordance."*

**Today this is dormant** — with 0 tenants opted in, `wizardPreference` resolves `'old'` everywhere (barring a manual `?wizard=new` override), so the Edit button always renders. **The moment the default flips to `'true'` tenant-wide, this button disappears from the talk detail page for every tenant**, unless they're specifically viewing with `?wizard=old` in the URL.

**Mitigating factor — capability is not actually lost.** The identical `/edit` route is separately reachable, **ungated by `wizardPreference`**, from the Learnings list's row-level Actions menu:

```tsx
// ToolboxTalkList.tsx:257-262
{canManage && (
  <DropdownMenuItem onClick={() => router.push(`${basePath}/talks/${item.id}/edit`)}>
    <PencilIcon .../> Edit
  </DropdownMenuItem>
)}
```

So flipping the default causes a **UX inconsistency** (one entry point vanishes, a second one right next to it on the list page still works), not a hard capability loss. It's worth fixing as a small pre-flip cleanup (drop the `wizardPreference === 'old'` condition on the detail-page button so it matches the list's unconditional behavior) but it does not block the retirement.

### URL override — verified still works after the flip

`useWizardPreference.ts:26-30`:
```ts
const urlOverride = searchParams.get('wizard');
if (urlOverride === 'new') return 'new';
if (urlOverride === 'old') return 'old';
return settings?.['UseNewWizard'] === 'true' ? 'new' : 'old';
```
The URL param is checked **before** the settings fallback and returns early. Flipping the default value the fallback resolves to does not touch this branch at all — `?wizard=old` will continue to force the legacy wizard for smoke-testing after the flip, exactly as it does today for `?wizard=new` before the flip. Confirmed by direct read, no ambiguity here.

---

## Part 3 — UI surfaces

### Primary toggle

`web/src/app/(authenticated)/admin/toolbox-talks/settings/page.tsx:63-66`:
```tsx
<TabsContent value="general" className="space-y-6 pt-4">
  <WizardDefaultsSection />
  <WizardToggleSection />
</TabsContent>
```

`WizardToggleSection` is its **own self-contained `<Card>`** (`wizard-toggle-section.tsx:44-68`) — a sibling to `WizardDefaultsSection`'s card, not nested inside it or any other card/section. Hiding it is a clean single-line removal of `<WizardToggleSection />` (or a comment-out) with **zero risk of collateral-hiding** any adjacent setting. `WizardDefaultsSection` is unaffected either way.

### No other interactive surface

Grepped all of `web/src` for `UseNewWizard`, `useWizardPreference`, `WizardToggleSection`, and `useUpdateTenantSettings` usage tied to this key — `wizard-toggle-section.tsx` is the only write call site anywhere in the codebase.

### Adjacent read-only display — correctly out of scope

The Draft Learnings list shows a **"Legacy" badge** (`learnings/drafts/page.tsx:37-39`) and a step label (`stepLabel()`, lines 17-21) per draft. This is driven entirely by `draft.lastEditedStep === null` — **the per-talk discriminator, not the tenant-level `UseNewWizard` toggle**. It doesn't read the toggle at all, so it is unaffected by this fix regardless. Leave untouched, per the task's own guidance on read-only version displays.

Checked two other pages that call `useTenantSettings()` (`admin/settings/page.tsx` for email/cert-prefix fields, `admin/employees/[id]/page.tsx` for QR training display) — neither surfaces `UseNewWizard`. No dashboard/monitoring/diagnostics page displays it either.

---

## Part 4 — Existing-tenant scenario

### Is a backfill migration safe?

**Yes, if you want one — but it's optional, not required.** Because no tenant (locally) has ever written to this key, and because a backfill would only ever *insert* a row where none exists (never overwrite an explicit `false`), there is no risk of clobbering a real tenant decision — there isn't one yet.

Two viable shapes:

1. **Pure application-level default flip** (minimal): change `TenantSettingsService.cs:56` from `"false"` to `"true"`. Zero DB writes, zero migration, takes effect for every tenant with no row (100% of them today) the moment the deploy lands. This alone is sufficient and matches the task's framing of a trivial cutover.
2. **Explicit backfill + default flip** (more auditable): a one-time script/migration inserts `TenantSetting{Key='UseNewWizard', Value='true'}` for every tenant currently lacking a row, **and** the in-code default is still flipped to `"true"` (required regardless — see below, since new tenants never get a seeded row). This makes today's cutover state durable and inspectable per-tenant in the DB, immune to any future unrelated change to the fallback default.

**The in-code default flip is not optional in either case.** Tenant creation (`TenantsController.cs`, confirmed by grep) never writes a `TenantSettings` row for this key — so any tenant created *after* a backfill migration runs would still fall through to the in-code default. If that default weren't also flipped, new tenants would silently land back on the legacy wizard while all pre-existing tenants sit on `true` — an inconsistency nobody intends.

### Risks specific to this codebase (not hypothetical)

1. **Courses are permanently excluded from the toggle.** `CourseList.tsx:257` ("Create New") and `courses/new/page.tsx` (a redirect stub to `/admin/toolbox-talks/create`) are **not** wizard-preference-aware — they always route to the legacy wizard, because no new-wizard course-creation flow exists. This was true at §5.27 and remains true today. **The legacy wizard's code must stay fully functional after this fix regardless of the toggle default** — it is not purely dormant, it is the only path for course creation. This reinforces (and is fully consistent with) the task's own scoping: "not a delete."
2. **The dormant Edit-button gap** described in Part 2 — goes live tenant-wide the moment the default flips, though it is a UX regression (extra click via the list page) rather than a capability loss.

### The named trial customer

No "trial customer" identifier exists anywhere in the codebase, `CLAUDE.md`, or `BACKLOG.md` (grepped, case-insensitive, no matches). Locally the only non-E2E tenant is the seeded default `RASCOR` tenant, which is not marked as a trial account anywhere. A real trial customer, if one exists, would live on the Railway Development or Production database — **not reachable this session** (Railway CLI token expired: `invalid_grant`, matching the prior-session limitation noted in BACKLOG's "Demo deploy" item). This question cannot be answered with the tools available here; re-running this check after `railway login` (or with direct production DB credentials) is the only way to close it.

---

## Part 5 — Recommended fix shape

Two required changes, one optional cleanup:

1. **Flip the default** — `src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingsService.cs:56`: `"false"` → `"true"`. (If the team prefers the more auditable path, pair this with a one-time backfill script inserting explicit `true` rows for tenants with none — see Part 4. Either way this line changes.)
2. **Hide the toggle UI** — `web/src/app/(authenticated)/admin/toolbox-talks/settings/page.tsx:65`: remove (or comment out) the `<WizardToggleSection />` line. Leave the component file, the hook, and the mutation plumbing in place and importable — nothing else on the General tab is affected (confirmed scoped, Part 3).
3. **Optional, recommended, separable** — `ToolboxTalkDetail.tsx:151`: drop the `wizardPreference === 'old'` condition so the Edit button always renders, matching the list page's already-unconditional behavior. This closes the dormant UX gap from Part 2 before it goes live for every tenant. Not required for the literal "hide UI + flip default" ask, but doing it in the same change avoids shipping a known, foreseeable regression.

### Leave dormant / untouched

- The URL override (`?wizard=old` / `?wizard=new`) in `useWizardPreference.ts` — still fully functional post-flip, verified above.
- The toggle write path (`useUpdateTenantSettings`, `TenantSettingsController` PUT endpoint) — stays live so a tenant can still be set back to `false` via direct API call even with the UI hidden.
- The entire legacy wizard code path (`/admin/toolbox-talks/create`, `CreateWizard.tsx`, `create-wizard/steps/*`) — still load-bearing for Course creation regardless of this fix.
- The `lastEditedStep` discriminator and "Legacy" badge on the drafts list — unrelated to this toggle, no change needed.

---

## Rollback shape

**If only the two required changes ship (default flip + hidden UI):** a clean two-commit revert. No data was written anywhere, so reverting is exactly "undo the code" with no follow-up cleanup.

**If a backfill migration is also used:** the code-level revert (steps 1 and 2 above) is still clean, but the backfilled `TenantSetting` rows (`Value='true'`) are **not** automatically undone by reverting code — that is the one one-way piece. Reversing tenant state fully would need either a follow-up data migration (e.g. `DELETE FROM "TenantSettings" WHERE "Key" = 'UseNewWizard'` for rows created by the backfill, or setting them back to `'false'`), or simply leaving them — since the toggle's write path stays functional, any tenant can still be flipped back individually via a direct `PUT /api/tenant-settings` call, UI or no UI.

---

## Notes for the boss

The retirement itself is a two-line change (flip one default value, hide one settings card) with no migration required, because the toggle was already built as a sparse per-tenant override with an application-level fallback — nobody has ever explicitly set it locally, so the flip is effectively free. The one thing worth knowing before flipping: the legacy wizard isn't fully retirable in spirit yet — Course creation still runs through it exclusively (no new-wizard course flow exists), and there's a small, currently-invisible UX regression where the talk detail page's "Edit" button (which opens the only screen that can change a talk's title, video, or PDF) would silently disappear tenant-wide on flip, though the same edit screen stays one click away from the Learnings list. Recommend folding a one-line fix for that button into the same change so it doesn't surface as a support ticket later.
