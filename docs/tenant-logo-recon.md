# Tenant Logo in Header: Recon

Read-only recon. No code changed. Labels: **OBSERVED** (read in source, path cited) vs **INFERRED** (reasoned, not verified at runtime).

Goal: per-tenant logo in the app header with a 'Powered by CertifiedIQ' block; fall back to the current CertifiedIQ logo when unset.

---

## 1. Header

**OBSERVED**
- Component: `TopNav` in `web/src/components/layout/top-nav.tsx`.
- Rendered once, by `web/src/app/(authenticated)/layout.tsx:48` (`<TopNav />`). It is the only `TopNav` usage in `web/src`, so every route under `(authenticated)` gets it: employee portal, admin, profile, dashboard.
- Not shared with public pages. `login`, `set-password`, `qr/[codeToken]`, `external-review/[token]`, `dpa-acceptance`, `ai-system-card` live outside the group and each has its own inline CertifiedIQ branding (grep hit on `CertifiedIQ|<svg` in those files).
- There is no separate mobile header. One `<header>` serves all widths. The container is `container flex h-14 items-center justify-between px-4`, with no responsive hiding or hamburger.
- Logo source: **inline JSX SVG plus text span**. It is not a static asset, an import, or `next/image`. `web/public/` holds only the default Next SVGs (file, globe, next, vercel, window). `web/src/app/icon.svg` is the favicon.
- Other place the string appears: the profile dropdown footer, `A CertifiedIQ Product` (top-nav.tsx:158). That is a natural home or neighbour for 'Powered by CertifiedIQ'.

```tsx
// top-nav.tsx:53-70
<header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
  <div className="container flex h-14 items-center justify-between px-4">
    <Link href={isSuperUser ? "/admin/tenants" : user?.employeeId ? "/toolbox-talks" : "/admin/toolbox-talks"} className="flex items-center gap-2 hover:opacity-80 transition-opacity">
      <div className="flex items-center gap-2">
        <svg viewBox="0 0 46 46" fill="none" className="w-8 h-8">
          {/* circle, clock hand, 4 dots, all #4d8eff */}
        </svg>
        <span className="text-lg font-bold tracking-tight">
          Certified<span className="text-primary font-extrabold">IQ</span>
        </span>
      </div>
    </Link>

    <div className="flex items-center gap-4">
      {user?.isSuperUser && <TenantSwitcher />}
    </div>

    <div className="flex items-center gap-2"> {/* Help link + avatar dropdown */} </div>
  </div>
</header>
```

**INFERRED**
- The header is 56px (`h-14`). A logo image needs a bounded height (about `h-8`) with `object-contain` and a max width, or arbitrary tenant aspect ratios will push the centre and right groups.
- The `justify-between` layout with three children means a wide logo plus a 'Powered by' block on narrow screens will crowd the right-hand group. Nothing currently handles this.

---

## 2. Active tenant context

**OBSERVED**
- `AuthProvider` (`web/src/lib/auth/auth-context.tsx`) holds `activeTenantId` in React state. It persists to `localStorage["activeTenantId"]` (lines 43-56) and restores it on init (lines 112-116). `logout()` clears it.
- `activeTenantId` is only meaningful for SuperUsers. They pick it in `TenantSwitcher` (`web/src/components/layout/tenant-switcher.tsx`). `TenantSwitcher` returns null for non-SuperUsers and is only rendered when `user?.isSuperUser`.
- For non-SuperUsers the tenant comes from `user.tenantId`, populated from `GET /api/auth/me`. The payload is `{ id, email, firstName, lastName, tenantId, roles, permissions, isSuperUser, employeeId, enabledModules }` (`AuthController.cs:159-171`, `web/src/types/auth.ts`). **There is no tenant name and no logo in `/me`.** The `User` and `MeResponse` types have no tenant name either.
- Where the tenant name comes from today: only `TenantSwitcher`, via `useTenants({ pageSize: 100 })` (`GET /api/tenants`, `Tenant.Manage` policy, so SuperUser only). It finds the name with `tenants.find(t => t.id === activeTenantId)?.name`. The list DTO `TenantListDto` has no logo field.
- Header API calls carry the tenant: `web/src/lib/api/client.ts:88-92` injects `X-Tenant-Id` from `localStorage.activeTenantId`. The backend reads it only for SuperUsers (`CurrentUserService.cs:135-138`); for others the JWT `tenant_id` claim wins.
- Caching and invalidation: React Query, with `TENANTS_KEY = ["tenants"]` (`web/src/lib/api/admin/use-tenants.ts:15`). On a switch, `TenantQueryInvalidator` in `web/src/lib/providers.tsx:10-23` calls `queryClient.invalidateQueries()` with no filter. Every query refetches when `activeTenantId` changes. Tenant mutations invalidate `TENANTS_KEY`.

