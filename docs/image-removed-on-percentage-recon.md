# Recon: Image removed from lesson when watch percentage is selected

Status: **READ-ONLY recon — no code changed.**

## Summary (read this first)

Two wizards exist for creating a learning (Note 29 in `CLAUDE.md`), and **both** put
a cover-image field and a "Minimum video watch percentage" field on the same
Settings step — but they store the image completely differently:

| Wizard | Cover image storage | Watch % storage |
|---|---|---|
| **Legacy / Create wizard** (`/admin/toolbox-talks/create`, default — `UseNewWizard=false`) | `ContentCreationSession.SettingsJson` (a JSON blob covering *all* settings fields) | Same blob |
| **New / Learning wizard** (`/admin/toolbox-talks/learnings/**`) | `ToolboxTalk.CoverImageUrl` column, written via a **dedicated** talk-level endpoint | `ToolboxTalk.MinimumVideoWatchPercent` column, written via a separate "update settings" endpoint |

Two distinct bugs were found, both centred on this same surface. **Bug 1 is
confirmed and deterministic** (always reproduces, doesn't need any specific
timing). **Bug 2 is plausible but timing-dependent** and would better explain a
user's literal "I clicked the percentage and the image vanished" experience —
it needs live reproduction to confirm.

- **Bug 1 (confirmed, server-side, legacy wizard only):** the cover image
  uploaded during the legacy Create wizard is stored only in the ephemeral
  session's `SettingsJson` blob and is **never copied onto the published
  `ToolboxTalk.CoverImageUrl` column**. Every learning created through the
  legacy wizard silently loses its cover image at publish time, regardless of
  whether the user ever touches the watch-percentage field.
- **Bug 2 (plausible, client-side race, legacy wizard):** the settings
  autosave is a full-object, last-write-wins overwrite of the same JSON blob.
  If the user changes *any other* field (e.g. clicks a watch-% preset) before
  the image-upload's own async completion has updated the shared in-memory
  settings object, the debounced autosave can push a stale (pre-upload,
  `coverImageUrl: null`) snapshot to the server and durably erase the image
  from the session record — even though the wizard UI itself may still show
  the image (see Part 2 for why this is easy to miss visually).
- The **new wizard's** persistence layer is architecturally immune to Bug 1
  (image and settings are separate DB columns, and the settings-update
  handler never touches `CoverImageUrl`). A narrower, third-order client-cache
  race is theoretically possible there too but requires genuinely overlapping
  requests, not just fast sequential clicks — see Part 2c.

---

## Part 1 — Locating the surface

Grepping the frontend for image-upload fields and percentage-based fields
associated with lesson creation turned up exactly one surface with both,
duplicated across the two wizards:

- **Legacy wizard:** `web/src/features/toolbox-talks/components/create-wizard/steps/SettingsStep.tsx`, composed of:
  - `settings/TitleDescriptionPanel.tsx` — Title, Description, **Cover Image** (drag/drop or click-to-browse)
  - `settings/BehaviourPanel.tsx` — Active on Publish, Generate Certificate, **Minimum Watch Percentage**, Auto-assign
- **New wizard:** `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`, with:
  - A "Cover Image" section (`4b`) rendering `components/CoverImageUpload.tsx`
  - A "Behaviour" section (`4c`) containing the react-hook-form field `minimumWatchPercent`

No other surface (section editor, slideshow config, talk-level thumbnail
outside these wizards, video config) combines an image field with a
percentage field. The talk-level thumbnail *is* the "cover image" described
above — there is no separate concept.

Other percentage fields exist (quiz passing score, subtitle/translation
progress) but none of them share a form/step with an image field, so they are
ruled out.

**Confirmed: the surface is the Settings step (step 4) of either wizard.**
Given `UseNewWizard` defaults to `"false"` (Note 29), most tenants are on the
**legacy wizard**, which is also the one with the confirmed server-side bug —
so it is the most likely candidate for what the user hit.

---

## Part 2 — Tracing the interaction

