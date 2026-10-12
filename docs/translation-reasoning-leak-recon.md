# Translation reasoning-leak recon (read-only)

Date: 2026-10-12. No code changes made. Paths are relative to `src/Modules/ToolboxTalks/`.

Short version: section translation is **Claude (Sonnet), not Gemini**. The Claude call sends no `thinking` config, and the response parser takes the first `text` block only, so the leak is not a parser bug that joins thought parts. The reasoning text is inside the model's own text output. Nothing in the pipeline checks for it before it is persisted.

## 1. Which path and model produced section translations

OBSERVED
- Section titles and content go through `ContentTranslationService.TranslateTextAsync` (`QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/ContentTranslationService.cs:45`).
- It posts to `{SubtitleProcessing.Claude.BaseUrl}/messages` with `model = aiProviders.Anthropic.Models.Sonnet` (`ContentTranslationService.cs:38`, `:263-273`). `appsettings.json:30` sets Sonnet to `claude-sonnet-4-5`.
- Two callers produce stored section translations:
  - `Jobs/TranslationValidationJob.cs:1051` (`GenerateTranslationForSectionsAsync`), used by the wizard and validation flow. `isHtml: true` is hardcoded.
  - `Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs:311`, used by MissingTranslationsJob, DailyTranslationScanJob and smart-generate reuse. `isHtml: true` is hardcoded.
- Gemini is **not** a translation provider. It exists only as Round 2 back-translation (`Services/Validation/GeminiTranslationService.cs`, `gemini-2.0-flash` from `AIProviders:Gemini:Models:Flash`). "Gemini 3.6 flash" does not appear anywhere in the repo.

INFERRED / NOT VERIFIABLE FROM REPO
- Production env vars (`AIProviders__Anthropic__Models__Sonnet`) can override the model. Check what Production actually resolves. A reasoning-leaking model class (an extended-thinking or newer reasoning model) would explain the symptom better than `claude-sonnet-4-5`.
- I cannot tell from code which of the two callers produced this Afrikaans row. `TranslatedSections` is stored identically by both. `ToolboxTalkTranslation.TranslationProvider` may help (see query notes).

## 2. How the response text is extracted

OBSERVED
- `Services/AnthropicResponseParser.cs:20-30` iterates `content[]` and returns the **first** item that has a `text` property, then `break`s. It does not join parts.
- A `thinking` block has a `thinking` property and no `text`, so thinking blocks would be skipped, not leaked.
- The request body (`ContentTranslationService.cs:263-271`) contains only `model`, `max_tokens`, `messages`. No `thinking`, no `system`, no prefill, no stop sequences, no temperature. Extended thinking is therefore not enabled by us.
- Consequence: the reasoning is in the text block itself. The model wrote it as plain output. The "Let me think more carefully" style fits a model that was not given a thinking channel and reasoned in-line.
- Gemini parser (`GeminiTranslationService.cs:165-188`) also returns the first part with `text`. It does not filter `thought: true` parts, and it sets no `thinkingConfig`. With a Gemini 2.5+/3.x model that thinks by default, `includeThoughts` defaults to false, so thoughts are not returned. Low risk, but it does not defend against it either.

INFERRED
- The prompt puts the source text last with no delimiter (`TranslationPrompts.cs:86-88`, `:187-191`). The model can read the trailing HTML as ambiguous and start by talking about the task. A system prompt, an XML wrapper around the source, or a response prefill would reduce that. Not tested.

## 3. Call sites sharing the extraction

All use `AnthropicResponseParser.Parse` (first text block, no leak filter):