**INFERRED**
- No endpoint today lets a **non-SuperUser** fetch their own tenant's name or logo. `GET /api/tenants/{id}` is `Tenant.Manage` only. A new lightweight endpoint (or extending `/me`) is needed for tenant admins, operators and supervisors.
- For a SuperUser with "All Tenants" selected (`activeTenantId == null`) there is no tenant in context, so the fallback CertifiedIQ logo is the correct behaviour. Note `user.tenantId` for a SuperUser is their home tenant, not the viewed one, so the header must key off `activeTenantId` for SuperUsers and `user.tenantId` for everyone else. Using `user.tenantId` alone would show the wrong logo for SuperUsers.
- A query key including the tenant id (for example `["tenant-branding", tenantId]`) would work with the existing invalidate-all-on-switch. A stale-flash on switch is the only concern.
- On hard refresh the header renders after `isLoading` clears, because the layout returns a spinner until auth is ready. A logo fetch adds a second async step, so the logo may pop in. Embedding branding in `/me` would avoid that for non-SuperUsers.

---

## 3. Tenant entity

**OBSERVED** (`src/Core/QuantumBuild.Core.Domain/Entities/Tenant.cs`, extends `BaseEntity`)

| Column | Type |
|---|---|
| `Name` | string |
| `Code` | string? |
| `IsActive` | bool |
| `CompanyName` | string? |
| `Status` | `TenantStatus` (Active, Inactive, Suspended) |
| `ContactEmail` | string? |
| `ContactName` | string? |

Plus `BaseEntity` audit fields (Id, CreatedAt/By, UpdatedAt/By, IsDeleted, DeletedBy).

- **No logo, image or branding field exists.**
- Existing generic per-tenant key/value store: `TenantSetting` (`TenantId`, `Module`, `Key`, `Value`), used by `TenantSettingsController` / `TenantSettingKeys`. A logo URL could live there with no migration, but see risks.
- DTOs `TenantListDto`, `TenantDetailDto`, `CreateTenantCommand` and `UpdateTenantCommand` (`TenantDtos.cs`) have no logo field. `TenantService.UpdateAsync` overwrites Name, Code, CompanyName, ContactEmail and ContactName from the command.

**INFERRED**
- Cleanest option is a nullable `LogoUrl` (and probably `LogoStorageKey`) column on `Tenant`. That needs a CLI-generated migration (Note 28), and DTO and mapping updates in `TenantService`.
- `UpdateTenantCommand` is a positional record that replaces all fields. If a logo field were added to it, a client that omits the field would null it. Logo changes should go through their own endpoint.

---

## 4. Storage

