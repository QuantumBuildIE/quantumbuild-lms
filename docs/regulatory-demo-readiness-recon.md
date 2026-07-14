# Regulatory Display Flow — Demo Readiness Recon

**Recon date:** 2026-06-25  
**Demo:** Friday 2026-06-26, Platinum Homecare, HIQA-driven positioning  
**Branch at time of recon:** `transval`

---

## 1. Headline

> **Demo-ready after data confirmation and one wizard-path decision**

All seed data for Homecare/HIQA is present in code and seeds unconditionally on every startup. The regulatory applicability endpoint, amber banner, and RegulatoryScorePanel are all implemented and match the §1.2.2 BACKLOG claim. Two pre-demo actions are needed:

1. **Confirm the Development DB has the seed data applied** — verify via `GET /api/regulatory/applicability?sectorKey=homecare` returning `{ hasRegulatoryProfile: true, approvedRequirementCount: 15 }`.
2. **Decide which wizard the boss uses on demo day** — the §1.2.2 amber banner lives only in the new wizard. For homecare (15 approved requirements) the banner won't trigger anyway, but the RegulatoryScorePanel's applicability-gating of the score button depends on `run.sectorKey` being set, which has NOT been verified for the legacy wizard path. Using the new wizard removes this uncertainty entirely.

No code change is required.

---

## 2. Data State Findings

All figures are inferred from seed code (`SectorSeedData.cs`, `RegulatoryProfileSeedData.cs`, `RegulatoryRequirementSeedData.cs`). Seeds run unconditionally in all environments via `Program.cs` on every startup.

### 2.1 Homecare Sector

- **Sector row:** exists, `Key = "homecare"`, `Name = "Homecare"`, `IsActive = true`, `DisplayOrder = 2`
- **Source:** `src/Modules/ToolboxTalks/.../Persistence/Seed/SectorSeedData.cs`

### 2.2 HIQA Regulatory Body

- **Code:** `HIQA`, **Name:** Health Information and Quality Authority, **Country:** Ireland
- **Source:** `RegulatoryProfileSeedData.cs`

### 2.3 Regulatory Document

- **Title:** "Draft National Standards for Home Support Services", **Version:** Draft Nov 2024
- **Body:** HIQA
- **Source:** `RegulatoryProfileSeedData.cs`

### 2.4 Regulatory Profile (homecare × HIQA)

- **SectorKey:** `"homecare"`, **IsActive:** true
- **ScoreLabel:** `"HIQA Regulatory Score"` — this is the value the `GetApplicability` endpoint returns as `profileName`, and what the RegulatoryScorePanel column header shows
- **ExportLabel:** `"HIQA Inspection Export"`
- **Description:** "Safeguarding, medication, mandatory reporting, EVV compliance"
- **CategoryWeightsJson:** 6 weighted categories (Terminology Consistency ×1.5, Safety-Critical Language ×1.5, Professional Register ×1.0, Regulatory Completeness ×1.0, Grammatical Accuracy ×1.0, Naturalness & Fluency ×1.0)
- **Source:** `RegulatoryProfileSeedData.cs`

### 2.5 Regulatory Requirements

- **Total seeded:** 15 requirements
- **IngestionStatus:** ALL `Approved` — zero Draft rows
- **IsActive:** all true
- **Sample titles:** Safeguarding Incident Recording, MAR Pre-signing Prohibition, EVV GPS Check-in, HIQA Notification Timeline, Care Plan Creation Timeline (×3 principles: Safety & Wellbeing, Responsiveness, Accountability)
- **Source:** `RegulatoryRequirementSeedData.cs`

### 2.6 Regulatory Criteria (scoring rubric)

- **Count:** 4 criteria seeded for homecare/HIQA
- **Categories:** 2× SAFETY_CRITICAL_LANGUAGE, 2× REGULATORY_COMPLETENESS
- **Topics:** safeguarding translation fidelity, MAR pre-signing prohibition, EVV GPS/time thresholds, HIQA 3-business-day notification requirement
- **Source:** `RegulatoryProfileSeedData.cs`

### 2.7 Expected live API response for homecare

For `GET /api/regulatory/applicability?sectorKey=homecare` (requires `Learnings.Admin`):