| Call site | Purpose |
|---|---|
| `ContentTranslationService.CallClaudeApiAsync` (`:300`) | Section/title/description/quiz/option/slide/email translation. Used by `TranslationValidationJob` (1040, 1051, 1094, 1105, 1127, 1144), `GenerateContentTranslationsCommandHandler` (256-610), `ContentCreationSessionService` (1644, 1654, 2102, 2112), LessonParser `TranslationQueueService` (192, 214) |
| `ContentTranslationService.TranslateBatchAsync` | Batch JSON-array translation. Less exposed: `ParseBatchResponse` slices from first `[` to last `]` |
| `ContentTranslationService.SendCustomPromptAsync` | Generic prompt |
| `Subtitles/ClaudeTranslationService.cs:85` | SRT translation (subtitles). Output stored in `SubtitleTranslations.SrtContent` |
| `Validation/ClaudeHaikuBackTranslationService.cs:107` | Round 1 back-translation |
| `Validation/ClaudeSonnetBackTranslationService.cs:108` | Round 3 back-translation |
| `Validation/RegulatoryScoreService.cs:511`, `PreFlightScanService.cs:146`, `DialectDetectionService.cs:163` | Scoring and scanning (JSON) |
| `AiSectionGenerationService.cs:120`, `AiQuizGenerationService.cs:123,253`, `ContentCreation/ContentParserService.cs:134`, `Slideshow/AiSlideshowGenerationService.cs:409` | Content generation |
| `RequirementIngestionJob.cs:912`, `RequirementMappingJob.cs:320` | Regulatory |
| `API/Controllers/HelpChatController.cs:86` | Help chat |

Gemini extraction (`ParseGeminiResponse`) is used only by the Gemini back-translator.

Slideshow translation is per-string through `TranslateTextAsync(..., isHtml:false)` (`GenerateContentTranslationsCommandHandler.cs:607`), so slideshow and slide text share the exposure. Short strings are the most likely to get a chatty preface.

## 4. Does validation run on this content, and why did it not flag it?

OBSERVED
- Only the `TranslationValidationJob` path validates. `GenerateContentTranslationsCommandHandler` (MissingTranslationsJob, DailyTranslationScanJob, reuse) has no validation step at all. If this Afrikaans row came from that path, nothing ever scored it.
- On the validated path, `TranslationValidationService.ValidateSectionAsync` back-translates the persisted text and scores it against the English original with `LexicalScoringService`: `matched tokens / max(original tokens, candidate tokens)`. The leaked English preamble back-translates to roughly itself. For a long section, a 40-60 word preamble drops the score by a few points only. The Pass threshold is 75 (85 for safety-critical), Review is threshold minus 15. A long section with a short preamble can still Pass.
- `ArtefactScanService` checks untranslated carried-over English words (more than 2 words, 4+ letters, that also appear in the source), truncation, duplicated 4-word phrases, collapsed bullets, stray numbers. It has **no rule for reasoning or meta text**. Preamble words like "translate", "content" only count if they also appear in the source. An artefact hit also only downgrades Pass to Review.
- Reviewers may have Accepted a Review without noticing, and `AcceptSection` propagates `EditedTranslation` only, so the original leaked text stays published.

INFERRED
- The Haiku/Gemini/Sonnet back-translators could themselves paraphrase or drop the preamble, which would make the score slightly worse, not better. I did not run a sample, so the exact score is unknown. Pull the `TranslationValidationResults` row for this section (Production) and check `FinalScore`, `ReviewReasonsJson`, `ArtefactsJson`, `ReviewerDecision`.

## 5. Scope queries (read-only, final)

Verified against the EF model snapshot and the local database (`information_schema`). Both queries are plain SELECTs. Run them as two separate statements (each has its own copy of the CTEs).

### Schema verification

Confirmed:
- `toolbox_talks."TranslationValidationResults"."ValidationRunId"` is the FK to `toolbox_talks."TranslationValidationRuns"."Id"`.
- `Tenants` is `public."Tenants"` (snapshot: `ToTable("Tenants", (string)null)`), columns `Id`, `Name`.
- All other tables are in schema `toolbox_talks`.

Corrected from my first draft:
- `ToolboxTalkSlides` has its own `TenantId`, so the slide-text branch no longer joins `ToolboxTalks`.
- `ToolboxTalkTranslations` has its own `TenantId`, so those branches no longer join `ToolboxTalks`.
- `SubtitleProcessingJobs` has `TenantId`, so subtitles take the tenant from the job.
- `ToolboxTalkSlideshowTranslations` and `ToolboxTalkCourseTranslations` have no `TenantId`, so they still join the talk or course.
- `TranslationValidationRuns` has both `ToolboxTalkId` and `CourseId`, and `TenantId`. The parent id is `COALESCE(ToolboxTalkId, CourseId)`.
- Removed the odd `CROSS JOIN LATERAL` on the validation branches; tenant comes straight from the run.
- Dropped `TranslationProvider` and `TranslatedAt` from the output. Timestamps are `CreatedAt` and `UpdatedAt` on every table (confirmed present on all 12), so every branch returns the same shape.

