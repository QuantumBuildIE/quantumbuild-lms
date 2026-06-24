-- ============================================================
-- Backfill: ToolboxTalk.TargetLanguageCodes
-- Root cause: GenerateContentTranslationsCommandHandler created
--   ToolboxTalkTranslation rows but never appended the language code
--   to ToolboxTalk.TargetLanguageCodes.  Both the workflow-state
--   endpoint and the Detail-page TranslateStep read ONLY
--   TargetLanguageCodes to decide which languages to show, so any
--   language translated via the old-wizard path was invisible.
-- Fixed in code 2026-06-24.  This script repairs pre-fix rows.
-- ============================================================
--
-- OPERATOR RUNBOOK
-- ----------------
-- 1. Take a full database backup before running on Demo or Production.
-- 2. Open this file in pgAdmin and connect to the target database.
-- 3. Execute the entire script up to (but NOT including) the final
--    COMMIT / ROLLBACK block.
-- 4. Inspect the "Step 1 dry-run" result set:
--      talk_id              — UUID of the affected ToolboxTalk
--      tenant_id            — which tenant owns it
--      current_target_codes — what TargetLanguageCodes holds right now
--      translation_codes    — all codes present in ToolboxTalkTranslations
--      codes_to_add         — what WILL be appended by the UPDATE
--    Expected: ISO codes like ["pl","ru","de"].
--    If any row looks wrong (unexpected UUIDs, odd codes), ROLLBACK.
-- 5. Inspect the "Step 3 verification" result set:
--    Expected: 0 rows (nothing left to repair).
--    If rows appear the UPDATE failed — ROLLBACK and investigate.
-- 6. If both checks pass, execute COMMIT (last statement in the file).
-- 7. Run the script a second time to confirm Step 1 returns 0 rows
--    (idempotency check — safe to run multiple times).
-- 8. Spot-check a previously-broken talk in the UI:
--    Detail page > Translations tab — the newly-added language should
--    now appear.
-- ============================================================

BEGIN;

-- ============================================================
-- Step 1: Dry-run — talks that need TargetLanguageCodes repair
--
-- Review this output before deciding to COMMIT or ROLLBACK.
-- Each row is a talk whose ToolboxTalkTranslations contain at least
-- one language code absent from TargetLanguageCodes.
-- ============================================================

WITH safe_target AS (
    -- Parse TargetLanguageCodes defensively: null / empty / non-array → []
    SELECT
        t."Id",
        t."TenantId",
        t."TargetLanguageCodes",
        CASE
            WHEN t."TargetLanguageCodes" IS NULL
              OR length(trim(t."TargetLanguageCodes")) = 0
                THEN '[]'::jsonb
            WHEN left(trim(t."TargetLanguageCodes"), 1) = '['
                THEN t."TargetLanguageCodes"::jsonb
            ELSE '[]'::jsonb
        END AS current_codes
    FROM "ToolboxTalks" t
    WHERE t."IsDeleted" = false
),
translation_codes AS (
    -- Collect all distinct lowercased language codes for each talk
    SELECT
        tr."ToolboxTalkId",
        jsonb_agg(
            DISTINCT lower(tr."LanguageCode")
            ORDER BY lower(tr."LanguageCode")
        ) AS codes
    FROM "ToolboxTalkTranslations" tr
    WHERE tr."IsDeleted" = false
    GROUP BY tr."ToolboxTalkId"
),
missing AS (
    SELECT
        s."Id",
        s."TenantId",
        s."TargetLanguageCodes" AS current_target_codes,
        tc.codes                AS translation_codes,
        (
            SELECT COALESCE(jsonb_agg(code ORDER BY code), '[]'::jsonb)
            FROM jsonb_array_elements_text(tc.codes) AS code
            WHERE NOT (s.current_codes @> to_jsonb(code))
        ) AS codes_to_add
    FROM safe_target s
    INNER JOIN translation_codes tc ON tc."ToolboxTalkId" = s."Id"
)
SELECT
    m."Id"                              AS talk_id,
    m."TenantId"                        AS tenant_id,
    m.current_target_codes,
    m.translation_codes,
    m.codes_to_add,
    jsonb_array_length(m.codes_to_add)  AS codes_to_add_count
FROM missing m
WHERE jsonb_array_length(m.codes_to_add) > 0
ORDER BY m."TenantId", m."Id";


-- ============================================================
-- Step 2: UPDATE — append missing codes to TargetLanguageCodes
--
-- For each qualifying talk, merges the existing TargetLanguageCodes
-- array with all translation codes not yet present in it.
-- Talks already up-to-date are untouched (idempotent).
-- ============================================================

