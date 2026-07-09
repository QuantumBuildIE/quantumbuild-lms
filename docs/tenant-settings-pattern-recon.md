# Tenant Settings Pattern Recon — replicating `UseNewWizard` for `UseNewCourseCreation`

**Date:** 2026-07-09
**Scope:** Read-only investigation. No code changes.
**Purpose:** Document the exact `UseNewWizard` storage/default/read/override mechanics so `UseNewCourseCreation` (per `docs/course-in-new-wizard-scoping-v2.md`) replicates the pattern rather than inventing a variant.

Related docs: `docs/wizard-toggle-retirement-recon.md` (retirement-focused, reused here where its findings overlap), `docs/course-in-new-wizard-scoping-v2.md` (the product decision this recon feeds).

---

## Headline

**The whole thing is a five-layer stack, and every layer for a new setting is a one-line addition to an existing generic mechanism — nothing wizard-specific has leaked into the storage, API, or write path; the only genuinely new code needed is a new hook (`useCourseModePreference`) mirroring `useWizardPreference` and its call sites.**

---

## Part 1 — Storage

### Table structure

Generic EF-mapped key/value table, one row per `(TenantId, Key)` (unique index also includes `Module`, but `Module` is always `"General"` in practice today):

```csharp
// src/Core/QuantumBuild.Core.Domain/Entities/TenantSetting.cs
public class TenantSetting : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

DB shape (confirmed in prior recon, unchanged): `TenantSettings(Id uuid, TenantId uuid, Module varchar(100), Key varchar(200), Value varchar(2000), ...)`, unique on `(TenantId, Module, Key)`.

### Key namespacing

No namespace convention (no `feature.wizard.new`-style dotted keys). Every key is a flat PascalCase constant matching the C# property name a reader would expect, defined centrally:

```csharp
// src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingKeys.cs
public static class TenantSettingKeys
{
    public const string GeneralModule = "General";

    public const string EmailTeamName = "EmailTeamName";
    public const string TalkCertificatePrefix = "TalkCertificatePrefix";
    public const string CourseCertificatePrefix = "CourseCertificatePrefix";
    public const string SkipValidationStep = "SkipValidationStep";
    public const string QrLocationTrainingEnabled = "QrLocationTrainingEnabled";
    public const string ExternalParticipantTokenLifetimeDays = "ExternalParticipantTokenLifetimeDays";
    public const string UseNewWizard = "UseNewWizard";

