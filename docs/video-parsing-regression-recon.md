# Video Parsing Regression — Recon

Read-only investigation. No code changed. All findings are file:line references to the current `transval` branch (HEAD `77f9a3a`), cross-checked against git history for the "recent changes" the incident is suspected to correlate with.

## TL;DR

Neither problem is caused by the Gemini model retirement or by the timestamp-stripping fix. Both problems are caused by the **new "learning wizard" (talk-based) content pipeline** (CLAUDE.md Note 29), which has existed since 2026-06-10 and has two independent, pre-existing defects that only manifest when a video is processed through it instead of the legacy wizard:

1. **Section degradation** — the new wizard defaults `PreserveSourceWording = true` and asks Claude to copy the transcript **verbatim** instead of rewriting it, combined with a hard-coded `minimumSections = 2` (vs. the legacy wizard's 7). For an unstructured spoken transcript this collapses to one big near-verbatim section. There is no AI-failure fallback involved — this is the AI faithfully executing a "don't rewrite" prompt.
2. **Video not displaying** — the new wizard's talk-initialisation and parse handlers never copy an uploaded video file's URL (`SourceFileUrl`) into the `VideoUrl` column that every single frontend surface reads. `VideoUrl` stays `null` for any new-wizard talk created by **uploading** a video file (as opposed to pasting a video URL).

The likely trigger for "why now" is that this talk was routed through the new wizard for the first time (tenant `UseNewWizard` setting flipped to `true`, or the one-shot `?wizard=new` URL override used, or the user navigated directly to `/admin/toolbox-talks/learnings/**`) rather than any of the recent AI-model or timestamp commits. See §D for how to confirm this against the actual failing row.

---

## A. Section-generation degradation

### A1. Two independent section-generation code paths exist

| Path | Entry point | Service | AI model | `PreserveSourceWording` support | Minimum sections |
|---|---|---|---|---|---|
| **Legacy wizard** (session-based, `create-wizard/**`) | `ContentGenerationJob` → `ContentGenerationService.GenerateContentAsync` [ContentGenerationService.cs:179-187](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentGenerationService.cs#L179-L187) | `AiSectionGenerationService` [AiSectionGenerationService.cs:18](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiSectionGenerationService.cs#L18) | Claude (Anthropic) Sonnet | **Not supported at all** — the method signature has no such parameter | Default 7 ([IContentGenerationService.cs:44](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IContentGenerationService.cs#L44), also [ToolboxTalksController.cs:1319](../src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L1319)) |
| **New wizard** (talk-based, `learning-wizard/**`, Note 29) | `ParseToolboxTalkContentCommandHandler.HandleVideoAsync` → `VideoTranscriptionJobForTalk` → `ContentCreationParseJobForTalk` [ContentCreationParseJobForTalk.cs:70-76](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentCreationParseJobForTalk.cs#L70-L76) | `ContentParserService` [ContentParserService.cs:18](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L18) | Claude (Anthropic) Sonnet | **Supported, driven by `talk.PreserveSourceWording`** | Hard-coded **2** ([ContentParserService.cs:83](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L83)) |

Both paths read the same model config key: `IOptions<AIProviderOptions>.Value.Anthropic.Models.Sonnet` ([AiSectionGenerationService.cs:35](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiSectionGenerationService.cs#L35), [ContentParserService.cs:37](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L37)). Currently `"claude-sonnet-4-5"` in checked-in config ([appsettings.json:30](../src/QuantumBuild.API/appsettings.json#L30)) — a live, non-deprecated model, consistent with the earlier P0 fixes (`832abde fix(p0): Anthropic model deprecation`, `8733c86 fix(p0): ElevenLabs unsupported_model`).

**Gemini does not appear anywhere in either path.** The only Gemini consumer in the codebase is `GeminiTranslationService` ([GeminiTranslationService.cs](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/GeminiTranslationService.cs)), used exclusively by `ConsensusEngine` for TransVal back-translation (Round 2 of the multi-round consensus process, per CLAUDE.md). Section/content generation never calls it. **The "leading hypothesis" that section-gen hit a retired Gemini model and fell back to raw transcript is refuted by the code** — there is no such fallback, and there is no Gemini call in this path to fail in the first place.

### A2. No AI-failure fallback to raw transcript exists

In both orchestrators, if AI section generation fails, the operation is recorded as a failure — it does **not** fall back to dumping the raw transcript into one section:

- Legacy: `ContentGenerationService.GenerateContentAsync` — on `!sectionResult.Success`, `sectionsGenerated` stays 0 and the error is appended to `errors`; final `success = errors.Count == 0 && (sectionsGenerated > 0 || questionsGenerated > 0)` ([ContentGenerationService.cs:191-212](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentGenerationService.cs#L191-L212), [:317](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentGenerationService.cs#L317)). A failed call produces **zero** sections, not one raw-transcript section, and the talk reverts to Draft with an error.
- New wizard: `ContentCreationParseJobForTalk.ExecuteAsync` — on `!result.Success`, the talk status is simply reverted to Draft and the method returns; no sections are written ([ContentCreationParseJobForTalk.cs:78-86](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentCreationParseJobForTalk.cs#L78-L86)).

So the "1 section of raw near-verbatim transcript" the user is seeing is **not** an AI-failure fallback — it is a successful AI call, with output the AI was explicitly instructed to produce.

### A3. Root cause: `PreserveSourceWording` default + prompt semantics

The new wizard's Step-1 form defaults `preserveSourceWording: true` ([InputConfigStep.tsx:175](../web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx#L175)), and this same default of `true` is also the backend's tenant-level default at the DB/EF-config layer:

```
builder.Property(s => s.DefaultPreserveSourceWording)
    .IsRequired()
    .HasDefaultValue(true);
```
[ToolboxTalkSettingsConfiguration.cs:119-121](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ToolboxTalkSettingsConfiguration.cs#L119-L121)

At talk creation the value is resolved as: explicit request value → tenant default → hard system default of `true` — all three defaults land on `true` unless someone actively unchecks the toggle:
```csharp
PreserveSourceWording = request.PreserveSourceWording ?? tenantSettings?.DefaultPreserveSourceWording ?? true,
```
[InitialiseToolboxTalkCommandHandler.cs:117-120](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandHandler.cs#L117-L120)

When `preserveSourceWording = true`, `SectionGenerationPrompts.BuildSectionPrompt` returns an entirely different prompt from the normal "rewrite" one:

```
Your job is to identify natural section breaks — NOT to rewrite, summarize, or rephrase.
...
- For each section, copy the source text VERBATIM into "content" — do not rephrase, condense, or expand
- Preserve the customer's wording, punctuation, line breaks, and emphasis exactly as written
...
- Identify between {minimumSections} and a reasonable number of sections based on the source structure
```
[SectionGenerationPrompts.cs:31-51](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Prompts/SectionGenerationPrompts.cs#L31-L51)

This is the mechanism that produces "filler words, 'proces- process' stutter, unedited" content: the AI is told, in plain English, not to clean any of that up. Combined with `minimumSections = 2` (a floor, not a target) and a continuous spoken transcript that has no natural verbal section breaks, the AI settling on a single section is an unsurprising outcome under this prompt — it is explicitly told to derive section count "based on the source structure," and a rambling, unstructured talk-track has none.

By contrast, the legacy path's prompt (used whenever `preserveSourceWording` isn't threaded through at all, i.e. always for that path) is the "rewrite" branch: "create clear, concise sections that summarize the key points... 4-5 lines... Use clear, simple language," with a floor of 7 ([SectionGenerationPrompts.cs:79-90](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Prompts/SectionGenerationPrompts.cs#L79-L90)). This matches the July "3 well-structured, rewritten sections" behaviour (floor was a target, not strictly hit, but rewriting was in effect).

**Conclusion: Problem 1 is explained entirely by which wizard/prompt-mode processed the video, not by any AI provider outage or fallback.** A talk that went through the new wizard with `PreserveSourceWording` at its default of `true` will reliably produce this symptom for any video with a rambling, non-headed verbal delivery, independent of Claude/Gemini health.

### A4. Timestamp-stripping fix is not implicated

Commit `77f9a3a` (2026-08-25, HEAD) added:
- Prompt instructions in both branches to exclude `[mm:ss]` markers ([SectionGenerationPrompts.cs:50-51](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Prompts/SectionGenerationPrompts.cs#L50-L51), [:88-89](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Prompts/SectionGenerationPrompts.cs#L88-L89))
- A clean-transcript getter (`ITranscriptService.GetCleanFullText`) feeding the prompt instead of the raw timestamped SRT text
- A post-generation sanitizer (`TranscriptMarkerSanitizer.StripTimestampMarkers`) applied to AI output before persisting, in **both** services ([AiSectionGenerationService.cs:220-229](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiSectionGenerationService.cs#L220-L229), [ContentParserService.cs:234-235](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L234-L235))

None of this touches `minimumSections`, `PreserveSourceWording`, or the section-count/splitting logic. It changes *what characters* appear in the content, not *how many sections* are produced or *whether* content is rewritten. The hypothesis in the brief — "did stripping timestamps remove structure the AI used to split sections" — does not hold: the AI was never instructed to use `[m:ss]` markers as section-boundary signals in either prompt branch, before or after this commit; section boundaries are determined by topic/structure instructions, which this commit did not touch. **This commit is unrelated to Problem 1.**

---

## B. Video not displaying ("Failed to load video")

### B1. `VideoUrl` is the single field every rendering surface reads

Every query DTO maps `VideoUrl = talk.VideoUrl` directly, with no fallback to `SourceFileUrl`:
- [GetToolboxTalkByIdQueryHandler.cs:62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Queries/GetToolboxTalkById/GetToolboxTalkByIdQueryHandler.cs#L62)
- [GetMyToolboxTalkByIdQueryHandler.cs:125](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Queries/GetMyToolboxTalkById/GetMyToolboxTalkByIdQueryHandler.cs#L125)
- [GetToolboxTalkPreviewQueryHandler.cs:123](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Queries/GetToolboxTalkPreview/GetToolboxTalkPreviewQueryHandler.cs#L123)
- `HasVideo = !string.IsNullOrEmpty(t.VideoUrl)` in list/dashboard queries ([GetToolboxTalksQueryHandler.cs:74](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Queries/GetToolboxTalks/GetToolboxTalksQueryHandler.cs#L74))

Every frontend surface that renders `<VideoPlayer>` gates on `talk.videoUrl`:
- Admin preview modal: `{talk.videoUrl && <VideoPlayer videoUrl={talk.videoUrl} ... />}` — [PreviewModal.tsx:149-158](../web/src/features/toolbox-talks/components/PreviewModal.tsx#L149-L158)
- Employee-facing viewer: `{currentStep === 'video' && talk.videoUrl && <VideoPlayer videoUrl={talk.videoUrl} ... />}` — [TalkViewer.tsx:591-601](../web/src/features/toolbox-talks/components/TalkViewer.tsx#L591-L601)
- Legacy edit form and detail panels also key off `talk.videoUrl` exclusively ([ToolboxTalkForm.tsx:105,155,1052-1055](../web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L105), [SettingsEditPanel.tsx:162](../web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L162))

`VideoPlayer`'s HTML5 `<video>` element fires `onError={() => setError('Failed to load video')}` when its `src` fails ([VideoPlayer.tsx:502-509](../web/src/features/toolbox-talks/components/VideoPlayer.tsx#L502-L509)) — this is the exact string in the user's report.

### B2. Legacy wizard explicitly syncs the uploaded file URL into `VideoUrl`; the new wizard never does

**Legacy** (`ContentCreationSessionService`, session-based draft talk creation) copies the uploaded file URL into `VideoUrl` at three separate materialisation points:
```csharp
draftTalk.VideoUrl = session.SourceFileUrl;              // ContentCreationSessionService.cs:626
draftTalk.VideoUrl = session.InputMode == InputMode.Video ? session.SourceFileUrl : null;  // :1438
videoTalk.VideoUrl = session.SourceFileUrl;               // :1878
```

**New wizard** — the uploaded video file is stored via a separate presigned-upload flow (`POST /toolbox-talks/learning-wizard/upload-source-url` → `talk.SourceFileUrl`, [ToolboxTalksController.cs:397-418](../src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L397-L418)) and `talk.VideoUrl` is populated **only** from the wizard's explicit "Video URL" text field:
```csharp
VideoUrl = request.VideoUrl,   // InitialiseToolboxTalkCommandHandler.cs:101
```
On the frontend, `videoUrl` is only sent when the user typed a URL, never when they uploaded a file:
```ts
videoUrl: values.inputMode === 'Video' ? values.videoUrl : undefined,   // InputConfigStep.tsx:376
```
(`values.videoUrl` is a separate form field from `sourceFileUrl`/`sourceFileName`, which is what gets set by the file-picker handlers — [InputConfigStep.tsx:288-337](../web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx#L288-L337).)

The code is fully aware `SourceFileUrl` is where the real video lives for an upload — every downstream job that actually needs to *use* the video computes this locally:
```csharp
var videoUrl = talk.SourceFileType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true
    ? talk.SourceFileUrl
    : talk.VideoUrl;
```
This exact pattern appears in three places, and in **none of them** is the result written back to `talk.VideoUrl`:
- `ParseToolboxTalkContentCommandHandler.HandleVideoAsync` [ParseToolboxTalkContentCommandHandler.cs:174-190](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ParseToolboxTalkContent/ParseToolboxTalkContentCommandHandler.cs#L174-L190) — the Step-2 trigger, used only to decide whether to enqueue transcription
- `VideoTranscriptionJobForTalk.ExecuteAsync` [VideoTranscriptionJobForTalk.cs:73-90](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/VideoTranscriptionJobForTalk.cs#L73-L90) — used only to call ElevenLabs and to start subtitle processing

A full sweep of every write to `.VideoUrl =` in the ToolboxTalks module ([grep results](#appendix)) confirms there is no code path anywhere in the new-wizard pipeline (`InitialiseToolboxTalkCommandHandler`, `ParseToolboxTalkContentCommandHandler`, `UpdateToolboxTalkSettingsCommandHandler`, `VideoTranscriptionJobForTalk`, `ContentCreationParseJobForTalk`) that ever assigns `talk.VideoUrl = talk.SourceFileUrl` for a file upload. Only `UpdateToolboxTalkCommandHandler.cs:120` (`toolboxTalk.VideoUrl = request.VideoUrl`) writes the field post-creation, and that's the legacy-wizard's own edit-form save path, driven by its own `videoUrl` form field, not by `sourceFileUrl`.

**Conclusion: for a talk created via the new wizard by uploading a video file (as opposed to pasting a URL), `talk.VideoUrl` stays `null` for the life of the talk.** Transcription and subtitle generation still work (they read `SourceFileUrl` directly), so the talk otherwise looks fully processed — sections parse, subtitles generate — but no UI surface will ever render the video, because they all key exclusively off the `VideoUrl` column, which was never populated.

### B3. Does this produce "no video" or literally "Failed to load video"?

The gates above (`talk.videoUrl &&`) mean a `null` `VideoUrl` should suppress the `<VideoPlayer>` entirely rather than render a broken `<video>` element — i.e. the more common visible symptom would be "no video section shown at all," not an explicit error banner. Two ways the reported "Failed to load video" text specifically could still occur, not fully distinguishable from static code alone:
- The talk was created via the **"Video URL" text-entry** path (not a file upload) with a URL that is valid-looking but not directly playable/embeddable (e.g. blocked by CORS — `VideoPlayer.tsx:512` sets `crossOrigin="anonymous"` on the `<video>` tag, which will hard-fail the load if the R2 bucket's CORS policy doesn't allow the app origin — or a URL type the player's `embedUrl`/`videoSource` logic doesn't handle as expected).
- A UI surface not covered by this recon's search (there could be a third `VideoPlayer`-like consumer, or the QR/location training player at [app/qr/[codeToken]/page.tsx:88](../web/src/app/qr/[codeToken]/page.tsx#L88), which was not audited here) passes a non-null but broken URL.

Either way, the confirmed, code-level defect (§B2 — `VideoUrl` never populated from `SourceFileUrl` for uploads) is real and reproducible by inspection, and is sufficient on its own to explain "the video doesn't display," whether the user-visible manifestation is a blank video area or the explicit error overlay. **§D gives the query to confirm which of these it is for the actual failing talk.**

### B4. Not related to R2/SrtStorage config

The `SrtStorage` config block (CLAUDE.md "Cloudflare R2 Storage") governs subtitle file storage; video files use the same R2 bucket via `IR2StorageService.GenerateUploadUrlAsync`/`GetPublicUrl` (used by both the presigned-upload endpoint and, presumably, the legacy wizard's upload path — not audited line-by-line here, out of scope since the defect is a DB-field-population gap, not a storage-layer or credentials problem). There is no evidence of an R2 config or credentials regression — the video **is** being uploaded and stored successfully (transcription, which reads `SourceFileUrl` directly, works); it's a metadata-wiring gap on the talk row, not a storage outage.

---

## C. Are Problems 1 and 2 connected?

Yes, at the level of "both are pre-existing defects in the same newer pipeline that this talk happened to go through," but they are **independent defects**, not cause-and-effect of each other:
- Problem 1 is a prompt-mode/default-configuration issue in `ContentParserService` / `SectionGenerationPrompts`.
- Problem 2 is a missing field-sync in `InitialiseToolboxTalkCommandHandler` / `ParseToolboxTalkContentCommandHandler`.

Fixing one would not fix the other. Both only manifest for talks processed by the new wizard (`learning-wizard/**`, discriminated by `LastEditedStep != null` per Note 29); neither manifests on the legacy wizard.

---

## D. Timeline and what to verify at runtime

| Date | Event | Relevant? |
|---|---|---|
| 2026-06-10 | `3c62593 feat(phase-5): rebuild Step 1 (Input & Config) — wizard 5.3a` — new wizard Step 1 scaffolded, `preserveSourceWording: true` default introduced | **Yes** — origin of Problem 1's default |
| 2026-06-11 | `900ad09 feat(phase-5): rebuild Step 2 (Parse) — wizard 5.3b` — `ParseToolboxTalkContentCommandHandler` / talk-based parse jobs introduced | **Yes** — origin of Problem 2's gap |
| 2026-07-24 | `9ba0355`/`1e9f927`/`79cfc24` — new-wizard tenant-level defaults wired up (settings UI, `DefaultPreserveSourceWording` etc.) | Confirms the defaults, doesn't change them |
| — | (unable to date) `bce8e52 feat(wizard): cutover toggle infrastructure (§5.27)` — `UseNewWizard` toggle added, default `"false"` ([TenantSettingsService.cs:56](../src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingsService.cs#L56)) | **Key unknown** — see below |
| 2026-08-25 | `77f9a3a` timestamp-stripping fix | Confirmed unrelated (§A4) |
| "recent" | Gemini model retirements / `gemini-3.6-flash` migration | Confirmed unrelated (§A1) — Gemini isn't in this path at all |

**The one fact this recon cannot determine from static code, and that would confirm the whole causal chain, is whether the specific failing talk was actually created via the new wizard.** Recommended verification (read-only SQL against the tenant's data):

```sql
SELECT "Id", "Title", "LastEditedStep", "PreserveSourceWording",
       "VideoUrl", "SourceFileUrl", "SourceFileType", "VideoSource",
       "CreatedAt"
FROM "ToolboxTalks"
WHERE "Title" = '<the talk in question>'
ORDER BY "CreatedAt" DESC;
```
- `LastEditedStep IS NOT NULL` → created via the new wizard (per Note 29's discriminator). `LastEditedStep IS NULL` would mean this recon's hypothesis is wrong and the cause lies elsewhere (worth re-opening the Gemini/timestamp angle in that case).
- `VideoUrl IS NULL AND SourceFileUrl IS NOT NULL` → confirms §B2 directly for this row.
- `PreserveSourceWording = true` → confirms §A3 directly for this row.

Also worth checking, to establish blast radius:
```sql
SELECT "TenantId", "Value" FROM "TenantSettings" WHERE "Key" = 'UseNewWizard';
```
and whether/when it was flipped for the affected tenant(s) (no audit history for `TenantSettings` changes was located in this recon — `SystemAuditLog` per Note 3 does not appear to cover this specific settings table based on the wiring list in that note).

---

## E. Severity / blast radius

- **Scope**: every video-based learning created via the new wizard (`learning-wizard/**`, i.e. `/admin/toolbox-talks/learnings/**`) by **uploading a video file** is affected by Problem 2 (no video display) with certainty, per static analysis. Every video/PDF/DOCX/text-based learning created via the new wizard with `PreserveSourceWording` left at its default (`true`, the default at three layers — UI, tenant setting, and system fallback) is affected by Problem 1 (verbatim/collapsed sections) with certainty.
- **Not affected**: any talk created via the legacy wizard (`create-wizard/**`, `LastEditedStep IS NULL`) — that path has neither defect. Since `UseNewWizard` defaults to `"false"` tenant-wide ([TenantSettingsService.cs:56](../src/Core/QuantumBuild.Core.Application/Features/TenantSettings/TenantSettingsService.cs#L56)), most tenants should still be defaulting to the legacy wizard unless a tenant admin explicitly flipped the toggle (Note 29) — which is exactly why this may be surfacing now for one tenant/user and not been noticed broadly: it requires either that flip, or the one-shot `?wizard=new` URL parameter, or direct navigation to a `/learnings/**` URL.
- **Client-facing risk**: for any tenant that *has* switched to the new wizard (or where a user stumbled into it), **every** subsequent video-based learning is silently degraded in two ways simultaneously — unreviewable/unedited transcript dumped as "content," and no visible video for employees to actually watch. Because content still reaches `ReadyForReview`/`Draft` status successfully (no errors are surfaced — both defects are silent, not failures), this could pass unnoticed through a review process that only checks "did generation succeed," not "does the output look like July's."
- **Recommended immediate check**: confirm via §D's queries whether `UseNewWizard` is `true` for the reporting tenant, and whether other recently-created talks for that tenant (or others with the toggle on) show the same `LastEditedStep`/`VideoUrl`/`PreserveSourceWording` pattern — this determines whether it's one incident or systemic across all new-wizard adopters.

---

## Appendix — full sweep of `.VideoUrl =` writes in ToolboxTalks module

```
src/Modules/ToolboxTalks/.../ContentCreationSessionService.cs:626   draftTalk.VideoUrl = session.SourceFileUrl;
src/Modules/ToolboxTalks/.../ContentCreationSessionService.cs:1438  draftTalk.VideoUrl = session.InputMode == InputMode.Video ? session.SourceFileUrl : null;
src/Modules/ToolboxTalks/.../ContentCreationSessionService.cs:1878  videoTalk.VideoUrl = session.SourceFileUrl;
src/Modules/ToolboxTalks/.../ContentDeduplicationService.cs:460     target.VideoUrl = source.VideoUrl;   (content-reuse/dedup, copies between existing talks — not creation)
src/Modules/ToolboxTalks/.../UpdateToolboxTalkCommandHandler.cs:120 toolboxTalk.VideoUrl = request.VideoUrl;   (legacy edit-form save)
```
No write exists in `InitialiseToolboxTalkCommandHandler`, `ParseToolboxTalkContentCommandHandler`, `VideoTranscriptionJobForTalk`, `ContentCreationParseJobForTalk`, or `UpdateToolboxTalkSettingsCommandHandler` (the new-wizard's own settings-step save handler) that would populate `VideoUrl` from `SourceFileUrl`.
