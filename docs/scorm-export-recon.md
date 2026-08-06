# SCORM 1.2 Export — Feasibility & Architecture Recon

Status: read-only recon, no code changed. Scope: standalone Toolbox Talks only (not courses), SCORM 1.2 only, one package per language.

---

## Executive summary

- **No viable .NET/C# library exists for *generating* SCORM 1.2 packages.** Every NuGet/GitHub candidate found is a client for Rustici's *hosted* services (SCORM Cloud, Hosted SCORM Engine) — i.e. tools for *playing/dispatching* SCORM content, not producing it. **Recommendation: hand-roll the manifest + zip.** The SCORM 1.2 manifest schema is small, especially for a single-SCO-per-talk model, which matches how ToolboxTalks content is already structured (one talk = one piece of content).
- **The browser-side JS API bridge is worth evaluating for reuse rather than writing from scratch.** `scorm-again` (npm, actively maintained, MIT-ish OSS) handles `window.API` discovery and the cross-browser/`window.parent`-chain quirks that are easy to get subtly wrong. Recommend a short spike before committing either way.
- **Three of the six content elements (sections, AI slideshow, PDF slides) are non-blockers** — they're already static HTML/images with no backend dependency. **Video is a non-blocker for the tracking algorithm** but raises a real packaging-size decision (embed vs. URL reference). **Quiz grading is a genuine blocker as currently designed** — correct answers are deliberately withheld from the client until server-side grading completes, which conflicts with a truly standalone SCORM package. This has to be a deliberate design decision, not an afterthought.
- **No ZIP-building infrastructure exists anywhere in the codebase today.** This is net-new work, though it follows established patterns (QuestPDF generate-then-R2-upload, certificate proxy-download) and needs no new NuGet dependency (`System.IO.Compression` is stock BCL).
- **"Passes SCORM Cloud validation" is necessary but not sufficient.** Real-world LMS compatibility (Moodle, SuccessFactors, Cornerstone) has vendor-specific quirks around zip structure, encoding, and JS API discovery that spec conformance alone won't catch. Budget an actual compatibility test pass, not just automated validation.
- **Recommended for first version: video by URL reference, not embedded.** Keeps packages small and avoids per-LMS upload-size ceilings; the tradeoff (host LMS network must reach the public R2 URL) is real but manageable, and R2 URLs are already unauthenticated/public.

---

## Part 1 — .NET ecosystem investigation

### SCORM 1.2 packaging libraries for .NET/C#

No actively-maintained NuGet/C# library exists for *generating* SCORM 1.2 packages. NuGet.org search ("scorm") returns 6 hits, all of them clients or wrappers for Rustici's hosted services:

| Package | Downloads | Last updated | What it actually is |
|---|---|---|---|
| `Com.RusticiSoftware.Cloud.V2` | 269,714 | May 2026 | Swagger-generated C# client for the **SCORM Cloud** API v2 — talks to Rustici's hosted service, doesn't build packages locally |
| `TinCan` | 551,112 | Dec 2018 | Tin Can/xAPI spec library — different spec, not SCORM 1.2 |
| `RusticiSoftware.HostedEngine.Client` | 163,181 | Apr 2019 | Client for the older Hosted SCORM Engine web service — again a client to a hosted player |
| `ScormHelper` | 7,056 | Nov 2016 | Wrapper around the SCORM Cloud client above |
| `ScormExtensions` | 4,392 | Dec 2016 | Async extension methods for the same SCORM Cloud client |
| `PageTiger.TinCan` | 142 | Feb 2026 | Tin Can/xAPI, not SCORM |

