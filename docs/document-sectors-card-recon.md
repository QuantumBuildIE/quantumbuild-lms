# Document Sectors Card — Wiring Recon

**Date:** 2026-07-31
**Status:** Read-only recon. Facts only, no design, no code changes.
**Branch:** `transval`, HEAD `4bbceb8` (working tree even with both `origin/transval` and `company/transval` — confirmed via `git status -sb` showing `## transval...origin/transval` with no ahead/behind marker, and `git log --oneline -3` identical across local, `origin/transval`, `company/transval`).

---

## Headline finding

**The sector-attach chain, as it exists in the current code, is fully wired end-to-end.** Every link the trigger prompt asked me to check — `onChange` → state, button `onClick` → handler, handler → mutation, mutation → API client, API client → backend route, backend route → DTO shape — is present, connected, and internally consistent. I did not find any of the four candidate break-shapes (selection not captured / handler missing / mutation not invoked / handler present but stubbed) in the code as it stands today.

This directly contradicts the bug report's premise ("selecting a sector fires no network request," "the frontend control appears to be unwired"). The likely explanation, stated as an observation rather than a diagnosis, is timing: the entire feature — `DocumentSectorsCard`, the hook, the API client, and the backend endpoint — was added in a single commit, `6ed10c1` ("Add sector-to-document attachment via RegulatoryProfile creation"), authored **2026-07-30 15:50 +0100**, which is the second-most-recent commit on this branch (one commit, `4bbceb8`, unrelated to this area, sits after it). If the "zero profile rows in the live DB" observation was made against a deployed build that predates this commit, the code that would have produced those rows simply didn't exist yet at observation time. See "Deployment timing" section at the end for exact evidence.

---

## 1. Full trace of `DocumentSectorsCard` wiring

Component defined in `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:508-608`.

**Sector `<select>` onChange → state capture** (confirmed, not stubbed):
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:517` — `const [selectedSectorId, setSelectedSectorId] = useState("");`
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:576-579`:
  ```
  <Select
    value={selectedSectorId}
    onValueChange={setSelectedSectorId}
  >
  ```
  `onValueChange` is bound directly to the state setter — every sector pick updates `selectedSectorId`. This is the shadcn/ui `Select` (Radix-based) pattern used identically elsewhere in the codebase (e.g. the priority `Select` in the same file at line 414-417); it is a controlled combobox, not a native `<select>`, so it never fires a network request on selection by design — only the follow-on button click should. The bug report's phrasing ("selecting a sector fires no network request") describes expected behavior for this component shape, not a defect, unless the reporter's actual observation was about clicking "Add Sector" and nothing happening.

**"Add Sector" button onClick → handler binding** (confirmed, not stubbed):
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:593-603`:
  ```
  <Button
    onClick={handleAddSector}
    disabled={!selectedSectorId || createProfile.isPending}
  >
  ```
  `onClick` is bound to `handleAddSector`, a real function reference (not `undefined`, not a no-op).

**Handler → mutation invocation** (confirmed, not a no-op):
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:523-548`:
  ```
  const handleAddSector = useCallback(() => {
    if (!selectedSectorId) return;
    createProfile.mutate(
      { sectorId: selectedSectorId },
      { onSuccess: ..., onError: ... }
    );
  }, [createProfile, selectedSectorId]);
  ```
  `handleAddSector` calls `createProfile.mutate(...)` with a real payload `{ sectorId: selectedSectorId }`. It is not a `console.log`, not commented out, not an empty function body.

**Mutation object source:**
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:516` — `const createProfile = useCreateRegulatoryProfile(documentId);`

**Component invocation on the page (confirms it's actually rendered, not orphaned):**
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:831-834`:
  ```
  <DocumentSectorsCard
    documentId={documentId}
    sectorKeys={currentDocument?.sectorKeys ?? []}
  />
  ```
  This sits inside the authenticated page's returned JSX tree, after the "Document Details & Ingestion" card and before the "Draft Requirements" card — unconditionally rendered (no feature flag, no conditional wrapper) whenever the outer `isSuperUser` gate at line 681-687 passes.