Each result row returns: tenant name, tenant id, source (table.column), entity id (the translation row), talk or course id, language, created_at, updated_at, confidence tier, 100-char preview.

### Local execution (read-only)

Run with `PGOPTIONS="-c default_transaction_read_only=on"` against `rascor_stock` on 127.0.0.1. `show default_transaction_read_only` returned `on`.
- Detail query: executed, 0 rows. Local has 13 ToolboxTalkTranslations, 55 TranslationValidationResults and 5 SubtitleTranslations rows, none containing the markers.
- Summary query: executed, 1 row: `(ALL TENANTS) / (ALL DAYS)`, 0 affected rows, null earliest and latest.
- Because local returned nothing, I also ran both against an inline `VALUES` stand-in for `src` containing one leaked row, one 'Wait,' row and one clean row. Detail returned 2 rows (1 high, 1 low) with correct previews; summary returned per-tenant-per-day rows plus tenant and overall subtotals. This proves the regex, preview and grouping logic, not the production data.

### Paste-ready queries

Tiers: `high` = phrases that effectively never occur in real translated training content. `low` = short markers ('Wait,', 'Actually,', 'Hmm', 'Okay, so', 'Let me ') that can false-positive on untranslated English. The timestamp used for day grouping is `COALESCE(UpdatedAt, CreatedAt)`.