GitHub topic search (`github.com/topics/scorm`, C# filter) surfaced only a demo LMS (consuming side, not a packaging tool) and one effectively-abandoned toolkit (1 star, ~4 commits, last touched 2022) too thin to be a real candidate.

**Untangling the well-known commercial names**, since they're easy to conflate:
- **Rustici Engine** — a self-hosted/managed *player/import* component embedded in your own LMS to play/track SCORM, xAPI, cmi5, LTI, AICC. Not something you'd call from a build pipeline to *produce* a zip.
- **Rustici Generator** — an unrelated product: an API-driven content/metadata *parser* (extracts text/metadata from files), not a SCORM packager.
- **SCORM Cloud** — Rustici's hosted SaaS for testing, playing, and dispatching SCORM content. Also a consumer, not a producer, of packages.

### Community consensus: hand-rolled manifest + JS bridge

No single source explicitly declares "hand-rolling is the standard" — this is an inference from a consistent pattern of evidence, not a directly-quoted claim:

- **pipwerks/SCORM-Manifests** — literally boilerplate XML templates meant to be copied and hand-edited, not a code generator. Its existence and modest persistent popularity signals that copy-and-edit is the normal workflow for the manifest side.
- **pipwerks SCORM API Wrapper** (JS/AS2/AS3, also on npm) — a small abstraction over the raw SCORM JS API, created 2008, still forked/referenced today. This is the "small JS bridge script" pattern, old and battle-tested enough to represent genuine de facto practice.
- **jcputney/scorm-again** — a *modern*, actively maintained (commits within the last month at research time, v3.0.5 on npm, 321 GitHub stars) JS runtime supporting SCORM 1.2 and 2004. This is the closest thing to a serious library in the whole space — but it's player/runtime-side (the API bridge), not a manifest/zip packaging tool.
- Multiple vendor knowledge-base articles (ISpring, Commlab India, LearnUpon, TalentLMS) walk through manually editing `imsmanifest.xml` fields as the standard onboarding task — none point to a packaging library.

**Net read:** for manifest + zip, hand-rolling is genuinely what the ecosystem does — there's little to generate (a handful of XML nodes + a flat zip). For the browser-side API bridge, `scorm-again` is a credible, actively-maintained option worth a reuse spike rather than reinventing.

### Licensing

- `scorm-again` — OSS, npm-published, no licensing blocker identified.
- Rustici's NuGet clients (`Com.RusticiSoftware.Cloud.V2`, etc.) are free to use but only useful if we adopt SCORM Cloud as a hosted service (commercial SaaS, not a packaging tool) — see Part 8.
- No commercial packaging library was found to have licensing implications, because none exists to license.

### Recommended approach: hand-roll manifest/zip; spike `scorm-again` for the JS bridge

- **Manifest + packaging: build ourselves.** `System.IO.Compression.ZipArchive` (stock BCL, no new NuGet dependency) + a small `imsmanifest.xml` template, populated per-talk. This is a modest, well-understood build consistent with community practice, and keeps us in control of the exact bytes going into the zip — important given the zip-structure gotchas in Part 8 (root-level manifest, lowercase filename, encoding).
- **JS API bridge: spike `scorm-again` before committing either way.** Writing `window.API` discovery and LMS communication from scratch risks re-discovering exactly the cross-browser/nesting quirks this library has already fought through. A short spike (does it fit our Next.js-adjacent static export model cleanly?) should decide this, not a default-to-custom assumption.
- **Rustici NuGet clients: not needed for packaging.** Only relevant if SCORM Cloud is adopted as a hosted validation/dispatch service (Part 8), which is a separate, optional decision.

---

## Part 2 — Current content model to SCORM mapping

### How a toolbox talk currently renders

**Employee-facing chain:** `web/src/app/(authenticated)/toolbox-talks/[id]/page.tsx` → `TalkViewer.tsx` (a client-side step machine: `video → sections → quiz → signature → complete`, driven by React state and TanStack Query) → per-step components.

| Element | Rendering mechanism | Backend dependency |
|---|---|---|
| **Section content** | `SectionContent.tsx` — `dangerouslySetInnerHTML={{ __html: section.content }}` wrapped in Tailwind `prose` classes. No custom parsing, no MDX, no embedded React widgets inside the HTML itself. | None for content itself; interactivity (5s read timer, acknowledge gate, sequential nav) lives in the **React shell around** the content, not inside it. |
| **AI HTML slideshow** | `ToolboxTalk.SlideshowHtml` (+ `ToolboxTalkSlideshowTranslation.TranslatedHtml`), a single self-contained HTML string rendered via `&lt;iframe srcDoc={html}&gt;` in `HtmlSlideshow.tsx`. Generation prompts use emoji glyphs for icons — no external `&lt;img&gt;`, no CDN `&lt;script src&gt;`/`&lt;link&gt;`/Google Fonts references found. | None — genuinely self-contained static HTML/CSS/JS already. Navigation is `postMessage` between parent and iframe. |
| **PDF-derived slides** | `SlideDto { Id, PageNumber, ImageUrl, Text }` — `ImageUrl` is an absolute public R2 URL. Rendered as plain `&lt;img&gt;` tags with CSS animation in `Slideshow.tsx`. | None per-slide; only the initial fetch of the slide list is dynamic. |
| **Video** | `VideoSource` enum (`None/YouTube/GoogleDrive/Vimeo/DirectUrl`). Embedded sources (YouTube/Vimeo/GDrive) render a plain `&lt;iframe&gt;`; `DirectUrl` renders a plain `&lt;video&gt;` tag — no wrapper library (no react-player/video.js). Uploaded video URLs are direct, unsigned, public R2 URLs. | Watch-progress persistence only (see Part 5); playback itself is dependency-free. |
| **Quiz** | `QuizSection.tsx` renders questions from a DTO that **excludes correct answers**; submits raw answers to `POST /quiz/submit` for server-side grading. | **Hard dependency** — grading logic and correct-answer data live only server-side, by design (anti-cheat). See below. |

### What needs to happen for standalone operation

| Element | Static rendering? | Asset packaging | Backend dependency to shim |
|---|---|---|---|
| Sections | Already static HTML | None needed — HTML is self-contained, no relative image paths found (all uploads use absolute R2 URLs; no image-insertion UI feeds section content) | Just the surrounding shell logic (timer, ack, nav) — trivial to reimplement in vanilla JS since it's already React-shell-only in the current app, not baked into the content |
| AI slideshow | Already static HTML/CSS/JS | None — no external asset refs | None |
| PDF slides | Static PNGs at absolute public R2 URLs | Either link directly to R2 (simplest) or download+embed images in the zip if true offline operation is wanted | None |
| Video | Static `&lt;video&gt;`/`&lt;iframe&gt;` element | Decision point — see Part 5 | Progress persistence needs to redirect from `POST /video-progress` to SCORM `LMSSetValue` calls — a clean swap point, not a redesign |
| Quiz | **Not currently standalone-capable** | N/A | **Grading logic + correct answers must be exposed client-side**, which is a deliberate reversal of the current server-authoritative anti-cheat design — see below |

### Rendering constraints specific to React → static translation

- Section content requires **no translation work** — it was never a React-component tree to begin with, just an HTML string rendered via `dangerouslySetInnerHTML`. The React "shell" behavior around it (read timer, acknowledgment gating, next/prev navigation) is simple state machine logic, portable to vanilla JS without meaningfully changing behavior.
- The AI HTML slideshow is the most SCORM-friendly asset in the entire app — it's *already* a fully self-contained static HTML/CSS/JS blob with no auth and no external dependency. It could be dropped into a SCORM package close to as-is.
- Video playback needs no React runtime — it's a plain `&lt;video&gt;`/`&lt;iframe&gt;` today, so the visual/playback layer ports trivially. What doesn't port trivially is progress persistence (see Part 5) and, for embedded sources (YouTube/Vimeo/GDrive), the fact that watch-tracking is already fake (a timer-based approximation, not real playback position) — so SCORM export gains nothing extra to solve there, but also inherits that weakness.

---

## Part 3 — SCORM API bridge design

### What our content needs to call

Standard SCORM 1.2 API surface, all via the LMS-injected `window.API` object:

- `LMSInitialize("")` — call once on package load, before any other API call.
- `LMSSetValue("cmi.core.lesson_status", ...)` — one of `passed | failed | completed | incomplete | browsed | not attempted`.
- `LMSSetValue("cmi.core.score.raw", ...)` — 0–100 numeric score.
- `LMSSetValue("cmi.core.session_time", ...)` — SCORM 1.2 time format (`HHHH:MM:SS.SS`).
- `LMSSetValue("cmi.core.lesson_location", ...)` — bookmark/resume location (e.g. current section index), for resume-on-reopen.
- `LMSCommit("")` — persist pending `SetValue` calls to the LMS; call after meaningful state changes, not just at the very end.
- `LMSFinish("")` — call once on package unload/exit.
- `LMSGetLastError()` / `LMSGetErrorString(errorCode)` — for surfacing API failures during development/debugging.

### Bridge design sketch

```
scorm-bridge.js
├── findAPI(win)          // walk win.parent chain (and win.opener) up to a depth limit,
│                          // looking for an object exposing LMSInitialize
├── init()                 // locate API, call LMSInitialize(""), read cmi.core.lesson_location
│                          // to resume at the right section; no-op gracefully if API not found
├── setLocation(sectionIdx)// LMSSetValue("cmi.core.lesson_location", ...) + LMSCommit on section change
├── reportScore(raw, passed)
│                          // LMSSetValue score.raw + lesson_status ("passed"/"failed"), LMSCommit
├── reportTime()           // accumulate session time client-side, LMSSetValue on finish
└── finish()               // LMSCommit(""), LMSFinish(""); called on page unload (beforeunload)
```

**"API not found" handling (dev-testing without a host LMS):** `findAPI` returns `null` after exhausting the search depth; every subsequent bridge call becomes a no-op that logs to console instead of throwing. This lets the exported package be opened directly in a browser for QA without a host LMS present, and matches the pattern both `pipwerks` and `scorm-again` already implement.

**Recommendation:** spike `scorm-again` against this exact call list before hand-writing it — if it fits cleanly, adopt it and write only the thin content-specific glue (section progress tracking, quiz score calculation) on top; if it doesn't fit our export shape, fall back to a hand-rolled ~150-line bridge modeled on `pipwerks`.

---

## Part 4 — Multi-language decision

### How the app knows which languages a talk has

- Entity: `ToolboxTalkTranslation` — one row per `(ToolboxTalkId, LanguageCode)`, holding `TranslatedTitle`, `TranslatedDescription`, `TranslatedSections` (JSON), `TranslatedQuestions` (JSON), plus review/revalidation metadata.
- **Existing admin endpoint:** `GET /api/toolbox-talks/{id}/translations` returns every existing `ToolboxTalkTranslation` row with `LanguageCode`, `Language`, `TranslatedTitle`, `TranslatedAt`, `TranslationProvider` — this is exactly the list an export UI's language multi-select needs.
- **No employee-facing "list languages" endpoint exists** — the employee experience hardcodes to `Employee.PreferredLanguage` (with fallback), inlining a single translation into the response rather than exposing a picker. Video subtitles and slideshow/slides do accept an explicit `?lang=` query param, but full-page section/quiz translation is not user-selectable at runtime today.

**Implication:** the export flow should use the **admin** translations endpoint to enumerate available languages, then pull `TranslatedSections`/`TranslatedQuestions` JSON directly — not route through the employee-scoped query, which is hardcoded to a specific employee's preference and would require faking an employee context.

### Per-language iteration vs. shared cost

Per-language export is genuinely just N iterations of the single-language flow with no meaningful shared cost worth bundling for a first version:

- Each language's content (`TranslatedSections`, `TranslatedQuestions`) is already fully materialized as JSON on the `ToolboxTalkTranslation` row — no additional translation work happens at export time.
- The only shared step across languages is fetching the talk's non-text assets once (video URL, slide images) — trivial to cache in-memory for the duration of a multi-language export request, not worth a different architecture.
- Bundling all languages into a single package would require SCORM content to switch language at runtime (a language picker inside the SCORM content itself) — this is more complex than N packages, not less, and doesn't match how the source content is modeled (one export = one flattened, static language). **Per-language packages is the right call for v1, not just the simpler one.**

### Export UI language selection

Confirmed feasible: the admin translations list already has everything (`LanguageCode`, `Language` display name) needed to drive a multi-select checkbox list in the export UI — no new backend query required beyond what exists, just a new controller action that accepts the selected language codes and loops the single-language export per selection.

---

## Part 5 — Video handling

### Current storage and delivery

- **Storage:** Cloudflare R2, bucket `rascor-videos` (via `SubtitleProcessingSettings`) or `rascor-media` (via `R2StorageSettings` — two separate settings classes exist for historical reasons, both documented in CLAUDE.md).
- **Delivery:** uploaded videos get a **direct, unsigned, public R2 URL** (`talk.VideoUrl = result.PublicUrl`), served via a plain `&lt;video&gt;` tag for `DirectUrl` sources, or an `&lt;iframe&gt;` embed for YouTube/Vimeo/Google Drive sources.
- **File size limit:** 500MB hard cap on video uploads (`MaxVideoSizeBytes`, enforced consistently in `R2StorageSettings`, `ToolboxTalkFilesController` via `[RequestSizeLimit]`/`[RequestFormLimits]`, and the content-creation wizard upload path). Allowed MIME types: `video/mp4`, `video/webm`, `video/quicktime`. **No documented typical size** — 500MB is a ceiling, not a target; there's a backlog item for cost analysis by video size that hasn't produced concrete numbers yet.

### Two options and tradeoffs

**Embed video in the SCORM zip:**
- Pro: package works fully offline, no host-LMS network dependency.
- Con: with a 500MB per-video ceiling, packages could be enormous. Most LMS SCORM importers enforce their own upload-size limits, frequently far below 500MB — a large embedded-video package risks failing import on the very LMSes we're trying to support, independent of anything we do right.
- Con: multiplies storage/transfer cost — every language's exported package would re-embed the same (or a re-encoded per-language-subtitle) video file.

**Reference video by URL (point `&lt;video src&gt;` at the existing public R2 URL):**
- Pro: packages stay small regardless of video length — this is the dominant practical advantage.
- Pro: R2 URLs are already public/unauthenticated, so no new auth-bridging problem is introduced.
- Con: requires the host LMS's network to reach the R2 URL. Some corporate networks block external CDN domains by default.
- Con: package is no longer *fully* standalone in the offline sense — there's a residual external dependency, just not an authenticated one.

**Hybrid (let admin choose per export):** technically straightforward given the URL-reference path already works and embedding is "just" adding a zip step — but doubles testing surface for a first version, with no clear near-term demand signal for the embedded path specifically.

### Recommendation for first version

**URL reference, not embedded**, matching the lean stated in the request. Rationale:
- Keeps packages small and upload-friendly across LMS import limits we don't control.
- Avoids re-encoding/re-embedding cost per language export.
- The "network must reach the URL" caveat is real but manageable — R2 URLs are already public and unauthenticated, so worst case is a customer IT team allowlisting one CDN domain, not a security/auth integration problem.
- Matches the existing architecture — the app already delivers video this way to the browser today; SCORM export doesn't need to reinvent video delivery, just the JS bridge that reports progress.
- Defer embedded-video as a later option once there's an actual customer requirement for fully offline packages (e.g. air-gapped sites) — note this explicitly in the export UI copy so admins understand the tradeoff, per the request's own framing.

---

## Part 6 — Quiz score reporting

### Current quiz submission flow

- Frontend (`QuizSection.tsx`) renders questions from `MyToolboxTalkQuestionDto`, which **deliberately excludes `CorrectAnswer`/`CorrectOptionIndex`** — confirmed via `GetMyToolboxTalkByIdQueryHandler`, with an explicit code comment: `// Build question DTOs (without correct answers for quiz taking)`.
- Raw answers are POSTed to `/api/my/toolbox-talks/{id}/quiz/submit`, graded entirely server-side in `SubmitQuizAnswersCommandHandler`:
  - MultipleChoice: index-based grading, translation-safe (frontend maps shuffled display index back to original index before submitting).
  - TrueFalse/ShortAnswer: case-insensitive string compare.
  - Quiz randomization (`ShuffleQuestions`, `ShuffleOptions`, `UseQuestionPool`) is generated fresh server-side per view.
- Correct answers are only returned to the client **after** grading, for the post-submit review screen — never before.
- Pass threshold is per-talk configuration (`talk.PassingScorePercent`-style setting, consistent with the documented default of 80%).

### For SCORM: reporting to the host LMS

Once a package has a pass/fail and score determined by whatever mechanism is chosen (see below), reporting is straightforward and matches the request's own plan:
- `LMSSetValue("cmi.core.score.raw", passPercentage)`
- `LMSSetValue("cmi.core.lesson_status", passed ? "passed" : "failed")`
- Both calls happen after quiz submission, followed by `LMSCommit` and eventually `LMSFinish` on package exit.

### Can the quiz run standalone (no backend calls for grading)?

**Not with the current design, without a deliberate change.** Correct answers never reach the browser prior to submission — this is a security/anti-cheat decision, not an incidental implementation detail (the DTO comment makes the intent explicit). A SCORM package with no backend has exactly three options, and this recon flags them as a decision point rather than picking one:

1. **Bake correct answers into the exported static JS/JSON.** Works technically, but is a real security regression relative to the current server-authoritative model — anyone can view-source the SCORM package and read the answer key. For workplace-safety compliance quizzes this may be an acceptable tradeoff (the stakes are different from, say, a certification exam), but it should be an explicit, named decision, not something discovered later.
2. **Keep quiz submission as a live backend call from inside the SCORM package.** Directly conflicts with the "no backend, standalone" requirement stated for this project — the package would need network access to our API specifically (not just a public CDN), plus some form of unauthenticated-but-scoped submission endpoint, which is new attack surface.
3. **Trust the SCORM `cmi.interactions` data model / host LMS grading.** SCORM 1.2 supports recording each question's response via `cmi.interactions.n.*` fields, but grading (pass/fail determination) is still something *our* content has to compute — SCORM doesn't grade for us. This doesn't avoid the answer-exposure problem, it just changes where the graded result is reported.

**Bottom line: option 1 is the only one that actually satisfies "standalone, no backend," and it requires explicit product sign-off** that exposing quiz answers client-side inside exported packages is acceptable. This is the single most consequential open decision in the whole recon — it should be resolved before implementation estimates are finalized, not discovered mid-build.

---

## Part 7 — Export UI and backend endpoint

### API endpoint sketch

```
POST /api/toolbox-talks/{id}/scorm-export
Body: { languageCodes: string[], includeVideo: boolean }
Auth: ToolboxTalks.Admin (consistent with other admin-only content operations like translations/generate)
Response: one of —
  - streamed ZIP per language if languageCodes.length == 1 (matches certificate-download proxy pattern: File(bytes, "application/zip", ...))
  - a manifest of per-language download URLs if languageCodes.length > 1 (avoids one giant multi-language response; each language zip generated and either streamed sequentially or uploaded to R2 with a returned URL, following the QuestPDF generate-then-upload-to-R2 pattern already used for certificates/validation reports)
```

No existing controller precedent covers "generate N artifacts from one request" — recommend modeling this on the validation-run pattern (kick off generation, poll/notify, download when ready) if multi-language export proves slow enough to need it; for a single language it can likely be synchronous, matching the certificate-download proxy pattern directly.

### Export UI sketch

- **Location:** talk detail page (`/admin/toolbox-talks/talks/[id]`), as a new action alongside existing admin actions (edit, generate, translations) — not a separate top-level section, since it's a per-talk operation like certificate download.
- **Language multi-select:** populated from `GET /api/toolbox-talks/{id}/translations` (already exists, no new query needed).
- **Video handling option:** a toggle or radio ("Reference video by URL" / "Embed video in package") — default to URL reference per the Part 5 recommendation, with inline copy explaining the host-LMS-network caveat.
- **Progress indicator:** needed if generation is slow (video embedding, multi-language, or zip assembly of larger PDF-slide sets) — reuse the existing SignalR hub pattern (e.g. modeled on `TranslationValidationHub`) if generation runs as a background job, or a simple spinner if it stays synchronous per-language. Recommend starting synchronous for v1 given per-language exports with URL-referenced video should be fast (mostly JSON→static-HTML templating + a thin zip), and only moving to a background-job+SignalR pattern if real-world generation time proves it necessary.

---

## Part 8 — Testing strategy

### SCORM Cloud (industry-standard validator)

- **Free trial confirmed, no time limit stated:** 10 active registrations, 3 active courses, 5GB content cap. Usable for manual pre-release checks; the "3 active courses" ceiling would need active management if wired into a pipeline running many times a day.
- **API exists (V2, JSON REST, OAuth2)** covering Course Service (import/preview/configure/info) and Dispatch Service, but **a dedicated, lightweight "just validate this zip, give me pass/fail + errors" endpoint could not be confirmed from public docs** — the fetched API overview and getting-started pages describe import/registration flows, not a standalone validation-only call, and don't state whether import is synchronous or async.
- **Recommendation: treat "SCORM Cloud in CI" as an open spike, not a settled architecture decision.** Before committing to it as a pipeline dependency, do a short hands-on test with a live trial account — upload a package via the API, inspect the actual response shape, confirm sync/async behavior. Don't budget automated CI validation as a known-quantity integration yet.
- There is a documented `testRegistrationPostUrl`-style method for webhook/postback testing, which is CI-adjacent but solves a different problem (webhook delivery, not package validation).

### Real-world LMS testing

**Recognized compatibility gotchas to test against explicitly** (not just spec conformance):
- Zip structure: `imsmanifest.xml` must sit at the zip root — nesting one folder down is reportedly the single most common packaging error across LMS support docs (Articulate, LearnUpon, TalentLMS, Moodle forums, and a Drupal SCORM-module issue tracker all describe this same failure independently). Compress the *contents* of the course folder, not the folder itself.
- Manifest filename must be lowercase; avoid special characters in file/folder names.
- Character encoding is inconsistent across platforms — directly relevant given QuantumBuild's multi-language export use case; treat each target LMS as needing its own encoding smoke test rather than assuming one fix (e.g. UTF-8 everywhere) covers all vendors.
- **Moodle:** SCORM 1.2 completion/scoring reporting reportedly behaves differently from 2004 in some configurations (anecdotal, forum-sourced, version-dependent); has a built-in SCORM debug/trace mode useful for our own testing.
- **SAP SuccessFactors:** externally-hosted SCORM 1.2 content requires a correctly configured cross-domain solution ("Proxlet") or content fails silently; legacy `*.plateau.com` domain sometimes needs to be in browser Trusted Sites; a KeepAlive session ping fires every 5 minutes.
- **Cornerstone OnDemand:** reported "Jump" navigation limitations are a SCORM 2004 sequencing feature and not relevant to a 1.2-only export.

**Recommended target LMSes for validation, in priority order:**
1. **SCORM Cloud** (hosted, free trial) — first-line automated/manual check, catches spec-level issues fast.
2. **Moodle**, self-hosted — free, real-world LMS, good second line. **Caveat:** the previously-easy path (`docker pull bitnami/moodle`) is no longer freely available — Broadcom moved Bitnami's free images behind a paid subscription, with existing tags migrated to a frozen, explicitly-temporary `bitnamilegacy/moodle`. The maintained alternative is `moodlehq/moodle-docker` (Moodle HQ's own docker-compose setup), but it's built for Moodle core developers running Behat tests against a source checkout, not a one-command spin-up — expect roughly an afternoon of setup, not five minutes, if this path is taken.
3. **SAP SuccessFactors / Cornerstone** — only if either is a named customer requirement; otherwise defer, since these typically require commercial trial access and the cross-domain/Proxlet configuration adds real setup overhead.