**Conclusion for §1:** no break in this chain. Selection is captured, the button is bound, the handler is real and calls the mutation with the correct payload shape.

---

## 2. Are `useCreateRegulatoryProfile` / `createRegulatoryProfile` imported and called, or defined-but-unused?

**Imported and called — not dead code.**

- Hook import: `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:16` — `useCreateRegulatoryProfile,` inside the `import { ... } from "@/lib/api/admin/use-regulatory-ingestion"` block (lines 6-17).
- Hook call: `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:516` — `useCreateRegulatoryProfile(documentId)`.
- Hook definition: `web/src/lib/api/admin/use-regulatory-ingestion.ts:213-225`:
  ```
  export function useCreateRegulatoryProfile(documentId: string) {
    const queryClient = useQueryClient();
    return useMutation({
      mutationFn: (data: CreateRegulatoryProfileRequest) =>
        createRegulatoryProfile(documentId, data),
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: regulatoryKeys.documents() });
        queryClient.invalidateQueries({
          queryKey: regulatoryKeys.ingestionStatus(documentId),
        });
      },
    });
  }
  ```
  `mutationFn` is wired to the real API client function, not a stub.
- `createRegulatoryProfile` import into the hooks file: `web/src/lib/api/admin/use-regulatory-ingestion.ts:16` (inside the import block from `./regulatory-ingestion`, lines 2-17).
- `createRegulatoryProfile` definition: `web/src/lib/api/admin/regulatory-ingestion.ts:57-66`:
  ```
  export async function createRegulatoryProfile(
    documentId: string,
    data: CreateRegulatoryProfileRequest
  ): Promise<RegulatoryProfileDto> {
    const response = await apiClient.post<RegulatoryProfileDto>(
      `/regulatory/documents/${documentId}/profiles`,
      data
    );
    return response.data;
  }
  ```
  This issues a real `apiClient.post` (the same shared axios instance used by every other working mutation in this file, e.g. `startIngestion` at lines 68-77, `approveRequirement` at lines 97-106) — not a mock, not a `Promise.resolve()` stub.

**Conclusion for §2:** both are imported and called from exactly one call site each (`DocumentSectorsCard`), and neither is defined-but-unused.

---

## 3. "Add Sector" button disabled/enabled state — can a click ever do nothing?

Governing expression: `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:595`:
```
disabled={!selectedSectorId || createProfile.isPending}
```

Two conditions disable the button:
- `!selectedSectorId` — true when nothing has been picked from the `Select` yet (initial state, line 517, is `""`).
- `createProfile.isPending` — true while a create-profile request is in flight (TanStack Query mutation pending state), preventing double-submit.

The `Select`'s option list itself is filtered to sectors not already attached: `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:519-521`:
```
const availableSectors = (sectors ?? []).filter(
  (sector) => !sectorKeys.includes(sector.key)
);
```
rendered into `SelectItem`s at lines 584-588. If every sector is already attached, `availableSectors` is `[]`, the dropdown offers nothing to pick, `selectedSectorId` stays `""`, and the button stays disabled — no path to an enabled-but-no-op click via this route.

The handler itself carries a second, redundant guard: `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:524` — `if (!selectedSectorId) return;` inside `handleAddSector`. This means even in a hypothetical state where the `disabled` prop were bypassed (e.g. a stale render), the handler body itself still no-ops without a selection. There is no code path where the button is both clickable and `selectedSectorId` is falsy at click time — the two checks (disabled-prop and in-handler-guard) are redundant, not contradictory.

**Conclusion for §3:** the disabled logic is correctly gated to selection state and in-flight-request state; no dead-click state exists in the current code.

---

## 4. Ingest Requirements wiring — sector/profile independence

**Confirmed: `handleStartIngestion` sends only `sourceUrl`, no sector or profile identifier of any kind.**

- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:645-660`:
  ```
  const handleStartIngestion = useCallback(() => {
    startIngestion.mutate(
      { sourceUrl: effectiveSourceUrl },
      { onSuccess: ..., onError: ... }
    );
  }, [startIngestion, effectiveSourceUrl]);
  ```
  `effectiveSourceUrl` is derived at line 640-641 purely from the `sourceUrl` text input state and/or the previously-fetched ingestion status's `sourceUrl` field — no reference to `selectedSectorId`, `sectorKeys`, or any profile ID anywhere in this function or its dependency array.
- The mutation's request type, `StartIngestionRequest`, is passed straight through by `startIngestion()` in `web/src/lib/api/admin/regulatory-ingestion.ts:68-77`, whose only body field is `sourceUrl` (confirmed by the call-site literal `{ sourceUrl: effectiveSourceUrl }` above — no other field is ever constructed for this request anywhere in the file).

**Confirmed: nothing on the page gates the Ingest Requirements button on profile/sector existence.**
- The button's `disabled` expression, `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:781-785`:
  ```
  disabled={
    startIngestion.isPending ||
    isPolling ||
    sourceUrlIssue?.level === "error"
  }
  ```
  Three conditions only: in-flight ingestion mutation, active polling, or a client-side URL-format validation error (`checkSourceUrlInput`, lines 77-111). None of these three reference `currentDocument?.sectorKeys`, `createProfile`, or any profile-related state. A document with zero attached sectors can have "Ingest Requirements" clicked freely from the frontend's perspective.

**Whether this is a missing frontend gate or an intentionally independent design:** the commit that added the sectors card, `6ed10c1`, states this explicitly in three places, confirming it as deliberate design rather than an oversight:
- Backend controller doc comment, `src/QuantumBuild.API/Controllers/RegulatoryIngestionController.cs:142-144`: `"Attach a sector to a regulatory document by creating a RegulatoryProfile. Restores a previously soft-deleted profile for the same document/sector pair. Does NOT trigger ingestion — that remains a separate explicit action."`
- The commit message itself: `"Profile creation is inert - it triggers no ingestion, pinned by test."`
- `docs/regulatory-profile-creation-recon.md:321-327` (the recon that preceded this feature): `"Profile creation is not wired to trigger anything. ... even in the current design intent, adding a profile is explicitly meant to be inert with respect to the ingestion pipeline, a pure prerequisite step."`

The enforcement of "a profile must exist before ingestion can *succeed*" happens server-side, at ingest time, not client-side as a pre-click gate: per `docs/regulatory-profile-creation-recon.md:329-336`, `RequirementIngestionJob` checks for an active profile when it has extracted requirements to attach, and calls `MarkSkippedAsync(document, "no_active_profiles", ...)` if none exists — i.e. clicking Ingest Requirements with zero sectors attached is allowed by the UI and will run, but the backend job will skip persisting results with a `no_active_profiles` status surfaced back through `IngestionSessionDto.lastIngestionErrorCode` (rendered by `describeIngestionError` at page.tsx:118-133, though note that function's current `switch` only special-cases `invalid_uri`/`fetch_failed`/`parse_failed` — `no_active_profiles` would fall through to the `default` branch and show the raw `errorMessage` text rather than a friendly explanation, per lines 130-131).

**Conclusion for §4:** ingest and profile-attachment are two independent, separately-triggered actions by design, not an accidentally-missing frontend gate. The one soft gap is cosmetic: `no_active_profiles` isn't in `describeIngestionError`'s known-code switch, so a user who ingests without attaching a sector first sees a generic fallback message rather than a friendly "attach a sector first" explanation.

---

## 5. Does the detail page show the document's existing attached profiles/sectors, and is it live data?

**Yes, and it reads live data — not stubbed.**

- Display markup: `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:556-568`:
  ```
  <div className="flex flex-wrap gap-2">
    {sectorKeys.length === 0 ? (
      <p className="text-sm text-muted-foreground">No sectors attached yet.</p>
    ) : (
      sectorKeys.map((key) => (
        <Badge key={key} variant="outline">{key}</Badge>
      ))
    )}
  </div>
  ```
- `sectorKeys` is a prop passed in from the parent at line 833: `sectorKeys={currentDocument?.sectorKeys ?? []}`.
- `currentDocument` is derived at lines 634-635:
  ```
  const { data: documents } = useRegulatoryDocuments();
  const currentDocument = documents?.find((d) => d.id === documentId);
  ```
  `useRegulatoryDocuments()` (`web/src/lib/api/admin/use-regulatory-ingestion.ts:40-46`) is a real `useQuery` hitting `getRegulatoryDocuments()` (`web/src/lib/api/admin/regulatory-ingestion.ts:21-26`), which `GET`s `/regulatory/documents` — the same live endpoint documented in CLAUDE.md's Regulatory Ingestion table. This is not mock/placeholder data.
- The badge list is kept fresh after a successful add: `useCreateRegulatoryProfile`'s `onSuccess` (`web/src/lib/api/admin/use-regulatory-ingestion.ts:218-219`) calls `queryClient.invalidateQueries({ queryKey: regulatoryKeys.documents() })`, which is the exact query key `useRegulatoryDocuments()` uses (line 42) — a successful "Add Sector" click triggers a refetch of `currentDocument.sectorKeys`, so the newly attached sector's badge appears without a manual page reload.

**Conclusion for §5:** the existing-attachments display is present, reads from the live `/regulatory/documents` endpoint (the same data source that already fed the list page's Sectors column, per `docs/regulatory-profile-creation-recon.md:257`), and is kept in sync with successful adds via query invalidation.

---

## Deployment timing (context for the discrepancy, not a diagnosis)

- `git log --oneline -3` is byte-identical across local `transval`, `origin/transval`, and `company/transval` — the commit that built this entire feature is present in both remotes, not sitting unpushed locally.
- `6ed10c1` ("Add sector-to-document attachment via RegulatoryProfile creation") is timestamped **2026-07-30 15:50 +0100** — one day before this recon (today is 2026-07-31 per system date).
- That single commit is the *entire* history of this feature: it introduces `DocumentSectorsCard`, `useCreateRegulatoryProfile`, `createRegulatoryProfile`, `CreateRegulatoryProfileRequest`/`RegulatoryProfileDto` types, the backend `CreateProfile` action, `IRequirementIngestionService.CreateProfileAsync`, and its test file — confirmed via `git show 6ed10c1 --stat` (10 files changed, 952 insertions, 0 deletions across frontend, backend, DTOs, and tests; no file in that diff was touched by any earlier commit in the same area).
- The one commit after it, `4bbceb8` ("Report all failed principle segments, not just the first"), touches only `RequirementIngestionJob.cs`, its test file, a test fake, and a recon doc — none of the files involved in sector-attach wiring (confirmed via `git show 4bbceb8 --stat`).
- No other commit, branch, or duplicate file defines a second/older version of `DocumentSectorsCard`, `useCreateRegulatoryProfile`, or `createRegulatoryProfile` — `Grep` for each identifier across the repo returns exactly the call sites and definitions cited above, nothing else.
- The only other route touching this `documentId` is a redirect stub: `web/src/app/(authenticated)/admin/regulatory/[documentId]/page.tsx:1-9` unconditionally `redirect()`s to `/admin/regulatory/system/${params.documentId}` — it does not render its own UI, so it cannot be a second, unwired copy of this page being hit instead.

If the reported symptom ("zero profile rows in the live DB," "no network request," "no sector in the ingest payload") was observed against a running instance, the only way to reconcile it with the code above is that the observed instance was running a build older than `6ed10c1` — before this capability existed at all — rather than the current HEAD. This report does not attempt to determine which deployed environment (Development/Production) was actually observed, since that is outside a read-only source-code recon.

---

## Non-scope confirmation

No code was modified. No backend investigation was performed beyond reading the already-cited `RegulatoryIngestionController.cs` route/DTO shape to confirm frontend/backend payload compatibility, and the pre-existing `docs/regulatory-profile-creation-recon.md`. Extraction count/quality was not investigated.
