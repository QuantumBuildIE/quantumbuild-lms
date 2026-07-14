-- ============================================================
-- Backfill: Stuck translation-validation workflow events
-- Root cause: On re-runs, TranslationValidationJob.LoadSectionsAsync
--   returns cached translations directly without calling
--   GenerateTranslationForSectionsAsync, so RecordTranslationCompleted
--   is never called.  State stays Translating; StartValidation and
--   RecordValidationCompleted both fail their guards silently; no
--   ValidationCompleted event is ever written.  The run row is marked
--   Completed correctly, but the workflow state machine is stuck at
--   Translating, causing the frontend to spin forever.
-- Fixed in code 2026-06-24.  This script repairs pre-fix rows.
-- ============================================================
--
-- OPERATOR RUNBOOK
-- ----------------
-- 1. Take a full database backup before running on Demo or Production.
-- 2. Open this file in pgAdmin and connect to the target database.
-- 3. Execute the entire script up to (but NOT including) the final
--    COMMIT line.
-- 4. Inspect the "Step 1 dry-run" result set:
--      talk_id       — UUID of the ToolboxTalk
--      language_code — BCP-47 code for the stuck language
--      tenant_id     — which tenant owns it
--      stuck_event   — the transient event type that has no follow-up
--      stuck_since   — when the event was written (UTC)
--    Expected: one row per stuck talk+language pair.
--    If anything looks wrong (unexpected UUIDs, wrong events), ROLLBACK.
-- 5. Inspect the "Step 3 verification" result set:
--    Expected: 0 rows (nothing left to repair).
--    If rows appear, the INSERT did not cover all stuck pairs — ROLLBACK
--    and investigate.
-- 6. If both checks pass, execute COMMIT (last statement in the file).
-- 7. Run the script a second time to confirm Step 1 returns 0 rows
--    (idempotency check — safe to run multiple times).
-- 8. Spot-check a previously-stuck talk in the UI:
--    Detail page > Translations tab — the language should now show
--    "Validated" state (not "Translating...") and the spinner should
--    be gone.
-- ============================================================

BEGIN;

-- ============================================================
-- Step 1: Dry-run — identify all stuck talk+language pairs
--
-- A pair is stuck when the most-recent workflow event for that
-- (talk, language) combination is a transient in-progress event
-- (TranslationStarted or ValidationStarted) with no subsequent
-- terminal event.
-- ============================================================

WITH stuck AS (
    SELECT DISTINCT ON (e."TenantId", e."TargetEntityId", e."TargetEntitySubKey")
        e."TargetEntityId"     AS talk_id,
        e."TargetEntitySubKey" AS language_code,
        e."TenantId"           AS tenant_id,
        e."EventType"          AS stuck_event,
        e."OccurredAt"         AS stuck_since
    FROM workflows."WorkflowEvents" e
    WHERE e."IsDeleted" = false
      AND e."EventType" IN ('TranslationStarted', 'ValidationStarted')
      AND NOT EXISTS (
          SELECT 1
          FROM workflows."WorkflowEvents" e2
          WHERE e2."IsDeleted"           = false
            AND e2."TargetEntityId"      = e."TargetEntityId"
            AND e2."TargetEntitySubKey"  = e."TargetEntitySubKey"
            AND e2."TenantId"            = e."TenantId"
            AND e2."OccurredAt"          > e."OccurredAt"
            AND e2."EventType" IN (
                'TranslationCompleted',
                'ValidationCompleted',
                'InternalReviewSubmitted',
                'ExternalReviewInitiated',
                'ExternalReviewSubmitted',
                'ExternalReviewConfirmed',
                'ExternalReviewRejected',
                'ExternalReviewCancelled',
                'ExternalReviewDeclined',
                'AcceptedAsFinal',
                'MarkedStale'
            )
      )
    ORDER BY e."TenantId", e."TargetEntityId", e."TargetEntitySubKey", e."OccurredAt" DESC
)
SELECT
    talk_id,
    language_code,
    tenant_id,
    stuck_event,
    stuck_since
FROM stuck
ORDER BY stuck_since DESC;


-- ============================================================
-- Step 2: INSERT — write ValidationCompleted for each stuck pair
--
-- Each INSERT targets exactly the set identified by the dry-run CTE
-- above.  The INSERT is idempotent in the sense that re-running after
-- the first COMMIT will find no stuck rows and insert nothing.
--
-- WorkflowType = 1 (Translation)
-- TriggeredByType = 2 (System)
-- ============================================================