**On automation:** given the SCORM Cloud validation-endpoint gap above, plan for **manual validation as the baseline for v1**, with automated CI-based validation as a stretch goal contingent on the SCORM Cloud API spike confirming a workable synchronous endpoint exists.

---

## Part 9 — Scope estimate

Rough implementation chunks, ordered roughly by dependency:

| Chunk | Scope | Confidence / notes |
|---|---|---|
| **1. Manifest generation** | `imsmanifest.xml` template + per-talk population (title, resource refs, identifiers, SCO organization) | Low risk — schema is small, well-precedented |
| **2. Static HTML rendering of content** | Template that assembles section HTML (already static) + AI slideshow (already static) + PDF slide images into a single-SCO static HTML/CSS page with sequential navigation reimplementing the current shell logic (read timer, acknowledge gate, next/prev) | Low-medium risk — logic exists today, just needs a vanilla-JS reimplementation outside React |
| **3. SCORM JS bridge** | `window.API` discovery, `LMSInitialize`/`SetValue`/`Commit`/`Finish` calls, resume-via-`lesson_location` | Medium risk if hand-rolled from scratch; **lower if the `scorm-again` spike (Part 3) pans out** — this is the single biggest lever on this chunk's size |
| **4. Quiz packaging + score reporting** | **Blocked on the Part 6 product decision.** If answers-in-package is approved: straightforward client-side grading + SCORM score/status reporting, similar complexity to chunk 2. If not approved: this chunk doesn't have a clean standalone solution within stated constraints and needs a design escalation, not just more implementation time | **Highest-uncertainty chunk in the whole estimate** — get this decision made early, it gates real work |
| **5. Video handling** | URL-reference `&lt;video&gt;` embed (Part 5 recommendation) + progress-to-SCORM-API wiring, reusing the existing unique-seconds-watched algorithm client-side | Low-medium risk — algorithm already exists and is portable; embedding option (if ever added) would be materially more work (zip size handling, R2 fetch-and-bundle) |
| **6. Export UI + endpoint** | New controller action (Part 7), zip assembly via stock `System.IO.Compression`, language multi-select UI reusing the existing admin translations endpoint | Low-medium risk — no ZIP infra exists today (net-new), but follows established generate-then-download/upload patterns (certificate PDF, QuestPDF reports) |
| **7. Multi-language iteration** | Loop chunks 1-6 per selected language, using `ToolboxTalkTranslation.TranslatedSections`/`TranslatedQuestions` JSON directly | Low risk — confirmed no shared-cost architecture is needed; genuinely just N iterations |
| **8. Testing/validation** | SCORM Cloud manual pass, Moodle self-hosted pass, CI-automation spike (contingent) | Medium effort due to the now-nontrivial Moodle self-hosting setup (Part 8) and the unresolved SCORM Cloud API-shape question — plan for hands-on spike time, not just "click validate" |