    public static class Defaults
    {
        public const string EmailTeamName = "Training Team";
        public const string TalkCertificatePrefix = "LRN";
        public const string CourseCertificatePrefix = "TBC";
    }
}
```

`UseNewCourseCreation` should be added here as a sibling constant — `TenantSettingKeys.UseNewCourseCreation = "UseNewCourseCreation"`. No sub-namespace, no prefix, matches the flat style of every existing key.

### Value typing

Everything is `string`. Booleans are the literal strings `"true"` / `"false"` compared with `=== 'true'` (frontend) or `== "true"` (backend, where backend even branches on it — see the QR job-enqueue example in Part 2). There is no typed/JSON column and no per-key schema describing expected value shape — it's genuinely free-form; `Value` is just whatever string the last writer put there (`ExternalParticipantTokenLifetimeDays` stores a numeric string, e.g., proving the table isn't boolean-only).

### Is there a settings schema?

No. `TenantSettingKeys` is the closest thing to a schema — it's just a list of known constants. Nothing validates that `dto.Settings` keys sent to the PUT endpoint are members of that list; the `TenantSettingsController.Update` action loops over whatever keys the client sends and blind-`SetSettingAsync`s each one. A typo'd key from the frontend would silently create a new orphan row with no code ever reading it back. This is a real (pre-existing, not in scope to fix) footgun — worth being deliberate about matching `TenantSettingKeys.UseNewCourseCreation` exactly between the constant and the frontend's literal string.

---

## Part 1 — Default mechanism

Defaults exist in exactly one place, as a hardcoded seed dictionary inside the service method that assembles the full settings response:

```csharp
// src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingsService.cs:43-65
public async Task<Dictionary<string, string>> GetAllSettingsAsync(Guid tenantId, CancellationToken ct = default)
{
    var settings = await context.TenantSettings
        .AsNoTracking()
        .Where(s => s.TenantId == tenantId)
        .ToListAsync(ct);

    // Start with defaults, then overlay saved values
    var result = new Dictionary<string, string>
    {
        [TenantSettingKeys.EmailTeamName] = TenantSettingKeys.Defaults.EmailTeamName,
        [TenantSettingKeys.TalkCertificatePrefix] = TenantSettingKeys.Defaults.TalkCertificatePrefix,
        [TenantSettingKeys.CourseCertificatePrefix] = TenantSettingKeys.Defaults.CourseCertificatePrefix,
        [TenantSettingKeys.UseNewWizard] = "false"
    };

    foreach (var setting in settings)
    {
        result[setting.Key] = setting.Value;
    }

    return result;
}
```

Notes on this mechanism:
- It's a literal dictionary initializer, not a switch, not an attribute, not driven by `TenantSettingKeys.Defaults` consistently — three of the four seeded defaults use the `Defaults` nested class, but `UseNewWizard` is seeded as a bare `"false"` literal inline rather than via `TenantSettingKeys.Defaults.UseNewWizard`. This is an existing inconsistency, not something to fix, but a new setting should decide which style to follow (see Part 7 template — recommend following the bare-literal style already used for the boolean flag, since `TenantSettingKeys.Defaults` currently only holds string-prefix-style values, not booleans).
- There is no DB-level column default and no seed/migration data (`DataSeeder.cs` does not touch `TenantSettings` at all, confirmed in the prior recon and re-confirmed by the grep in Part 1 above — `TenantSettingKeys` and `TenantSettingsService` are the only two backend files matching `UseNewWizard`).
- **Resolution of "unset":** a tenant with no row for a key gets the dictionary's seeded value, full stop. `GetSettingAsync(tenantId, key, defaultValue)` (used by any backend code that wants a single setting outside the `GetAllSettingsAsync` bulk path) takes its own explicit `defaultValue` parameter per call site — it does **not** consult the `GetAllSettingsAsync` dictionary, so if backend code ever needs to branch on `UseNewCourseCreation` server-side (unlike `UseNewWizard`, which today has zero backend read sites), the default must be passed at that call site too, independently of the dictionary above. Two defaults to keep in sync if both paths are used.

---

## Part 2 — Read consumers

### Backend

**Zero backend behavior branches on `UseNewWizard`.** Its only backend appearance is the seed-dictionary entry in `TenantSettingsService.cs:56`, which is just data supplied to the frontend — confirmed by grepping the whole backend for `UseNewWizard` (only the two files in Part 1 match). This is a real asymmetry from other settings like `QrLocationTrainingEnabled`, which the backend does branch on (`TenantSettingsController.cs:38-48`, deciding whether to enqueue `GenerateEmployeePinsJob`). `UseNewCourseCreation` is expected to be `UseNewWizard`-shaped (frontend-only routing decision, no backend behavior change) per the scoping doc — confirm this holds before assuming zero backend touch points.

### Frontend

All reads for `UseNewWizard` funnel through one hook, `useWizardPreference`, which is the only place that actually reads `settings?.['UseNewWizard']` for routing purposes. One component (`WizardToggleSection`) reads the raw setting directly because it needs the raw `'true'`/`'false'` string for the Switch control, not the resolved `'new'`/`'old'` preference.

| # | File : Line | Reads via | Behavior on unset/`false` | Behavior on `true` |
|---|---|---|---|---|
| 1 | `web/src/features/toolbox-talks/hooks/useWizardPreference.ts:30` | Canonical hook | `'old'` | `'new'` (unless URL override) |
| 2 | `web/src/features/toolbox-talks/components/settings/wizard-toggle-section.tsx:18` | Direct read (Switch UI only) | Switch off | Switch on |
| 3 | `web/src/features/toolbox-talks/components/ToolboxTalkList.tsx:57,358` | Via hook | "Create New" → legacy `${basePath}/create` | → `/admin/toolbox-talks/learnings/new` |
| 4 | `web/src/features/toolbox-talks/components/create-wizard/steps/PublishStep.tsx:93,226` | Via hook | "Create Another" → `/admin/toolbox-talks/create` | → `/admin/toolbox-talks/learnings/new` |
| 5 | `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx:40,64,151` | Via hook | Edit button shown (`canManage && wizardPreference === 'old'`) | Edit button hidden |

All five are null-safe via the hook's `settings?.['UseNewWizard'] === 'true'` strict-equality check — `undefined`/loading/missing-key all resolve to `false` → `'old'`.

Three other files call `useTenantSettings()` for **unrelated** keys and are not part of the `UseNewWizard` read set — noting them so they aren't mistaken for hidden read sites: `admin/settings/page.tsx` (email/cert-prefix fields), `admin/employees/[id]/page.tsx` (QR training display), `create-wizard/CreateWizard.tsx:169` + both `InputConfigStep.tsx` files (`SkipValidationStep`), `settings/pass-threshold-section.tsx`, `settings/skip-validation-section.tsx`, `settings/qr-location-training-section.tsx`, `settings/audit-purpose-section.tsx` (all read their own specific key from the same `useTenantSettings()` bag).

### Is it exposed via an API endpoint / context provider?

Neither, precisely — it's a plain TanStack Query hook, not a React Context. `GET /api/tenant-settings` returns the **entire** flat dictionary (all keys, defaults overlaid) in one call; there is no per-key endpoint. `useTenantSettings()` (`web/src/lib/api/admin/use-tenant-settings.ts:10-15`) wraps that single `GET` in a `useQuery` keyed on `["tenant-settings"]`, cached and shared across every consuming component via TanStack Query's cache — so `useWizardPreference`, `WizardToggleSection`, and every other settings-reading component all hit the same cached response, not five separate network calls. Read at first render of whichever component mounts first (typically page load of whatever authenticated page renders first), not a dedicated bootstrap/context step.

---

## Part 3 — URL override pattern

### Interception layer

Frontend-only, inside the same hook — **not** Next.js middleware, not a backend concern, not a route resolver. `useSearchParams()` (Next.js App Router client hook) reads the query string of whichever page currently has the hook mounted:

```ts
// web/src/features/toolbox-talks/hooks/useWizardPreference.ts:22-31
export function useWizardPreference(): WizardPreference {
  const searchParams = useSearchParams();
  const { data: settings } = useTenantSettings();

  const urlOverride = searchParams.get('wizard');
  if (urlOverride === 'new') return 'new';
  if (urlOverride === 'old') return 'old';

  return settings?.['UseNewWizard'] === 'true' ? 'new' : 'old';
}
```

### Interaction with the tenant setting

**Short-circuit, not overlay.** The URL param is checked first and returns immediately on a match (`'new'` or `'old'` literal) — the tenant-setting fallback line never executes when an override is present. This means the override does not need to know or care what the underlying setting resolves to; it's a pure early-return, which is why flipping the in-code default later doesn't touch this branch at all (verified in the prior recon and re-confirmed here by direct read).

Any string other than the literals `'new'`/`'old'` (including absence of the param) falls through to the tenant-setting branch — there's no validation/warning for a mistyped `?wizard=nwe`, it just silently behaves as if no override were present.

### Persistence

**Per-navigation only, not persisted.** It survives a refresh only because refreshing re-requests the same URL (param stays in the address bar), but any `router.push()` to a **different** path (e.g., clicking "Create New" itself) drops the query string unless the new URL explicitly carries it forward — none of the routing call sites in Part 2 do this. Nothing writes it to localStorage, a cookie, or the tenant-setting table. This is confirmed by the docstring on the hook itself (`useWizardPreference.ts:16-18`: "not persisted... intended for operator smoke-testing only; not exposed to end users") and by the absence of any `localStorage`/cookie write in the file.

---

## Part 4 — Gaps and quirks

### Confirmed gaps (courses)

Both course-creation entry points are **completely unaware** of `UseNewWizard` — no import of `useWizardPreference` exists in either file (grepped, zero matches):

- `web/src/app/(authenticated)/admin/toolbox-talks/courses/new/page.tsx` — unconditional server-side `redirect('/admin/toolbox-talks/create')`. No client component, no hook possible here as currently written (a `redirect()` in a Server Component can't read a tenant-setting fetched client-side without restructuring — see Part 7 template for the shape this needs to take for `UseNewCourseCreation`).
- `web/src/features/toolbox-talks/components/CourseList.tsx:257-260` — "Create New" button hardcoded to `router.push('/admin/toolbox-talks/create')`, no conditional at all.

This matches `docs/course-in-new-wizard-scoping-v2.md`'s framing exactly: these are the two files needing the toggle-aware routing swap for `UseNewCourseCreation`. Confirmed still true as of this recon (no work has landed on either file yet).

### Other admin surfaces — none found

Grepped `web/src` broadly for `UseNewWizard`, `useWizardPreference`, and `WizardToggleSection` — no other admin page, dashboard, or nav surface references it. The Draft Learnings list's "Legacy" badge (`learnings/drafts/page.tsx`) is driven by the per-talk `lastEditedStep === null` discriminator, not this tenant setting — unrelated, confirmed in the prior recon, still holds.

### Inconsistencies in default resolution

- **Bare literal vs. `Defaults` class:** `UseNewWizard`'s default (`"false"`) is inlined directly in `GetAllSettingsAsync`, while `EmailTeamName`/`TalkCertificatePrefix`/`CourseCertificatePrefix` route through `TenantSettingKeys.Defaults.*`. Stylistically inconsistent but functionally identical — both paths land in the same dictionary. `UseNewCourseCreation` can follow either; recommend matching `UseNewWizard`'s bare-literal style since both are booleans and the `Defaults` class doesn't currently hold any boolean members.
- **Default-when-unset varies by consumer, not by mechanism:** `useWizardPreference` resolves unset to `'old'` (i.e., `false`). `WizardToggleSection`'s direct read (`settings?.[SETTINGS_KEY] === 'true'`) also resolves unset to `false`/off — consistent with each other. No consumer found that defaults `true` on absence. So despite the "quirks to look for" prompt in the task, no inconsistency was found here for `UseNewWizard` specifically — worth stating plainly rather than manufacturing a finding.
- **Per-call-site defaults for `GetSettingAsync`:** as noted in Part 1, any backend code calling the single-key `GetSettingAsync` helper supplies its own default independently of the `GetAllSettingsAsync` dictionary default. `UseNewWizard` has no such backend call site today, so this hasn't been a live inconsistency, but it becomes one to watch if `UseNewCourseCreation` ever needs a backend-side read (e.g., if the compose-existing course validation needs to know which mode created a course) — two defaults would need to be kept in sync by hand.

---

## Part 7 — Template for adding `UseNewCourseCreation`

### 1. Backend key constant

```csharp
// src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingKeys.cs
public const string UseNewCourseCreation = "UseNewCourseCreation";
```

### 2. Backend default registration

```csharp
// src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingsService.cs
// inside GetAllSettingsAsync's seed dictionary, alongside UseNewWizard:
[TenantSettingKeys.UseNewCourseCreation] = "true"   // per scoping-v2.md: default true at cutover, unlike UseNewWizard's "false"
```
No migration, no DB default, no `DataSeeder.cs` change needed — same as `UseNewWizard`. Note the default value itself differs deliberately from the `UseNewWizard` template: scoping-v2 calls for `UseNewCourseCreation` to default `true` at cutover (course composition ships as primary), whereas `UseNewWizard` defaults `false` (legacy-first, opt-in). Don't copy the `"false"` literal by habit.

### 3. Backend read (if ever needed)

Not required today — `UseNewWizard` has zero backend read sites, and `UseNewCourseCreation` is scoped the same way (pure frontend routing decision) per scoping-v2.md. If a backend guard is later needed (e.g. rejecting legacy-course-shaped payloads when the tenant is in new-mode), use:
```csharp
var mode = await tenantSettingsService.GetSettingAsync(tenantId, TenantSettingKeys.UseNewCourseCreation, defaultValue: "true", ct);
```

### 4. Frontend hook

New file mirroring `useWizardPreference.ts` exactly in structure (do not extend/parameterize the existing hook — `docs/course-in-new-wizard-scoping-v2.md` treats this as an independent toggle, and a shared generic hook would be a premature abstraction for two call sites):

```ts
// web/src/features/toolbox-talks/hooks/useCourseModePreference.ts
'use client';