**OBSERVED**
- Service: `R2StorageService : IR2StorageService`, `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Storage/R2StorageService.cs`. Provider: Cloudflare R2 via the AWS S3 SDK (`ForcePathStyle`). Interface in `ToolboxTalks.Application/Abstractions/Storage/IR2StorageService.cs`.
- Config: `R2StorageSettings` (section `R2Storage`): `BucketName` (default `rascor-media`), `PublicUrl`, `Endpoint`, `AccessKeyId`, `SecretAccessKey`, `MaxVideoSizeBytes` (500MB), `MaxPdfSizeBytes` (50MB). Note: CLAUDE.md documents `SubtitleProcessing__SrtStorage__CloudflareR2__*` keys, which are a separate subtitle config path.
- Key layout: `{tenantId}/{folder}/{fileName}`. Folders in use: `videos`, `pdfs`, `subs`, `certificates`, `validation-reports`, `qr-codes`, `cover-images`, `training-evidence-packs`, `sessions`, `bulk-import`. `regulatory/{docId}/source.pdf` has no tenant prefix.
- **Image helpers already exist.** `UploadCoverImageAsync(tenantId, talkId, stream, originalFileName)` stores to `{tenantId}/cover-images/{talkId:N}-cover.{png|jpg}`; `UploadQrCodeImageAsync` (png); `UploadSlideImageAsync(storagePath, bytes)` (png, arbitrary key). There is **no generic image upload**; each helper is entity-specific.
- Existing upload endpoint pattern: `POST /api/toolbox-talks/{id}/cover-image` in `ToolboxTalkFilesController.cs:294-341`. It uses `[RequestSizeLimit(5MB)]` and `IFormFile`, then calls the storage service and stores `result.PublicUrl` on the entity. There is also a session-scoped variant in `ContentCreationController` / `ContentCreationSessionService.UploadCoverImageAsync`.
- Serving: **public, unsigned, permanent URLs.** `GeneratePublicUrl` / `GetPublicUrl` build `{R2Storage.PublicUrl}/{escaped key}` (R2 public bucket domain, `pub-*.r2.dev` per CLAUDE.md). The URL is stored in the DB (e.g. `ToolboxTalk.CoverImageUrl`) and handed straight to the browser. No signing, no expiry, no proxy. A pre-signed URL exists only for PUT (`GenerateUploadUrlAsync`).
- Browser rendering of existing stored images: plain `<img src=...>` (`PublishStep.tsx:165`). `web/next.config.ts` is empty, so there is no `images.remotePatterns`. `next/image` with R2 URLs would fail until configured.
- Tenant cleanup: `DeleteAllTenantFilesAsync` deletes everything under `{tenantId}/` (used by reset data). A logo stored under that prefix would be deleted on "Reset Learning Data". Check whether the reset command should preserve it.

**INFERRED**
- Reusing `UploadCoverImageAsync` is not appropriate (keyed by talk id). A new `UploadTenantLogoAsync(tenantId, stream, ext)` with folder `branding` is the natural shape.
- Public URLs mean a logo is world-readable by anyone with the URL. That is acceptable for a logo. Overwriting at a fixed key would cause CDN/browser cache staleness, so a content-hashed or versioned key is advisable (the cover image helper uses a fixed key per talk, so the same staleness applies there).
- R2 public bucket CORS and caching headers were not inspected (not in repo); only matters if the logo is drawn to canvas or PDF.
- The storage service lives in the ToolboxTalks module, but `Tenant` and `TenantService` are in Core. Core cannot reference the ToolboxTalks module. The upload endpoint would need to sit in the API project (as `ToolboxTalkFilesController` does) or the interface would need to move or be duplicated.

---

## 5. Tenant Management UI

**OBSERVED**
- Pages: `web/src/app/(authenticated)/admin/tenants/page.tsx` (list), `.../new/page.tsx`, `.../[id]/page.tsx` (detail). Edit form: `web/src/components/admin/tenant-form.tsx`, used on the detail page. Detail page also has cards for status, modules (`tenant-modules-card.tsx`), sectors (`tenant-sectors-card.tsx`) and a reset-data danger zone.
- Frontend gate: the detail page returns "You do not have permission to access Tenant Management" unless `useIsSuperUser()` (`[id]/page.tsx:96`). Nav item `/admin/tenants` has `superUserOnly: true` (`admin/layout.tsx:15`).
- Backend gate: `TenantsController` is class-level `[Authorize(Policy = "Tenant.Manage")]`. `DataSeeder.GetPermissionsForRole` gives `Tenant.Manage` to SuperUser only; Admin gets "all except Tenant.Manage" (`DataSeeder.cs:383-386`).
- **A tenant admin cannot edit their own tenant today.** There is no tenant-admin tenant-edit surface. `/admin/settings` (Learnings.Admin / Core.ManageUsers) is the tenant-scoped settings page; it was not read.
- Precedent for tenant admins touching tenant data: `TenantSectorsController.AssignSector` allows `Learnings.Admin` on own tenant or SuperUser (see section 6), with a separate `/admin/regulatory/my-sectors` UI.

**INFERRED**
- Logo upload by platform admins only fits the existing detail page (new card beside modules and sectors). Tenant-admin self-service would need a new endpoint plus a UI in `/admin/settings`, following the `TenantSectors` split.
- That `/admin/settings` is the right home for tenant-admin self-service is inferred from the nav entry only; the page itself was not opened.

---

## 6. Authorisation pattern