```sql
-- ===== QUERY 1: DETAIL (read-only) =====
WITH params AS (
  SELECT
    '(The user (wants|is asking|asked|has asked)|Let me (think|re-?read|check|reconsider|re-?translate|look|start|analy[sz]e)|I need to (translate|keep|preserve|make sure)|I should (translate|keep|preserve)|I''ll (translate|keep|preserve)|Looking at (this|the) (text|content|html)|Here is the (translation|translated))'::text AS high_re,
    '(Wait,|Actually,|Hmm|Okay, so|Let me )'::text AS low_re
),
src AS (
  SELECT tt."TenantId" AS tenant_id, 'ToolboxTalkTranslations.TranslatedTitle' AS source, tt."Id" AS entity_id, tt."ToolboxTalkId" AS parent_id,
         tt."LanguageCode" AS language, tt."CreatedAt" AS created_at, tt."UpdatedAt" AS updated_at, tt."TranslatedTitle"::text AS txt
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.TranslatedDescription', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."TranslatedDescription"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.TranslatedSections (JSON)', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."TranslatedSections"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.TranslatedQuestions (JSON)', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."TranslatedQuestions"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.EmailSubject', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."EmailSubject"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.EmailBody', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."EmailBody"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT t."TenantId", 'ToolboxTalkSlideshowTranslations.TranslatedHtml', st."Id", st."ToolboxTalkId", st."LanguageCode", st."CreatedAt", st."UpdatedAt", st."TranslatedHtml"::text
    FROM toolbox_talks."ToolboxTalkSlideshowTranslations" st
    JOIN toolbox_talks."ToolboxTalks" t ON t."Id" = st."ToolboxTalkId"
    WHERE NOT st."IsDeleted"
  UNION ALL
  SELECT s."TenantId", 'ToolboxTalkSlideTranslations.TranslatedText', sl."Id", s."ToolboxTalkId", sl."LanguageCode", sl."CreatedAt", sl."UpdatedAt", sl."TranslatedText"::text
    FROM toolbox_talks."ToolboxTalkSlideTranslations" sl
    JOIN toolbox_talks."ToolboxTalkSlides" s ON s."Id" = sl."SlideId"
    WHERE NOT sl."IsDeleted"
  UNION ALL
  SELECT c."TenantId", 'ToolboxTalkCourseTranslations.TranslatedTitle', ct."Id", ct."CourseId", ct."LanguageCode", ct."CreatedAt", ct."UpdatedAt", ct."TranslatedTitle"::text
    FROM toolbox_talks."ToolboxTalkCourseTranslations" ct
    JOIN toolbox_talks."ToolboxTalkCourses" c ON c."Id" = ct."CourseId"
    WHERE NOT ct."IsDeleted"
  UNION ALL
  SELECT c."TenantId", 'ToolboxTalkCourseTranslations.TranslatedDescription', ct."Id", ct."CourseId", ct."LanguageCode", ct."CreatedAt", ct."UpdatedAt", ct."TranslatedDescription"::text
    FROM toolbox_talks."ToolboxTalkCourseTranslations" ct
    JOIN toolbox_talks."ToolboxTalkCourses" c ON c."Id" = ct."CourseId"
    WHERE NOT ct."IsDeleted"
  UNION ALL
  SELECT j."TenantId", 'SubtitleTranslations.SrtContent', sub."Id", j."ToolboxTalkId", sub."LanguageCode", sub."CreatedAt", sub."UpdatedAt", sub."SrtContent"::text
    FROM toolbox_talks."SubtitleTranslations" sub
    JOIN toolbox_talks."SubtitleProcessingJobs" j ON j."Id" = sub."SubtitleProcessingJobId"
    WHERE NOT sub."IsDeleted"
  UNION ALL
  SELECT run."TenantId", 'TranslationValidationResults.TranslatedText', vr."Id", COALESCE(run."ToolboxTalkId", run."CourseId"), run."LanguageCode", vr."CreatedAt", vr."UpdatedAt", vr."TranslatedText"::text
    FROM toolbox_talks."TranslationValidationResults" vr
    JOIN toolbox_talks."TranslationValidationRuns" run ON run."Id" = vr."ValidationRunId"
    WHERE NOT vr."IsDeleted"
  UNION ALL
  SELECT run."TenantId", 'TranslationValidationResults.EditedTranslation', vr."Id", COALESCE(run."ToolboxTalkId", run."CourseId"), run."LanguageCode", vr."CreatedAt", vr."UpdatedAt", vr."EditedTranslation"::text
    FROM toolbox_talks."TranslationValidationResults" vr
    JOIN toolbox_talks."TranslationValidationRuns" run ON run."Id" = vr."ValidationRunId"
    WHERE NOT vr."IsDeleted"
),
hits AS (
  SELECT
    src.*,
    COALESCE(src.updated_at, src.created_at) AS changed_at,
    CASE WHEN src.txt ~ p.high_re THEN 'high' ELSE 'low' END AS confidence,
    left(regexp_replace(
           substring(src.txt from '((?:' || p.high_re || '|' || p.low_re || ').{0,120})'),
           '\s+', ' ', 'g'), 100) AS preview
  FROM src CROSS JOIN params p
  WHERE src.txt IS NOT NULL
    AND (src.txt ~ p.high_re OR src.txt ~ p.low_re)
)
SELECT
  src_t."Name"      AS tenant,
  h.tenant_id,
  h.source,
  h.entity_id,
  h.parent_id       AS talk_or_course_id,
  h.language,
  h.created_at,
  h.updated_at,
  h.confidence,
  h.preview
FROM hits h
LEFT JOIN public."Tenants" src_t ON src_t."Id" = h.tenant_id
ORDER BY h.confidence, tenant, h.source, h.language, h.changed_at;

-- ===== QUERY 2: SUMMARY per tenant and day, with tenant subtotal and overall earliest/latest (read-only) =====
WITH params AS (
  SELECT
    '(The user (wants|is asking|asked|has asked)|Let me (think|re-?read|check|reconsider|re-?translate|look|start|analy[sz]e)|I need to (translate|keep|preserve|make sure)|I should (translate|keep|preserve)|I''ll (translate|keep|preserve)|Looking at (this|the) (text|content|html)|Here is the (translation|translated))'::text AS high_re,
    '(Wait,|Actually,|Hmm|Okay, so|Let me )'::text AS low_re
),
src AS (
  SELECT tt."TenantId" AS tenant_id, 'ToolboxTalkTranslations.TranslatedTitle' AS source, tt."Id" AS entity_id, tt."ToolboxTalkId" AS parent_id,
         tt."LanguageCode" AS language, tt."CreatedAt" AS created_at, tt."UpdatedAt" AS updated_at, tt."TranslatedTitle"::text AS txt
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.TranslatedDescription', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."TranslatedDescription"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.TranslatedSections (JSON)', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."TranslatedSections"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.TranslatedQuestions (JSON)', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."TranslatedQuestions"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.EmailSubject', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."EmailSubject"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT tt."TenantId", 'ToolboxTalkTranslations.EmailBody', tt."Id", tt."ToolboxTalkId", tt."LanguageCode", tt."CreatedAt", tt."UpdatedAt", tt."EmailBody"::text
    FROM toolbox_talks."ToolboxTalkTranslations" tt WHERE NOT tt."IsDeleted"
  UNION ALL
  SELECT t."TenantId", 'ToolboxTalkSlideshowTranslations.TranslatedHtml', st."Id", st."ToolboxTalkId", st."LanguageCode", st."CreatedAt", st."UpdatedAt", st."TranslatedHtml"::text
    FROM toolbox_talks."ToolboxTalkSlideshowTranslations" st
    JOIN toolbox_talks."ToolboxTalks" t ON t."Id" = st."ToolboxTalkId"
    WHERE NOT st."IsDeleted"
  UNION ALL
  SELECT s."TenantId", 'ToolboxTalkSlideTranslations.TranslatedText', sl."Id", s."ToolboxTalkId", sl."LanguageCode", sl."CreatedAt", sl."UpdatedAt", sl."TranslatedText"::text
    FROM toolbox_talks."ToolboxTalkSlideTranslations" sl
    JOIN toolbox_talks."ToolboxTalkSlides" s ON s."Id" = sl."SlideId"
    WHERE NOT sl."IsDeleted"
  UNION ALL
  SELECT c."TenantId", 'ToolboxTalkCourseTranslations.TranslatedTitle', ct."Id", ct."CourseId", ct."LanguageCode", ct."CreatedAt", ct."UpdatedAt", ct."TranslatedTitle"::text
    FROM toolbox_talks."ToolboxTalkCourseTranslations" ct
    JOIN toolbox_talks."ToolboxTalkCourses" c ON c."Id" = ct."CourseId"
    WHERE NOT ct."IsDeleted"
  UNION ALL
  SELECT c."TenantId", 'ToolboxTalkCourseTranslations.TranslatedDescription', ct."Id", ct."CourseId", ct."LanguageCode", ct."CreatedAt", ct."UpdatedAt", ct."TranslatedDescription"::text
    FROM toolbox_talks."ToolboxTalkCourseTranslations" ct
    JOIN toolbox_talks."ToolboxTalkCourses" c ON c."Id" = ct."CourseId"
    WHERE NOT ct."IsDeleted"
  UNION ALL
  SELECT j."TenantId", 'SubtitleTranslations.SrtContent', sub."Id", j."ToolboxTalkId", sub."LanguageCode", sub."CreatedAt", sub."UpdatedAt", sub."SrtContent"::text
    FROM toolbox_talks."SubtitleTranslations" sub
    JOIN toolbox_talks."SubtitleProcessingJobs" j ON j."Id" = sub."SubtitleProcessingJobId"
    WHERE NOT sub."IsDeleted"
  UNION ALL
  SELECT run."TenantId", 'TranslationValidationResults.TranslatedText', vr."Id", COALESCE(run."ToolboxTalkId", run."CourseId"), run."LanguageCode", vr."CreatedAt", vr."UpdatedAt", vr."TranslatedText"::text
    FROM toolbox_talks."TranslationValidationResults" vr
    JOIN toolbox_talks."TranslationValidationRuns" run ON run."Id" = vr."ValidationRunId"
    WHERE NOT vr."IsDeleted"
  UNION ALL
  SELECT run."TenantId", 'TranslationValidationResults.EditedTranslation', vr."Id", COALESCE(run."ToolboxTalkId", run."CourseId"), run."LanguageCode", vr."CreatedAt", vr."UpdatedAt", vr."EditedTranslation"::text
    FROM toolbox_talks."TranslationValidationResults" vr
    JOIN toolbox_talks."TranslationValidationRuns" run ON run."Id" = vr."ValidationRunId"
    WHERE NOT vr."IsDeleted"
),
hits AS (
  SELECT
    src.*,
    COALESCE(src.updated_at, src.created_at) AS changed_at,
    CASE WHEN src.txt ~ p.high_re THEN 'high' ELSE 'low' END AS confidence,
    left(regexp_replace(
           substring(src.txt from '((?:' || p.high_re || '|' || p.low_re || ').{0,120})'),
           '\s+', ' ', 'g'), 100) AS preview
  FROM src CROSS JOIN params p
  WHERE src.txt IS NOT NULL
    AND (src.txt ~ p.high_re OR src.txt ~ p.low_re)
)
SELECT
  CASE WHEN GROUPING(h.tenant_id) = 1 THEN '(ALL TENANTS)' ELSE COALESCE(MAX(t."Name"), h.tenant_id::text) END AS tenant,
  CASE WHEN GROUPING(h.changed_at::date) = 1 THEN '(ALL DAYS)' ELSE (h.changed_at::date)::text END AS day,
  COUNT(*)                                             AS affected_rows,
  COUNT(*) FILTER (WHERE h.confidence = 'high')        AS high_confidence_rows,
  MIN(h.changed_at)                                    AS earliest,
  MAX(h.changed_at)                                    AS latest
FROM hits h
LEFT JOIN public."Tenants" t ON t."Id" = h.tenant_id
GROUP BY GROUPING SETS ((h.tenant_id, h.changed_at::date), (h.tenant_id), ())
ORDER BY GROUPING(h.tenant_id), 1, GROUPING(h.changed_at::date), 2;
```