```json
{
  "hasRegulatoryProfile": true,
  "approvedRequirementCount": 15,
  "profileName": "HIQA Regulatory Score"
}
```

Verify this against Development before demo.

---

## 3. Code State Findings per Surface

### 3.1 `GET /api/regulatory/applicability` endpoint

- **File:** [`src/QuantumBuild.API/Controllers/RegulatoryBrowseController.cs:64-100`](../src/QuantumBuild.API/Controllers/RegulatoryBrowseController.cs#L64)
- **Route:** `GET /api/regulatory/applicability?sectorKey=<key>`
- **Auth:** `Learnings.Admin` policy (class-level on `RegulatoryBrowseController`)
- **Response shape:** `RegulatoryApplicabilityDto` — 3 fields: `hasRegulatoryProfile`, `approvedRequirementCount`, `profileName`
- **DTO source:** [`src/.../DTOs/Validation/RegulatoryScoreDtos.cs:79-84`](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DTOs/Validation/RegulatoryScoreDtos.cs#L79)
- **Unknown-sector handling:** returns `hasRegulatoryProfile: false, approvedRequirementCount: 0, profileName: null` — uniform shape, no 404
- **Requirements-pending handling:** `hasRegulatoryProfile: true, approvedRequirementCount: 0, profileName: <ScoreLabel>` — profile exists but count is zero
- **Note:** `profileName` is only populated when exactly one profile matches the sectorKey (`profiles.Count == 1`). Homecare has one HIQA profile, so it will be set.

### 3.2 `useRegulatoryApplicability` hook

- **File:** [`web/src/lib/api/toolbox-talks/use-content-creation.ts:677-687`](../web/src/lib/api/toolbox-talks/use-content-creation.ts#L677)
- **Returns:** `{ data: RegulatoryApplicabilityDto | null | undefined, isLoading: boolean }`
- **Disabled when:** `sectorKey` is null/empty — query not fired
- **Stale time:** 5 minutes (profile counts change rarely)
- **Consumers (exhaustive search):**
  - `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` — sole consumer
  - No other consumers found via grep on `useRegulatoryApplicability`, `/api/regulatory/applicability`, and `RegulatoryApplicabilityDto` across `web/src`
  - The `RegulatoryScorePanel` accesses applicability via a separate path: `history.applicability` from `useRegulatoryScoreHistory` (see §3.5)

### 3.3 Amber banner — `TranslateStep.tsx` (new wizard only)

- **File:** [`web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx:127-151`](../web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx#L127)
- **Used by new wizard:** `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/translate/page.tsx` imports `TranslateStep`
- **Trigger condition:** `sectorApplicability && sectorApplicability.approvedRequirementCount === 0`
- **Two sub-variants:**
  - `hasRegulatoryProfile: false` → "There is no regulatory profile configured for this sector. Translation and scoring will proceed against general criteria, but the compliance checklist will not be available."
  - `hasRegulatoryProfile: true, approvedRequirementCount: 0` → "The regulatory requirements for **[profileName]** haven't been approved yet. Translation and scoring will proceed, but the compliance checklist will be empty until requirements are reviewed in Regulatory → System."
- **Does it block proceeding?** No — both variants say "will proceed". It is informational only.
- **For homecare (15 approved requirements):** banner does NOT appear. No visible regulatory signal at translate step.
- **Old/legacy wizard:** `web/src/features/toolbox-talks/components/create-wizard/steps/TranslateStep.tsx` — a SEPARATE component. Does NOT import `useRegulatoryApplicability`. No amber banner. Confirmed by inspection.

### 3.4 Sector selection step (old and new wizard)

- **New wizard InputConfigStep:** [`web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx`](../web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx) — sector rendered as a static display field (single-sector tenants) or dropdown (multi/no-sector tenants). No regulatory applicability check. No banner. No affordance beyond the picker itself.
- **Old wizard:** same — sector picker with no regulatory signal.
- **Verdict:** The §1.2.2 banner relocated to translate step (confirmed). Sector selection step has no regulatory display in either wizard. This is expected per BACKLOG description.

### 3.5 `RegulatoryScorePanel` — four states confirmed

- **File:** [`web/src/features/toolbox-talks/components/RegulatoryScorePanel.tsx`](../web/src/features/toolbox-talks/components/RegulatoryScorePanel.tsx)
- **Used on:** validation run detail page [`web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/validation/[runId]/page.tsx`](../web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/validation/[runId]/page.tsx) — rendered as the final card after all section results
- **Applicability data source:** `useRegulatoryScoreHistory(talkId, runId)` → `GET /api/toolbox-talks/validation-runs/{runId}/regulatory-score/history` → `RegulatoryScoreHistoryDto.Applicability` (populated by `RegulatoryScoreService.GetScoreHistoryAsync` using the same logic as the dedicated endpoint)
- **`RegulatoryScoreHistoryDto` source:** [`src/.../DTOs/Validation/RegulatoryScoreDtos.cs:59-73`](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DTOs/Validation/RegulatoryScoreDtos.cs#L59)

**Four render states (exactly four, conditions verified):**

| State | Trigger condition | What user sees |
|-------|------------------|----------------|
| **No sector** | `sectorKey === null` (run has no sector) | Amber header alert in CardHeader: "No sector configured for this run." All three score buttons disabled. Tooltip on Regulatory button: "No sector configured." |
| **No profile** | `sectorKey` set AND `history.applicability.hasRegulatoryProfile === false` | Amber callout inside Regulatory column: "No regulatory profile exists for this sector. Scoring is not available…" Button disabled. |
| **Requirements pending** | `hasRegulatoryProfile: true`, `approvedRequirementCount: 0`, no existing score | Amber callout inside Regulatory column: "Requirements for **[profileName]** are pending approval. Scoring works, but compliance checklist will be empty…" Button **enabled** — scoring is allowed. |
| **Ready / scored** | `score` is non-null (scoring has been run) OR requirements exist and no warnings | Score card with numeric value, verdict badge, category breakdown with weighted progress bars, delta indicator if re-run, expandable full findings. Green "Approved for Distribution" banner appears additionally if `verdict === "APPROVED FOR DISTRIBUTION"` (disables re-run). |

**For Homecare demo:**
- `sectorKey = "homecare"` (set on validation run)
- `hasRegulatoryProfile: true, approvedRequirementCount: 15` → no warning callout
- On first load (before scoring): button shows "Calculate Homecare Score" or "Calculate HIQA Score"
- Column header shows "HIQA REGULATORY SCORE" once a score exists

**Condition triggering "Requirements pending" vs "No profile":**
The panel reads `history.applicability` from the score history endpoint, not from `useRegulatoryApplicability` directly. The score history endpoint (`RegulatoryScoreService.GetScoreHistoryAsync`) runs identical logic — same `RegulatoryApplicabilityDto` shape.

### 3.6 Training Evidence Pack

- **Frontend entry:** `POST /api/toolbox-talks/requirement-mappings/compliance/{sectorKey}/generate-report` via `useGenerateInspectionReport()` — triggered from `GenerateReportDialog` component
- **Page:** `/admin/regulatory/compliance` — "Generate Training Evidence Pack" dialog button
- **Backend:** [`src/.../Services/Mapping/InspectionReportService.cs`](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Mapping/InspectionReportService.cs)
- **Homecare supported:** YES — HIQA profile maps to homecare sector, export label "HIQA Inspection Export"
- **Sector-specific appendix ("Other Records Required"):** EXISTS only for `sectorKey == "food"`. Homecare does not get this appendix section.
- **What the homecare report contains:** Cover page → Executive summary → Coverage by Principle → Requirement detail pages (all 15 HIQA requirements grouped by principle) → Declaration page → Disclaimer. No "other records" appendix.

### 3.7 `/admin/regulatory/regulations` browse page

- **File:** [`web/src/app/(authenticated)/admin/regulatory/regulations/page.tsx`](../web/src/app/(authenticated)/admin/regulatory/regulations/page.tsx)
- **Auth gate:** `Learnings.Admin`
- **Data source:** `useBrowsableRequirements()` → `GET /api/regulatory/browse` — filtered to **tenant's assigned sectors**
- **What it shows for homecare tenant:** All 15 HIQA requirements grouped by body → document → principle → individual requirement cards (title, priority badge, section info, description)
- **Prerequisite:** Tenant must have the homecare sector assigned. The page is empty if no sectors assigned.

---

## 4. Click-Path Walk

Boss is demoing as: SuperUser creates demo tenant → assigns Homecare sector → Admin user creates lesson → translates → validates → scores.

| Step | Surface | File | What user sees (Homecare data state) | Gaps |
|------|---------|------|---------------------------------------|------|
| 1 | **Sector picker — tenant setup** `/admin/tenants` or sector assignment | `TenantSectors` API | Dropdown/list shows "Homecare 🏠" with key `homecare`. All 6 sectors visible (construction, homecare, manufacturing, transport, food_hospitality, healthcare). | None — sector seeded and active. |
| 2 | **Lesson creation — sector config step** (InputConfigStep, step 1 of new wizard) | `InputConfigStep.tsx` | Sector displayed as static field (single-sector tenant) or dropdown. No regulatory signal here in either wizard. | No regulatory affordance at this step by design (banner was moved to translate step per §1.2.2). |
| 3 | **Translate step** (new wizard only) | `TranslateStep.tsx` (learning-wizard) | For homecare with 15 approved requirements: **no amber banner appears**. Step loads cleanly. Translation and validation proceed. | If using legacy wizard (`UseNewWizard = false`, the default): no banner either — but legacy wizard's translate step doesn't call the applicability hook at all. Functionally identical output for clean homecare data. |
| 4 | **Validation run detail page** | `talks/[id]/validation/[runId]/page.tsx` | RegulatoryScorePanel rendered at bottom of page. Header: no amber alert (sectorKey set). Regulatory column: no pending-requirements callout (15 approved). Button reads "Calculate Homecare Score" or "Calculate HIQA Score". Source and Pure score buttons also available. | RegulatoryScorePanel depends on `run.sectorKey` being non-null. **Unverified for legacy wizard** — if legacy wizard creates runs without setting SectorKey, the panel shows "No sector configured" state. New wizard sets SectorKey reliably. |
| 5 | **Run regulatory score** | `RegulatoryScorePanel.tsx` | After clicking "Calculate HIQA Score": spinner, then scored result appears — numeric score, verdict badge (e.g., "APPROVED FOR DISTRIBUTION"), 6 weighted category bars, summary text, HIQA regulatory body label, run label. Re-run button remains unless verdict is "APPROVED FOR DISTRIBUTION". | Score depends on live Claude Sonnet call — API key and network connectivity must be available on demo day. |
| 6 | **Training Evidence Pack** | `/admin/regulatory/compliance` | "Generate Training Evidence Pack" button in dialog. Generates PDF: cover page, executive summary, coverage pie, requirement detail (15 HIQA requirements), declaration, disclaimer. No homecare-specific appendix. | Missing "Other Records Required" appendix section that food sector has. For an HIQA demo this could look incomplete vs a food sector demo. Boss should be briefed. |
| 7 | **Regulatory browse** (optional) | `/admin/regulatory/regulations` | Lists all 15 HIQA requirements grouped by body → document → principle. Shows priority badges, section refs, full descriptions. | Requires homecare sector assigned to demo tenant. Empty page otherwise. |

---

## 5. Gaps

### Data setup needed

| Gap | Severity | Detail |
|-----|----------|--------|
| Development DB not yet confirmed | **Medium** | Seed data exists in code; must verify Development DB applied it. Run `GET /api/regulatory/applicability?sectorKey=homecare` and confirm `approvedRequirementCount: 15`. If zero, migrations or seeder did not run — restart API and recheck. |
| Demo tenant must have Homecare sector assigned | **Medium** | The regulations browse page and compliance checklist are empty until the demo tenant has the homecare sector linked via `POST /api/tenants/{id}/sectors`. Assign before demo walkthrough. |

### Code change needed

None. All described features exist and function correctly at HEAD.

### Acceptable for demo as-is

| Item | Notes |
|------|-------|
| No amber banner at translate step | For homecare with 15 approved requirements, correct behavior — banner only fires when data is missing. No user-visible gap. |
| No regulatory signal at sector selection step | By design; banner moved to translate step per §1.2.2. |
| Evidence Pack missing HIQA-specific appendix | Only food sector has a "other records required" appendix. Homecare report still generates with full requirement detail. Acceptable unless boss specifically wants to show a "what else to prepare" section. |
| `RegulatoryScorePanel` shows three score columns | Source Document, Pure Translation, and Regulatory Translation columns all appear. Source/Pure scoring is sector-independent; Regulatory is HIQA-gated. Three columns visible even on first load. |

### Unverified (resolve by manual click-through before demo)

| Item | Risk | How to verify |
|------|------|---------------|
| Legacy wizard sets `SectorKey` on validation run | **High if boss uses default wizard** | Create a lesson via legacy wizard, run a validation, open the run detail page, check whether RegulatoryScorePanel shows "No sector configured" amber alert or shows the Regulatory score button enabled. |
| `UseNewWizard` toggle setting for demo tenant | **Medium** | Check via Settings → General tab. If boss demos a lesson creation end-to-end, using the new wizard guarantees the translate step uses the §1.2.2 code path. |

### Out of scope for demo

- The corpus audit controls, pipeline change records, and deviation tracking are invisible during the demo path.
- The HIQA regulatory requirements ingestion UI (`/admin/regulatory/system`) — data is already seeded, so this page only matters if boss needs to show the "how data gets in" story.

---

## 6. Recommended Next Steps

**Today (2026-06-25) — data confirmation (10 minutes):**

1. Start Development API, log in as SuperUser.
2. Hit `GET /api/regulatory/applicability?sectorKey=homecare` — expect `{ hasRegulatoryProfile: true, approvedRequirementCount: 15, profileName: "HIQA Regulatory Score" }`. If count is 0, restart the API to force seeder re-run.
3. Confirm demo tenant exists with Homecare sector assigned — or create one and assign the sector now.

**Today — wizard path decision (5 minutes):**

4. Either (a) set `UseNewWizard = true` for the demo tenant in Settings → General, or (b) do a manual click-through of the legacy wizard end-to-end: create a lesson, reach the validation run detail page, confirm RegulatoryScorePanel shows the Regulatory score button enabled (not "No sector configured").

**Before going live — Evidence Pack awareness:**

5. Brief the boss that the homecare Evidence Pack PDF does not have an "Other Records Required" appendix (that's a food-sector-specific section). The report still generates with full HIQA requirement coverage. If a homecare appendix is needed for the demo, that's a code change — flag as a follow-up item, not a demo blocker.

---

## Appendix: Endpoint and file quick-reference

| Artifact | Path |
|----------|------|
| `RegulatoryApplicabilityDto` | `src/Modules/ToolboxTalks/.../DTOs/Validation/RegulatoryScoreDtos.cs:79` |
| `RegulatoryScoreHistoryDto` (embeds applicability) | `src/Modules/ToolboxTalks/.../DTOs/Validation/RegulatoryScoreDtos.cs:59` |
| `GET /api/regulatory/applicability` | `src/QuantumBuild.API/Controllers/RegulatoryBrowseController.cs:64` |
| `GET /api/toolbox-talks/validation-runs/{runId}/regulatory-score/history` | `src/QuantumBuild.API/Controllers/RegulatoryScoreController.cs:92` |
| `useRegulatoryApplicability` hook | `web/src/lib/api/toolbox-talks/use-content-creation.ts:677` |
| `useRegulatoryScoreHistory` hook | `web/src/lib/api/toolbox-talks/use-content-creation.ts:636` |
| Amber banner (new wizard only) | `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx:127` |
| Legacy wizard translate step (no banner) | `web/src/features/toolbox-talks/components/create-wizard/steps/TranslateStep.tsx` |
| `RegulatoryScorePanel` | `web/src/features/toolbox-talks/components/RegulatoryScorePanel.tsx` |
| Validation run detail page | `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/validation/[runId]/page.tsx` |
| Evidence Pack backend | `src/Modules/ToolboxTalks/.../Services/Mapping/InspectionReportService.cs` |
| `/admin/regulatory/regulations` page | `web/src/app/(authenticated)/admin/regulatory/regulations/page.tsx` |
| Sector seed data | `src/Modules/ToolboxTalks/.../Persistence/Seed/SectorSeedData.cs` |
| Homecare requirements seed | `src/Modules/ToolboxTalks/.../Persistence/Seed/RegulatoryRequirementSeedData.cs` |