**OBSERVED**
- `TenantsController.Update(Guid id, ...)` (`TenantsController.cs:101-117`) has **no per-tenant ownership check**. Safety rests entirely on the class-level `Tenant.Manage` policy being SuperUser-only. `TenantService.UpdateAsync` uses `IgnoreQueryFilters()` and loads by route id with no caller-tenant comparison.
- Self-scope pattern that does exist, in `TenantSectorsController`:
  - Read (line 28): `if (!currentUserService.IsSuperUser && currentUserService.TenantId != tenantId) return Forbid();`
  - Write (lines 49-52): `var isLearningsAdmin = User.HasClaim("permission", "Learnings.Admin"); var ownTenant = currentUserService.TenantId == tenantId; if (!currentUserService.IsSuperUser && !(isLearningsAdmin && ownTenant)) return Forbid();`
- `ToolboxTalkFilesController` scopes by query: `t.TenantId == _currentUser.TenantId`.
- `ICurrentUserService.HasPermission` returns true for SuperUser unconditionally (`CurrentUserService.cs:83`).
- Note 24 applies: class-level and action-level `[Authorize]` both must pass, so a tenant-admin logo endpoint cannot live on `TenantsController`.

**INFERRED**
- For a tenant-admin logo endpoint, the safest shape is a route with no tenant id (resolve from `ICurrentUserService.TenantId`) or the `TenantSectorsController` check copied exactly. A SuperUser must have `X-Tenant-Id` set, otherwise `TenantId` is `Guid.Empty` and the write would target no tenant (see risks).
- If logo edit is ever added to `UpdateTenantCommand`, the missing ownership check becomes load-bearing only if the policy is ever broadened. It is not today.

---

## 7. Existing file-upload validation to reuse

**OBSERVED**
- `ToolboxTalkFilesController.cs:29-33,304-311`: `MaxCoverImageSizeBytes = 5MB`, `AllowedImageTypes = ["image/png", "image/jpeg"]`, null/empty check, `ContentType` allow-list, `Length` check, plus `[RequestSizeLimit(5MB)]`. Closest match for a logo.
- `R2StorageService.UploadCoverImageAsync` (lines 500-502) coerces any non-png/jpg/jpeg extension to `jpg`.
- `ContentCreationSessionService.UploadCoverImageAsync` (lines 1261-1269): 5MB limit and **extension** check (png/jpg/jpeg). It passes the client `file.ContentType` straight to R2.
- `RegulatoryIngestionController.cs:186-195`: ContentType allow-list plus size limit for PDFs.
- `BulkSopImportController.cs:113-123`: content-type or `.zip` extension check plus size limit. `LessonParserController`: extension allow-list plus size limit.
- Per-type config on `R2StorageSettings` covers video and PDF only; no image settings.
- **No magic-byte/signature sniffing, no image decoding, no dimension checks, and no SVG handling anywhere** (grep for `svg|magic|FileSignature` in `src/**/*.cs` found nothing relevant).

**INFERRED**
- All current checks trust the client-supplied `Content-Type` or file extension. The two cover-image paths disagree on which one to check, and the stored object's content type is derived from whichever the caller trusted. For a tenant logo displayed on every page, content-sniffing and a dimension cap would be reasonable additions; none exist to reuse.

---

## Notable risks

1. **No path for non-SuperUsers to learn the tenant's name or logo.** `/me` has neither, and `/api/tenants` is SuperUser-only. A new endpoint or `/me` extension is required, with a self-tenant check.
2. **SuperUser header must use `activeTenantId`, not `user.tenantId`.** Otherwise the wrong tenant's logo shows, and "All Tenants" must fall back to CertifiedIQ.
3. **SuperUser write with no tenant selected resolves `TenantId = Guid.Empty`** (`CurrentUserService.cs:122`). Any new logo endpoint that uses `ICurrentUserService.TenantId` must reject `Guid.Empty`, or it will write to `{Guid.Empty}/branding/...`.
4. **Upload validation trusts client content type or extension, with no sniffing.** SVG must be explicitly excluded (script-in-SVG risk on a public, unsigned origin). PNG/JPEG only, matching the cover-image allow-list, is the lowest-risk option.
5. **Public, unsigned, non-expiring URLs.** Fine for a logo, but fixed-key overwrites will serve stale images from cache. Use a versioned or hashed key.
6. **`DeleteAllTenantFilesAsync` wipes `{tenantId}/`.** Tenant "Reset Learning Data" would delete a logo stored there and leave a dangling URL on the tenant. Decide whether reset should clear the DB field or preserve the file.
7. **Module boundary.** `IR2StorageService` is in the ToolboxTalks module; `Tenant` and `TenantService` are in Core. The logo endpoint likely belongs in the API project; Core cannot call the storage service directly.
8. **`next/image` will not work with R2 URLs** without `images.remotePatterns` (`next.config.ts` is empty). Existing code uses plain `<img>`; follow that or add config.
9. **`UpdateTenantCommand` is full-replace.** Do not add logo to it; use a dedicated endpoint, or omitted fields will null the logo.
10. **Header layout has no responsive handling.** One `h-14` header for all widths; a wide logo plus 'Powered by' will crowd the controls on mobile unless constrained.
11. **Schema change needs a CLI-generated migration** (Note 28) if a `Tenant` column is added. Using `TenantSetting` avoids this, but the key/value store has no URL semantics and is not returned by any anonymous or me-style path.
12. **Flash of fallback logo on first paint**, since branding would load after auth. Consider embedding it in `/me` for non-SuperUsers to avoid a CertifiedIQ-to-tenant swap on every page load.