Notes
- Soft-deleted rows are excluded (`NOT "IsDeleted"`). Remove those predicates to include history.
- JSON columns are searched as raw text, so matches inside escaped strings still hit; previews may show escapes such as `<`.
- Not covered: wizard `ContentCreationSessions` JSON state and R2 `subs/` SRT objects (`SubtitleTranslations.SrtContent` may be null if only `SrtUrl` is stored).
- The `(ALL DAYS)` row under a tenant is that tenant's subtotal. The `(ALL TENANTS) / (ALL DAYS)` row gives the overall count, earliest and latest.

## 6. Why the prompt says "HTML content" for plain text

OBSERVED
- `TranslationPrompts.cs:30`: `contentType = isHtml ? "HTML content" : "text"`. The generic prompt at `:187` does the same.
- `isHtml` is decided by the **caller as a literal**, never inferred from the text:
  - Section content: `true` at `TranslationValidationJob.cs:1051-1052` and `GenerateContentTranslationsCommandHandler.cs:311-312`.
  - Section title, talk title, description, question text, options, slide text, email, slideshow strings: `false`.
- Rationale in the model: sections are stored as HTML (`ToolboxTalkSection.Content`, and the content-parser prompt asks for `"<p>HTML content here</p>"`). So a section whose body is plain text, or whose HTML is only wrapper tags, is still labelled "HTML content". The instruction "Keep all HTML tags exactly as they are" with no tags to keep is a plausible trigger for the model to deliberate in its output. That is INFERRED, not tested.

## Notable risks

1. **No output guard anywhere.** Nothing rejects a translation that contains meta text before it is saved to `ToolboxTalkTranslations.TranslatedSections`. A cheap check (marker regex, or response not starting with `<` for HTML, or length ratio versus source) would catch this class at write time.
2. **Unvalidated path.** MissingTranslationsJob and DailyTranslationScanJob persist translations with no validation run, so a leak there is never scored or reviewed.
3. **Validation is weak against appended English.** The lexical score is diluted but not failed by a short preamble, and the artefact scanner has no meta-text rule.
4. **Reviewer Accept does not clean the text.** Accepting a Review row keeps the leaked text published; only explicit Edit replaces it.
5. **Model drift on Production.** The leak style suggests a model config worth confirming against `appsettings.json` (`claude-sonnet-4-5`).
6. **Same exposure for quiz, slideshow strings, titles and SRT.** Short strings have the highest chance of a chatty preface and no length-ratio check.