### Unknowns that would meaningfully change the estimate

1. **The Part 6 quiz-answer-exposure decision is the single biggest swing factor.** If answers-in-package is rejected, chunk 4 has no clean solution under the stated "no backend" constraint and the whole SCORM-export concept may need a scope renegotiation (e.g., "host LMS records completion, but our quiz still requires a network call back to us" — which contradicts "no backend" as currently framed). This should be resolved before committing to a build timeline.
2. **Whether admin-authored section HTML ever contains inline `&lt;img&gt;` tags with relative paths.** The recon found no evidence of an image-insertion UI feeding section content, and the storage convention throughout the app is absolute public R2 URLs — but freeform admin-pasted HTML isn't schema-constrained, so this couldn't be fully confirmed. If it turns out relative-path images are common in practice, chunk 2 grows to include a path-rewriting/asset-collection pass.
3. **Whether `scorm-again` genuinely fits our export shape.** If the spike shows friction, chunk 3 reverts to a hand-rolled bridge (~150 lines based on the `pipwerks` model) — not a blocker, just a size difference in that one chunk.
4. **Whether the SCORM Cloud API has a real synchronous validation endpoint.** If it doesn't, chunk 8's CI-automation stretch goal simply doesn't happen for v1, and testing stays manual — not a blocker for shipping, but changes what "automated validation" can mean in this project going forward.
5. **Real per-LMS import size ceilings**, if the video-embedding option is ever revisited — not relevant to the recommended v1 (URL-reference) approach, but worth surfacing now so it isn't rediscovered as a surprise later.