---

# Addendum: DbContext, migration snapshot, query filters

## A. Which DbContext owns Tenant

**OBSERVED**
- `ApplicationDbContext` (`src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs:49`, `DbSet<Tenant> Tenants`). It also implements `ICoreDbContext` and `IToolboxTalksDbContext`, so it is the single context for Core and ToolboxTalks entities. `dotnet ef dbcontext list` shows two contexts: `ApplicationDbContext` and `LessonParserDbContext` (separate, not relevant).
- The migrations and snapshot live in **`src/Core/QuantumBuild.Core.Infrastructure/Migrations/`** (`ApplicationDbContextModelSnapshot.cs`, latest `20260826152122_ChangeDefaultPreserveSourceWordingToFalse`). The ToolboxTalks Infrastructure project has no Migrations folder.
- **CLAUDE.md is stale here.** Its EF section says `--project ../Modules/ToolboxTalks/...Infrastructure`. The correct command, run from `src/QuantumBuild.API`, is:
  `dotnet ef migrations add AddTenantBranding --project ../Core/QuantumBuild.Core.Infrastructure --context ApplicationDbContext`
  `--context` is mandatory because two contexts exist; without it EF errors "More than one DbContext was found".
- Entity configurations are registered by `modelBuilder.ApplyConfiguration(new XConfiguration())` in `OnModelCreating` (e.g. lines 300-333). A new `TenantBrandingConfiguration` must be added to that list, plus a `DbSet` (and an `ICoreDbContext` member if Core services will use it).

## B. Model snapshot cleanliness

**OBSERVED** (run just now, nothing written)
- `git status --short`: only `?? docs/tenant-logo-recon.md`. The working tree has no uncommitted model work.
- `dotnet ef migrations has-pending-model-changes --project ../Core/QuantumBuild.Core.Infrastructure --context ApplicationDbContext` returned **"No changes have been made to the model since the last migration."**
- So the snapshot is clean on branch `transval` at the current HEAD. A migration generated now would contain only TenantBranding, provided nothing else is merged first.
- Side notes: EF tools are 9.0.10 vs runtime 9.0.20 (warning only). The `Model.Validation[20601]` sentinel warnings are the known pre-existing noise.

**Procedure for the build chunk**
1. Re-run `has-pending-model-changes` immediately before `migrations add` (if any rebase or merge happened).
2. Run `migrations add AddTenantBranding`; confirm both `.cs` and `.Designer.cs` exist (Note 28).
3. Open `Up()` and confirm it contains only the `TenantBrandings` table plus its index and FK. Any other operation means unrelated drift: stop and report.

## C. Query filters

**OBSERVED**
- Filters are declared centrally in `ApplicationDbContext.OnModelCreating` (lines 335-394). There is **no `ITenantEntity` interface and no automatic filter application**. Each entity needs its own explicit `HasQueryFilter` line. A new entity deriving `TenantEntity` gets **no filter unless one is added** (Note 14 was this class of bug).
- Tenant-scoped predicate: `!e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId)`.
- `TenantId` is `_currentUserService.TenantId`. For a SuperUser that is the `X-Tenant-Id` header value, or `Guid.Empty` if absent (`CurrentUserService.cs:122-140`). `BypassTenantFilter` is true only for a SuperUser with **no** header.
- `Tenant` itself has `HasQueryFilter(e => !e.IsDeleted)` only (line 388): global, not tenant-scoped.
- `SaveChangesAsync` runs `SetAuditFields` (lines 411-456):
  - Added `TenantEntity` with `TenantId == Guid.Empty` gets `TenantId = TenantId` (the active tenant). An explicit non-empty `TenantId` is respected (Note 22).
  - **Modified `TenantEntity` has `TenantId.IsModified = false` forced.** TenantId can never change on update.
  - Deleted becomes soft-delete.
