-- =============================================================================
-- verify_settings_migration.sql
--
-- Purpose : Confirm that the Settings.Initial migration correctly copied every
--           row from the public-schema source tables into the Settings schema.
--           Run this AFTER the migration has been applied.
--
-- Uses the same sentinel guild ID (111000111000111001) as seed_migration_test_data.sql.
-- Each section prints the expected value alongside the actual migrated value
-- so mismatches are immediately visible.
-- =============================================================================

-- ======================================================================
-- Settings.GuildSettings — module flags and channel IDs
-- ======================================================================
-- Expected rows (14 total — optional log-channel columns were left NULL in
-- the seed so the WHERE IS NOT NULL guards skip those paths):
--   AntiSpamModuleEnabled       → 'True'
--   CustomCommandsModuleEnabled → 'True'
--   LevelScalingBase            → '20'   (seed uses 20 ≠ default 15)
--   LevelScalingModifier        → '60'   (seed uses 60 ≠ default 50)
--   LevelingModuleEnabled       → 'True'
--   MessageLogModuleEnabled     → 'True'
--   ModerationLogChannel        → '200900200900200901'  (from Guilds)
--   ModerationModuleEnabled     → 'True'
--   MuteRole                    → '444000444000444001'
--   SinAutoPardonDuration       → '365.00:00:00'  (seed uses 365 d ≠ default 10950 d)
--   UserCommandChannel          → '200800200800200801'  (from Guilds)
--   UserLogModuleEnabled        → 'True'
--   XpGainAmount                → '8'    (seed uses 8 ≠ default 5)
--   XpTimeoutPeriod             → '00:05:00'  (seed uses 5 min ≠ default 3 min)

SELECT
    "Type",
    "GuildId",
    "State",
    "Value"
FROM "Settings"."GuildSettings"
WHERE "GuildId" = 111000111000111001
ORDER BY "Type";

-- ======================================================================
-- Spot-check: interval → TimeSpan string conversions
-- ======================================================================

SELECT
    "Type",
    "Value"                            AS "Migrated value",
    CASE "Type"
        WHEN 'XpTimeoutPeriod'       THEN '00:05:00'
        WHEN 'SinAutoPardonDuration' THEN '365.00:00:00'
    END                                AS "Expected value",
    "Value" = CASE "Type"
        WHEN 'XpTimeoutPeriod'       THEN '00:05:00'
        WHEN 'SinAutoPardonDuration' THEN '365.00:00:00'
    END                                AS "Match?"
FROM "Settings"."GuildSettings"
WHERE "GuildId" = 111000111000111001
  AND "Type" IN ('XpTimeoutPeriod', 'SinAutoPardonDuration');

-- ======================================================================
-- Settings.XpTrackedItems — ignored channels / members / roles
-- ======================================================================
-- Expected 3 rows:
--   IgnoredChannel  222000222000222001
--   IgnoredMember   333000333000333001
--   IgnoredRole     444000444000444001

SELECT "Type", "Id", "GuildId"
FROM "Settings"."XpTrackedItems"
WHERE "GuildId" = 111000111000111001
ORDER BY "Type";

-- ======================================================================
-- Settings.ChannelLocks
-- ======================================================================
-- Expected 1 row:
--   ChannelId        = 222000222000222002
--   EventType        = 'Locked'
--   Reason           = 'Test lock for migration verification'
--   PreviouslyDenied = 1024

SELECT
    "ChannelId",
    "GuildId",
    "EventType",
    "Reason",
    "PreviouslyAllowed",
    "PreviouslyDenied",
    "EndTime" IS NOT NULL AS "HasEndTime"
FROM "Settings"."ChannelLocks"
WHERE "GuildId" = 111000111000111001;

-- ======================================================================
-- Settings.Mutes
-- ======================================================================
-- Expected 1 row:
--   UserId    = 333000333000333001
--   EventType = 'Added'
--   HasSinId  = true

SELECT
    "UserId",
    "GuildId",
    "EventType",
    "SinId" IS NOT NULL   AS "HasSinId",
    "EndTime" IS NOT NULL AS "HasEndTime"
FROM "Settings"."Mutes"
WHERE "GuildId" = 111000111000111001;

-- ======================================================================
-- Settings.Rewards
-- ======================================================================
-- Expected 1 row:
--   RoleId        = 444000444000444002
--   RewardType    = 'Added'
--   RewardLevel   = 10
--   RewardMessage = 'Congratulations on reaching level 10!'

SELECT
    "RoleId",
    "GuildId",
    "RewardType",
    "RewardLevel",
    "RewardMessage"
FROM "Settings"."Rewards"
WHERE "GuildId" = 111000111000111001;

-- ======================================================================
-- Settings.MessageLogChannelOverrides
-- ======================================================================
-- Expected 1 row: ChannelOption = 'AlwaysLog' (source int 0)

SELECT
    "ChannelId",
    "GuildId",
    "ChannelOption"                 AS "Migrated value",
    'AlwaysLog'                     AS "Expected value",
    "ChannelOption" = 'AlwaysLog'   AS "Match?"
FROM "Settings"."MessageLogChannelOverrides"
WHERE "GuildId" = 111000111000111001;

-- ======================================================================
-- Settings.SpamFilterOverrides
-- ======================================================================
-- Expected 1 row: ChannelOption = 'NeverFilter' (source int 1)

SELECT
    "ChannelId",
    "GuildId",
    "ChannelOption"                  AS "Migrated value",
    'NeverFilter'                    AS "Expected value",
    "ChannelOption" = 'NeverFilter'  AS "Match?"
FROM "Settings"."SpamFilterOverrides"
WHERE "GuildId" = 111000111000111001;

-- ======================================================================
-- Summary — row counts for the test guild across all Settings tables
-- ======================================================================

SELECT 'GuildSettings'             AS "Table",
       COUNT(*)                    AS "Rows",
       14                          AS "Expected",
       COUNT(*) = 14               AS "OK?"
FROM "Settings"."GuildSettings"
WHERE "GuildId" = 111000111000111001

UNION ALL

SELECT 'XpTrackedItems', COUNT(*), 3, COUNT(*) = 3
FROM "Settings"."XpTrackedItems"
WHERE "GuildId" = 111000111000111001

UNION ALL

SELECT 'ChannelLocks', COUNT(*), 1, COUNT(*) = 1
FROM "Settings"."ChannelLocks"
WHERE "GuildId" = 111000111000111001

UNION ALL

SELECT 'Mutes', COUNT(*), 1, COUNT(*) = 1
FROM "Settings"."Mutes"
WHERE "GuildId" = 111000111000111001

UNION ALL

SELECT 'Rewards', COUNT(*), 1, COUNT(*) = 1
FROM "Settings"."Rewards"
WHERE "GuildId" = 111000111000111001

UNION ALL

SELECT 'MessageLogChannelOverrides', COUNT(*), 1, COUNT(*) = 1
FROM "Settings"."MessageLogChannelOverrides"
WHERE "GuildId" = 111000111000111001

UNION ALL

SELECT 'SpamFilterOverrides', COUNT(*), 1, COUNT(*) = 1
FROM "Settings"."SpamFilterOverrides"
WHERE "GuildId" = 111000111000111001

ORDER BY "Table";