---

## Sources consulted

- NuGet.org SCORM package search; GitHub topic search (`github.com/topics/scorm`)
- `RusticiSoftware/SCORMCloud_NetLibrary`, `RusticiSoftware/scormcloud-api-v2-client-net`, `musicm122/ScormToolkit`
- `pipwerks/SCORM-Manifests`, `pipwerks/scorm-api-wrapper`, `jcputney/scorm-again`
- SCORM Cloud API Documentation, V2 API Overview, Getting Started guide, Content Import Options, pricing/trial page, Registration Postbacks docs
- Articulate community, LearnUpon support docs, Moodle forums (SCORM 1.2 completion behavior, encoding issues), SAP SuccessFactors help docs (SCORM 1.2/AICC common problems, cross-domain communication), Drupal Opigno SCORM module issue tracker
- Bitnami/Moodle Docker deprecation reporting (Northflank blog, Moodle forum thread), `moodlehq/moodle-docker`
- Codebase: `web/src/features/toolbox-talks/components/{TalkViewer,SectionContent,VideoPlayer,QuizSection,HtmlSlideshow,Slideshow}.tsx`; `src/Modules/ToolboxTalks/.../Entities/{ToolboxTalkSection,ToolboxTalkTranslation}.cs`; `GetMyToolboxTalkByIdQueryHandler.cs`; `SubmitQuizAnswersCommandHandler.cs`; `ToolboxTalkFilesController.cs`; `MyToolboxTalksController.cs`; `R2StorageSettings.cs`; `CertificateGenerationService.cs`; `ToolboxTalkExportService.cs`