- Precedent for filter-exempt per-tenant tables: `DpaAcceptance` has soft-delete only (line 353). `ToolboxTalkSettings` and others are "BaseEntity-only" with tenant handled in service code (comment lines 375-377). `TenantSector` uses the full tenant predicate.

**The collision for TenantBranding (INFERRED from the filter definition, not run)**
- If `TenantBranding` derived `TenantEntity` and got the standard filter, a SuperUser with active tenant X (`X-Tenant-Id: X`) writing a logo for route tenant Y would:
  - read Y's row: **filtered out** (`TenantId == X` predicate), so the lookup returns null and the code takes the insert path;
  - insert with explicit `TenantId = Y` (auto-stamp skipped): fine on first upload, but on the second upload it **violates the unique index** on `TenantId`, because the existing Y row was invisible.
- `TenantService` already sidesteps this for `Tenants` by calling `.IgnoreQueryFilters()`.

**Recommended design (pick one explicitly in the build prompt)**
1. **Preferred: `TenantBranding` as a `BaseEntity` with `TenantId` as a plain unique FK column and a soft-delete-only filter**, mirroring `DpaAcceptance` and `Tenant`. Every query is explicit: `.Where(b => b.TenantId == routeTenantId)`. The write path never depends on ambient `X-Tenant-Id`. Because it is not a `TenantEntity`, `SetAuditFields` neither auto-stamps nor freezes `TenantId`, which is correct since the service sets it from the route and never changes it. Add it to the "intentionally unfiltered" comment block (lines 375-377).
   - Risk: with no tenant filter, any future query that forgets the `TenantId` predicate leaks across tenants. Mitigate by exposing access only through one service (`ITenantBrandingService`) and by the isolation tests below.
2. Alternative: derive `TenantEntity` with the standard filter, and make the service use `.IgnoreQueryFilters().Where(TenantId == routeTenantId && !IsDeleted)` on every read and write. Keeps the safety net for other call sites, but `IgnoreQueryFilters()` must be on every write-path query.
3. Either way, the header read path resolves the tenant by explicit rule (non-SuperUser: JWT tenant; SuperUser: `activeTenantId`), never by the ambient filter.

Authorisation is independent of this choice. The route `tenantId` is untrusted, so the controller check (`IsSuperUser || (Learnings.Admin && CurrentUserService.TenantId == tenantId)`, copied from `TenantSectorsController.AssignSector`) is what blocks cross-tenant writes, not the filter. A SuperUser passes by `IsSuperUser` regardless of `X-Tenant-Id`, which is the case in the requested test.

## D. Required test (add to the build chunk)

Location: `tests/QuantumBuild.Tests.Integration/`, next to `Core/TenantIsolationTests.cs` (uses `IntegrationTestBase` and `CustomWebApplicationFactory`). A grep of `IntegrationTestBase.cs` and `CustomWebApplicationFactory.cs` for SuperUser found no SuperUser client helper. The factory has `CreateClient(new WebApplicationFactoryClientOptions ...)` at lines 363 and 388 that should be inspected first. "Not found by grep" is not proof it is absent.

`SuperUser_ActiveTenantX_UploadsLogoForTenantY_WritesY_LeavesXUntouched`
1. Arrange: two tenants X and Y, neither with a branding row. SuperUser client sending `X-Tenant-Id: X`.
2. Act: upload a valid PNG to the endpoint for tenant **Y** (route id Y).
3. Assert: 200; a row exists for Y with a storage key under `{Y}/...`; **no row for X**; the fake R2 recorded no key under `{X}/`.
4. Upload again for Y: still exactly one row for Y (upsert works, no unique-index violation). This covers the filter collision in section C.

Companion tests:
- SuperUser with **no** `X-Tenant-Id` uploading for Y: succeeds and writes Y, never `Guid.Empty`.
- Tenant admin of X uploading for Y: 403, nothing written.
- Tenant admin of X uploading for X: 200, row for X only.
- Read path: tenant admin of X reads X's branding; asking for Y's id returns 403.