### 2a. Legacy wizard — form model

- No `react-hook-form`/Formik here — a single local `useState<ContentCreationSettings>`
  object (`settings`) in `SettingsStep.tsx`, passed down as a prop to each
  panel (`TitleDescriptionPanel`, `CategoryPanel`, `RefresherPanel`,
  `BehaviourPanel`, `SlideshowPanel`). Every panel's `onChange` is really the
  same `handleChange` callback, called with a **spread of the whole object**:
  `onChange({ ...settings, someField: newValue })`.
- The cover image is one field in that same object: `settings.coverImageUrl`.
- `handleChange` immediately does `setSettings(newSettings)` and schedules a
  **500ms debounced** autosave: `updateSettings.mutate({ sessionId, settings: newSettings })`.
- Image upload does **not** go through `handleChange`. `TitleDescriptionPanel`
  calls `onUploadCoverImage(file)` → `handleUploadCoverImage` → its own
  mutation (`uploadCoverImage.mutate`) hitting a **separate endpoint**
  (`POST /content-creation/sessions/{id}/cover-image`). On success, it patches
  local state with a **functional updater**: `setSettings(prev => ({...prev, coverImageUrl}))`.

### 2b. What happens server-side — this is where Bug 1 lives

- `ContentCreationSessionService.UpdateSettingsAsync` (the handler behind the
  debounced autosave) — [ContentCreationSessionService.cs:1234-1249](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1234-L1249):

  ```csharp
  public async Task<ContentCreationSessionDto> UpdateSettingsAsync(...)
  {
      var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);
      session.SettingsJson = JsonSerializer.Serialize(settings, CamelCaseJson);   // full overwrite
      await _dbContext.SaveChangesAsync(cancellationToken);
      ...
  }
  ```

  This **blindly replaces the entire `SettingsJson` blob** with whatever
  `SessionSettingsDto` the client sent — there is no per-field patch. Whatever
  value of `CoverImageUrl` happens to be in the client's payload at that
  moment becomes the new persisted truth, wiping any previously-saved value
  that the payload didn't know about.

  Contrast with `UploadCoverImageAsync` — [ContentCreationSessionService.cs:1251-1289](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1251-L1289) — which correctly does a **read-current, patch-one-field, write-back**:

  ```csharp
  var settings = ... existing SettingsJson, deserialized ...
  settings = settings with { CoverImageUrl = result.PublicUrl };
  session.SettingsJson = JsonSerializer.Serialize(settings, CamelCaseJson);
  ```

  Only `UpdateSettingsAsync` (the one triggered by *every other field*,
  including the watch-percentage preset buttons) has the blind-overwrite
  pattern.

- **The bigger finding:** grepping the entire backend for every place
  `ToolboxTalk.CoverImageUrl` is *assigned* (not just read) turns up exactly
  two lines, both in `ToolboxTalkFilesController.cs` (`.CoverImageUrl = result.PublicUrl`
  and `.CoverImageUrl = null`) — the **talk-level** direct upload/remove
  endpoints used only by the **new** wizard.
  **`SessionSettingsDto.CoverImageUrl` is never read back out anywhere** when
  the legacy wizard syncs session settings onto the draft/published
  `ToolboxTalk` entity. The explicit "apply behaviour fields from session
  settings" block that copies `IsActiveOnPublish`, `GenerateCertificate`,
  `MinimumWatchPercent`, `AutoAssign`, `AutoAssignDueDays` onto `draftTalk` —
  [ContentCreationSessionService.cs:1443-1452](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1443-L1452) — conspicuously has **no**
  `CoverImageUrl` line. The same is true at every other draft/publish
  construction site in that file (confirmed by exhaustive grep).

  **This means: for the legacy wizard, the cover image the user sees in the
  wizard is never connected to the published learning at all.** This is a
  deterministic, always-reproducing bug independent of the watch-percentage
  field — it would happen even if the user touched nothing else after
  uploading the image.

### 2c. Why the watch-percentage click specifically might appear to "cause" it