UPDATE "ToolboxTalks" t
SET
    "TargetLanguageCodes" = (
        -- existing codes (safe parse) || new codes only
        CASE
            WHEN t."TargetLanguageCodes" IS NULL
              OR length(trim(t."TargetLanguageCodes")) = 0
                THEN '[]'::jsonb
            WHEN left(trim(t."TargetLanguageCodes"), 1) = '['
                THEN t."TargetLanguageCodes"::jsonb
            ELSE '[]'::jsonb
        END
        ||
        COALESCE(
            (
                -- aggregate only the codes that are missing from TargetLanguageCodes
                SELECT jsonb_agg(
                    DISTINCT lower(tr."LanguageCode")
                    ORDER BY lower(tr."LanguageCode")
                )
                FROM "ToolboxTalkTranslations" tr
                WHERE tr."ToolboxTalkId" = t."Id"
                  AND tr."IsDeleted" = false
                  AND NOT (
                      CASE
                          WHEN t."TargetLanguageCodes" IS NULL
                            OR length(trim(t."TargetLanguageCodes")) = 0
                              THEN '[]'::jsonb
                          WHEN left(trim(t."TargetLanguageCodes"), 1) = '['
                              THEN t."TargetLanguageCodes"::jsonb
                          ELSE '[]'::jsonb
                      END @> to_jsonb(lower(tr."LanguageCode"))
                  )
            ),
            '[]'::jsonb
        )
    )::text,
    "UpdatedAt" = NOW(),
    "UpdatedBy" = 'backfill-target-language-codes'
WHERE t."IsDeleted" = false
  AND EXISTS (
      -- only touch rows that actually need repair (skip already-correct talks)
      SELECT 1
      FROM "ToolboxTalkTranslations" tr
      WHERE tr."ToolboxTalkId" = t."Id"
        AND tr."IsDeleted" = false
        AND NOT (
            CASE
                WHEN t."TargetLanguageCodes" IS NULL
                  OR length(trim(t."TargetLanguageCodes")) = 0
                    THEN '[]'::jsonb
                WHEN left(trim(t."TargetLanguageCodes"), 1) = '['
                    THEN t."TargetLanguageCodes"::jsonb
                ELSE '[]'::jsonb
            END @> to_jsonb(lower(tr."LanguageCode"))
        )
  );


-- ============================================================
-- Step 3: Verification — re-run the dry-run after the UPDATE
--
-- Expected: 0 rows.
-- If any rows appear, the UPDATE did not fully repair all talks —
-- do NOT commit; investigate and ROLLBACK.
-- ============================================================

WITH safe_target AS (
    SELECT
        t."Id",
        t."TenantId",
        t."TargetLanguageCodes",
        CASE
            WHEN t."TargetLanguageCodes" IS NULL
              OR length(trim(t."TargetLanguageCodes")) = 0
                THEN '[]'::jsonb
            WHEN left(trim(t."TargetLanguageCodes"), 1) = '['
                THEN t."TargetLanguageCodes"::jsonb
            ELSE '[]'::jsonb
        END AS current_codes
    FROM "ToolboxTalks" t
    WHERE t."IsDeleted" = false
),
translation_codes AS (
    SELECT
        tr."ToolboxTalkId",
        jsonb_agg(
            DISTINCT lower(tr."LanguageCode")
            ORDER BY lower(tr."LanguageCode")
        ) AS codes
    FROM "ToolboxTalkTranslations" tr
    WHERE tr."IsDeleted" = false
    GROUP BY tr."ToolboxTalkId"
),
missing AS (
    SELECT
        s."Id",
        s."TenantId",
        s."TargetLanguageCodes" AS updated_target_codes,
        tc.codes                AS translation_codes,
        (
            SELECT COALESCE(jsonb_agg(code ORDER BY code), '[]'::jsonb)
            FROM jsonb_array_elements_text(tc.codes) AS code
            WHERE NOT (s.current_codes @> to_jsonb(code))
        ) AS remaining_missing
    FROM safe_target s
    INNER JOIN translation_codes tc ON tc."ToolboxTalkId" = s."Id"
)
SELECT
    m."Id"              AS talk_id,
    m."TenantId"        AS tenant_id,
    m.updated_target_codes,
    m.translation_codes,
    m.remaining_missing
FROM missing m
WHERE jsonb_array_length(m.remaining_missing) > 0
ORDER BY m."TenantId", m."Id";

-- ============================================================
-- If Step 1 dry-run looked correct AND Step 3 returns 0 rows:
--   execute COMMIT below.
-- If anything looks wrong:
--   execute ROLLBACK instead.
-- ============================================================

COMMIT;
-- ROLLBACK;