import { useSearchParams } from 'next/navigation';
import { useTenantSettings } from '@/lib/api/admin/use-tenant-settings';

export type CourseModePreference = 'new' | 'old';

export function useCourseModePreference(): CourseModePreference {
  const searchParams = useSearchParams();
  const { data: settings } = useTenantSettings();

  const urlOverride = searchParams.get('coursemode');
  if (urlOverride === 'new') return 'new';
  if (urlOverride === 'old') return 'old';

  return settings?.['UseNewCourseCreation'] === 'true' ? 'new' : 'old';
}
```

### 5. Frontend read call sites (the two confirmed gaps from Part 4)

- `CourseList.tsx:257-260` — replace the hardcoded `router.push('/admin/toolbox-talks/create')` with the same conditional pattern as `ToolboxTalkList.tsx:358-360`: `coursePreference === 'new' ? '<new-course-route>' : '/admin/toolbox-talks/create'`.
- `courses/new/page.tsx` — this file is currently a Server Component doing an unconditional `redirect()`. Reading a client-fetched tenant setting requires converting it to a Client Component (mirroring how `CreateWizard.tsx` reads `useTenantSettings()` client-side) or moving the branch up into whatever links to this route so the router never even hits a redirect stub. Match whichever new-course route shape `docs/course-in-new-wizard-scoping-v2.md`'s implementation chunk 1 settles on for `CourseForm.tsx`'s entry point.

### 6. Settings UI toggle (optional, per scoping doc's chunk 1)

Mirror `wizard-toggle-section.tsx` if a manually-flippable UI toggle is wanted alongside the API-only path — same `Card` + `Switch` + `useTenantSettings`/`useUpdateTenantSettings` shape, `SETTINGS_KEY = 'UseNewCourseCreation'`.

### 7. URL override

No backend involvement — purely the `searchParams.get('coursemode')` check inside the new hook (step 4 above). Confirmed this is the entire mechanism for `?wizard=`; `?coursemode=` needs nothing beyond the hook itself.