Given Bug 1 exists regardless, the user's specific mention of the percentage
field is most plausibly explained by one of:

1. **Reporting artifact** — the percentage field is simply the last thing the
   user touches before moving on (it's the very next control below the Cover
   Image section in both wizards' layouts — section `4b` then `4c`), so it's
   the action they associate with the image "disappearing" when in fact the
   image was never going to survive to publish regardless.
2. **A genuine, narrower client-side race** (Bug 2): if the user clicks a
   watch-% preset **while the image upload is still in flight** (before its
   "Cover image uploaded" toast), the settings spread captured at that click
   (`{...settings, minimumWatchPercent: pct}`) carries the **pre-upload**
   `coverImageUrl` (still `null`). `setSettings(newSettings)` stores that
   locally, and 500ms later the debounced autosave persists it — durably
   erasing the image server-side via the blind-overwrite handler in 2b, on
   top of whatever Bug 1 was already going to do. Whether this is *visibly*
   noticeable in the same session depends on timing: the upload's own
   `onSuccess` uses a functional `setSettings(prev => ...)` update, so if it
   resolves *after* the percentage click, it re-applies the correct URL to
   local state and the wizard UI keeps showing the image — masking the fact
   that the persisted server copy is already wrong. The loss would only
   surface later (resuming the draft, or at publish — where Bug 1 already
   guarantees loss anyway).
3. Genuinely reordering (image upload finishing, *then* well after, clicking
   the percentage) — reviewed and ruled out: the spread would then carry the
   correct (already-uploaded) URL forward, so no additional loss occurs in
   that ordering.

**This part needs live reproduction to confirm** — the code supports #2 as
possible, but it depends on human click timing relative to a network
round-trip, so it isn't as clean a slam-dunk as Bug 1.

### 2d. New wizard — form model (for comparison / completeness)

- Cover image: `react-query` cache key `['learnings', talkId]`, patched via
  `useUploadCoverImage`/`useRemoveCoverImage` with a **partial** functional
  update (`{...prev, coverImageUrl}`) after hitting the dedicated
  `POST/DELETE /toolbox-talks/{id}/cover-image` endpoints.
- Watch %: a `react-hook-form` field (`minimumWatchPercent`), saved via
  `PUT /toolbox-talks/{id}/settings` on every preset click (not debounced),
  whose handler (`UpdateToolboxTalkSettingsCommandHandler`) **does not touch**
  `CoverImageUrl` at all and correctly re-reads/returns the current DB value.
- The mutation's `onSuccess` does `queryClient.setQueryData(['learnings', talkId], data)`
  — a **full replace** of the *same cache entry* the image mutations patch.
  Because the settings handler always re-reads current DB state, this is only
  wrong if the settings PUT's DB read genuinely overlaps the image POST's
  still-uncommitted write (true request concurrency, not just fast
  sequential UI clicks) — a much narrower window than the legacy wizard's
  guaranteed full-blob overwrite. Rated **unlikely** to be the reported bug
  unless the user is on the new wizard (uncommon given the default toggle)
  and reports intermittent/inconsistent recurrence.

---

## Part 3 — Recent history

`git log` on the relevant files shows ongoing wizard work but nothing that
specifically touches cover-image ↔ settings sync:

- Legacy wizard `SettingsStep.tsx` / panels / `ContentCreationSessionService.cs`:
  recent commits are CONTENT-LIFECYCLE cascade-reset work, DOCX import,
  reviewer-edit fixes, quiz/verbatim-parse features — no commit message
  mentions cover image.
- New wizard `SettingsStep.tsx` / `CoverImageUpload.tsx` / `useUpdateTalkSettings.ts`:
  history is the original Phase 5 "rebuild Step 4" work plus a duplicate-title
  fix and visual polish — again nothing cover-image-specific.

This suggests the cover-image-to-published-talk gap (Bug 1) is a **long-
standing, never-noticed gap** in the legacy wizard rather than a recent
regression — cover images were likely added to the legacy wizard's Settings
step by porting the new wizard's UI pattern into the session-based model
without adding the corresponding sync-to-`ToolboxTalk` step that the other
five behaviour fields already had.

---

## Part 4 — Reproduction hints

### To confirm Bug 1 (always-reproduces, no timing needed)
1. Go to `/admin/toolbox-talks/create` (or hit "Create New" with the
   wizard-version toggle left at its default `false`/legacy).
2. Get to Step 4 (Settings). Upload a cover image and wait for the
   "Cover image uploaded" toast.
3. Do **not** touch anything else related to the image; finish the wizard
   normally (Quiz → Settings → Translate & Validate → Publish).
4. Open the published talk's detail/edit page. **Expected finding:** no
   cover image is present, even though it showed correctly throughout the
   wizard session.

### To attempt Bug 2 (timing-dependent, needs fast clicking)
1. Same starting point as above, Step 4.
2. Drag/drop or select a cover image, and **immediately** (within roughly a
   second, before the success toast appears) click a "Minimum Watch
   Percentage" preset button.
3. Watch whether the image preview flickers/disappears in the dropzone at
   that moment, and whether it's still present after a full page reload of
   the same wizard session (this checks whether the *server* copy, not just
   the client state, lost the image).

### If the user is actually on the new wizard
1. Confirm via the `?wizard=new` URL override or checking `Settings → General
   → Wizard Version`, or checking an existing draft's `lastEditedStep` — a
   non-null value indicates the new wizard was used.
2. Same steps as Bug 2 above, on `/admin/toolbox-talks/learnings/**`. This is
   architecturally harder to trigger (needs true request overlap) — if it
   reproduces reliably here, it points to the cache-replacement race in 2d
   rather than Bug 1/2 in the legacy wizard.

**Ask the user directly:** which wizard were they in (legacy "Create" vs. the
newer step-per-URL wizard), and did the image ever survive to the published
learning at all, or only disappear from the in-progress wizard screen? That
answer discriminates between Bug 1 (never survives publish, any wizard path)
and Bug 2/2d (a live, in-session disappearance tied to click timing).

---

## Diagnosis

- **Bug 1 is a server-side persistence bug**, not a display bug: the image
  genuinely never reaches the `ToolboxTalk` row for anything created via the
  legacy wizard. Confirmed by exhaustive grep — `ToolboxTalk.CoverImageUrl` is
  only ever set by the new wizard's dedicated talk-level endpoint.
- **Bug 2 is a client-side race** compounded by a server-side blind-overwrite
  handler (`UpdateSettingsAsync`) — plausible, not yet confirmed live. It
  would make the loss happen earlier (during the session) and be
  triggerable specifically by fast interaction with *any* other settings
  field, watch-percentage included, but isn't specific to percentage alone.
- No evidence found for a pure "hidden but not lost" display bug — the
  session/DB inspection in Part 2b shows the data is actually gone from
  `SettingsJson`/never reaches `ToolboxTalk`, not merely hidden from view.

## Recommended fix scope (not implemented — recon only)

1. **Bug 1 (confirmed):** add `CoverImageUrl` to every place
   `ContentCreationSessionService` syncs `SessionSettingsDto` fields onto a
   `ToolboxTalk`/`draftTalk` entity (at minimum the block at
   `ContentCreationSessionService.cs:1443-1452`, and the standalone-lesson
   creation path around line ~1705-1750 — both need checking together, not
   independently, per this file's usual pattern).
2. **Bug 2 (needs confirmation first):** change `UpdateSettingsAsync` to a
   read-current/patch-changed-fields pattern (matching `UploadCoverImageAsync`'s
   already-correct approach) instead of a blind full-object overwrite, so a
   stale client snapshot of one field can't erase a different field that was
   updated out-of-band. Confirm reproduction before scoping this — it may be
   moot if Bug 1's fix is deployed and no one ever notices Bug 2 independently.
3. Do not conflate the two wizards' fixes — they have separate data models
   and separate code paths; a fix for one will not touch the other.