INSERT INTO workflows."WorkflowEvents" (
    "Id",
    "TenantId",
    "WorkflowType",
    "TargetEntityId",
    "TargetEntitySubKey",
    "EventType",
    "TriggeredByType",
    "TriggeredByUserId",
    "PayloadJson",
    "OccurredAt",
    "CreatedAt",
    "CreatedBy",
    "UpdatedAt",
    "UpdatedBy",
    "IsDeleted"
)
SELECT
    gen_random_uuid(),
    stuck."TenantId",
    1,                                -- WorkflowType.Translation
    stuck."TargetEntityId",
    stuck."TargetEntitySubKey",
    'ValidationCompleted',
    2,                                -- TriggeredByType.System
    NULL,
    NULL,
    NOW(),
    NOW(),
    'backfill-stuck-validation-workflow',
    NULL,
    NULL,
    false
FROM (
    -- Inline the same CTE so INSERT and SELECT target identical rows
    SELECT DISTINCT ON (e."TenantId", e."TargetEntityId", e."TargetEntitySubKey")
        e."TenantId",
        e."TargetEntityId",
        e."TargetEntitySubKey"
    FROM workflows."WorkflowEvents" e
    WHERE e."IsDeleted" = false
      AND e."EventType" IN ('TranslationStarted', 'ValidationStarted')
      AND NOT EXISTS (
          SELECT 1
          FROM workflows."WorkflowEvents" e2
          WHERE e2."IsDeleted"           = false
            AND e2."TargetEntityId"      = e."TargetEntityId"
            AND e2."TargetEntitySubKey"  = e."TargetEntitySubKey"
            AND e2."TenantId"            = e."TenantId"
            AND e2."OccurredAt"          > e."OccurredAt"
            AND e2."EventType" IN (
                'TranslationCompleted',
                'ValidationCompleted',
                'InternalReviewSubmitted',
                'ExternalReviewInitiated',
                'ExternalReviewSubmitted',
                'ExternalReviewConfirmed',
                'ExternalReviewRejected',
                'ExternalReviewCancelled',
                'ExternalReviewDeclined',
                'AcceptedAsFinal',
                'MarkedStale'
            )
      )
    ORDER BY e."TenantId", e."TargetEntityId", e."TargetEntitySubKey", e."OccurredAt" DESC
) AS stuck;


-- ============================================================
-- Step 3: Verification — re-run the dry-run after the INSERT
--
-- Expected: 0 rows.
-- If any rows appear, the INSERT did not cover all stuck pairs —
-- do NOT commit; investigate and ROLLBACK.
-- ============================================================

WITH stuck AS (
    SELECT DISTINCT ON (e."TenantId", e."TargetEntityId", e."TargetEntitySubKey")
        e."TargetEntityId"     AS talk_id,
        e."TargetEntitySubKey" AS language_code,
        e."TenantId"           AS tenant_id,
        e."EventType"          AS stuck_event,
        e."OccurredAt"         AS stuck_since
    FROM workflows."WorkflowEvents" e
    WHERE e."IsDeleted" = false
      AND e."EventType" IN ('TranslationStarted', 'ValidationStarted')
      AND NOT EXISTS (
          SELECT 1
          FROM workflows."WorkflowEvents" e2
          WHERE e2."IsDeleted"           = false
            AND e2."TargetEntityId"      = e."TargetEntityId"
            AND e2."TargetEntitySubKey"  = e."TargetEntitySubKey"
            AND e2."TenantId"            = e."TenantId"
            AND e2."OccurredAt"          > e."OccurredAt"
            AND e2."EventType" IN (
                'TranslationCompleted',
                'ValidationCompleted',
                'InternalReviewSubmitted',
                'ExternalReviewInitiated',
                'ExternalReviewSubmitted',
                'ExternalReviewConfirmed',
                'ExternalReviewRejected',
                'ExternalReviewCancelled',
                'ExternalReviewDeclined',
                'AcceptedAsFinal',
                'MarkedStale'
            )
      )
    ORDER BY e."TenantId", e."TargetEntityId", e."TargetEntitySubKey", e."OccurredAt" DESC
)
SELECT
    talk_id,
    language_code,
    tenant_id,
    stuck_event,
    stuck_since
FROM stuck
ORDER BY stuck_since DESC;

-- ============================================================
-- If Step 1 dry-run identified the expected stuck rows AND
-- Step 3 returns 0 rows:
--   execute COMMIT below.
-- If anything looks wrong:
--   execute ROLLBACK instead.
-- ============================================================

COMMIT;
-- ROLLBACK;
