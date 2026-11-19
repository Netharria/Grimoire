-- This file is part of the Grimoire Project.
--
-- Copyright (c) Netharia 2021-Present.
--
-- All rights reserved.
-- Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

-- Migration spec 2.1: production measurements. READ-ONLY.
--
-- How to run: open this file in pgAdmin's Query Tool, connected to the database, and run the whole file (F5).
-- The results are in the Messages tab, one line per row, each tagged with its measurement (P-0 to P-7, see
-- docs/langext-migration/specs/2.1-production-measurements.md, section 6.1). Copy the whole Messages tab into
-- docs/langext-migration/measurements/2.1-production-baseline.out.
--
-- Safety: everything runs in one READ ONLY transaction that ends in ROLLBACK, so any write is refused. Each
-- measurement is limited to 5 minutes and waits at most 2 s for a lock. A measurement that times out or fails
-- prints so and the rest still run. Any 17-to-20-digit number (a Discord id) is printed as <id>.
--

BEGIN READ ONLY;
SET LOCAL statement_timeout = '5min';
SET LOCAL lock_timeout = '2s';

-- P-0 Migration history: the last three (expect 20250705185227_SpamFilterOverrides first)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-0] -- Migration history: the last three (expect 20250705185227_SpamFilterOverrides first)';
    FOR result_row IN
        SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3
    LOOP
        RAISE NOTICE '[P-0] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-0] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-0] failed: %', SQLERRM;
END $$;

-- P-0 No Settings schema yet (expect true)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-0] -- No Settings schema yet (expect true)';
    FOR result_row IN
        SELECT to_regnamespace('"Settings"') IS NULL AS settings_schema_absent
    LOOP
        RAISE NOTICE '[P-0] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-0] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-0] failed: %', SQLERRM;
END $$;

-- P-1 Server and database
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-1] -- Server and database';
    FOR result_row IN
        SELECT version() AS version, pg_size_pretty(pg_database_size(current_database())) AS database_size,
               pg_database_size(current_database()) AS database_bytes, clock_timestamp() AS measured_at
    LOOP
        RAISE NOTICE '[P-1] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-1] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-1] failed: %', SQLERRM;
END $$;

-- P-1 Settings that affect rewrites and sorts
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-1] -- Settings that affect rewrites and sorts';
    FOR result_row IN
        SELECT name, setting, unit FROM pg_settings
        WHERE name IN ('shared_buffers', 'work_mem', 'maintenance_work_mem', 'max_wal_size', 'statement_timeout',
                       'max_parallel_maintenance_workers', 'track_io_timing')
        ORDER BY name
    LOOP
        RAISE NOTICE '[P-1] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-1] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-1] failed: %', SQLERRM;
END $$;

-- P-2 Every table: rows, sizes, maintenance
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-2] -- Every table: rows, sizes, maintenance';
    FOR result_row IN
        SELECT c.relname,
               s.n_live_tup AS live_rows, s.n_dead_tup AS dead_rows,
               pg_size_pretty(pg_relation_size(c.oid)) AS heap,
               pg_size_pretty(pg_indexes_size(c.oid)) AS indexes,
               pg_size_pretty(coalesce(pg_total_relation_size(nullif(c.reltoastrelid, 0)), 0)) AS toast,
               pg_size_pretty(pg_total_relation_size(c.oid)) AS total,
               pg_total_relation_size(c.oid) AS total_bytes,
               greatest(s.last_vacuum, s.last_autovacuum)::date AS last_vacuum,
               greatest(s.last_analyze, s.last_autoanalyze)::date AS last_analyze
        FROM pg_class c JOIN pg_stat_user_tables s ON s.relid = c.oid
        WHERE c.relnamespace = 'public'::regnamespace
        ORDER BY pg_total_relation_size(c.oid) DESC
    LOOP
        RAISE NOTICE '[P-2] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-2] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-2] failed: %', SQLERRM;
END $$;

-- P-3 Indexes on the five largest tables
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-3] -- Indexes on the five largest tables';
    FOR result_row IN
        SELECT t.relname AS table_name, i.relname AS index_name, pg_size_pretty(pg_relation_size(i.oid)) AS size
        FROM pg_index x JOIN pg_class i ON i.oid = x.indexrelid JOIN pg_class t ON t.oid = x.indrelid
        WHERE t.oid IN (SELECT relid FROM pg_stat_user_tables WHERE schemaname = 'public'
                        ORDER BY pg_total_relation_size(relid) DESC LIMIT 5)
        ORDER BY pg_total_relation_size(t.oid) DESC, pg_relation_size(i.oid) DESC
    LOOP
        RAISE NOTICE '[P-3] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-3] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-3] failed: %', SQLERRM;
END $$;

-- P-4 XP rows per member
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-4] -- XP rows per member';
    FOR result_row IN
        SELECT count(*) AS members, max(n) AS max_rows,
               percentile_disc(0.5) WITHIN GROUP (ORDER BY n) AS p50_rows,
               percentile_disc(0.99) WITHIN GROUP (ORDER BY n) AS p99_rows
        FROM (SELECT count(*) AS n FROM "XpHistory" GROUP BY "GuildId", "UserId") m
    LOOP
        RAISE NOTICE '[P-4] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4] failed: %', SQLERRM;
END $$;

-- P-4 The five heaviest members' row counts
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-4] -- The five heaviest members'' row counts';
    FOR result_row IN
        SELECT n FROM (SELECT count(*) AS n FROM "XpHistory" GROUP BY "GuildId", "UserId") m ORDER BY n DESC LIMIT 5
    LOOP
        RAISE NOTICE '[P-4] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4] failed: %', SQLERRM;
END $$;

-- P-4 Guilds, and the largest guild's rows and members
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-4] -- Guilds, and the largest guild''s rows and members';
    FOR result_row IN
        SELECT count(*) AS guilds, max(n) AS largest_guild_rows, max(members) AS largest_guild_members
        FROM (SELECT count(*) AS n, count(DISTINCT "UserId") AS members FROM "XpHistory" GROUP BY "GuildId") g
    LOOP
        RAISE NOTICE '[P-4] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4] failed: %', SQLERRM;
END $$;

-- P-4 Members whose total is 0 or less (left off the board by Q12b)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-4] -- Members whose total is 0 or less (left off the board by Q12b)';
    FOR result_row IN
        SELECT count(*) AS members_with_zero_xp
        FROM (SELECT 1 FROM "XpHistory" GROUP BY "GuildId", "UserId" HAVING sum("Xp") <= 0) z
    LOOP
        RAISE NOTICE '[P-4] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4] failed: %', SQLERRM;
END $$;

-- P-4a Whole-table XP aggregate (also the hardware ratio k)
DO $$
DECLARE line text;
BEGIN
    RAISE NOTICE '[P-4a] -- Whole-table XP aggregate (also the hardware ratio k)';
    FOR line IN EXECUTE
        'EXPLAIN (ANALYZE, BUFFERS) ' || $q$
        SELECT "GuildId", "UserId", sum("Xp") FROM "XpHistory" GROUP BY 1, 2
        $q$
    LOOP
        RAISE NOTICE '[P-4a] %', regexp_replace(line, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4a] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4a] failed: %', SQLERRM;
END $$;

-- P-4b The leaderboard view's body, as committed
DO $$
DECLARE line text;
BEGIN
    RAISE NOTICE '[P-4b] -- The leaderboard view''s body, as committed';
    FOR line IN EXECUTE
        'EXPLAIN (ANALYZE, BUFFERS) ' || $q$
        SELECT "GuildId", "UserId", sum("Xp") AS "TotalXp",
               ROW_NUMBER() OVER (PARTITION BY "GuildId" ORDER BY sum("Xp") DESC) AS "Rank"
        FROM "XpHistory" GROUP BY "GuildId", "UserId"
        $q$
    LOOP
        RAISE NOTICE '[P-4b] %', regexp_replace(line, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4b] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4b] failed: %', SQLERRM;
END $$;

-- P-4c One member's total, for the heaviest member (upper bound: main lacks the branch's index)
DO $$
DECLARE line text;
    g numeric;
    u numeric;
BEGIN
    RAISE NOTICE '[P-4c] -- One member''s total, for the heaviest member (upper bound: main lacks the branch''s index)';
    SELECT "GuildId", "UserId" INTO g, u FROM "XpHistory" GROUP BY 1, 2 ORDER BY count(*) DESC LIMIT 1;
    FOR line IN EXECUTE
        format('EXPLAIN (ANALYZE, BUFFERS) SELECT sum("Xp") FROM "XpHistory" WHERE "UserId" = %s AND "GuildId" = %s', u, g)
    LOOP
        RAISE NOTICE '[P-4c] %', regexp_replace(line, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-4c] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-4c] failed: %', SQLERRM;
END $$;

-- P-5 XpHistory by Type (Q9 rewrite; Type 4 is deleted)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-5] -- XpHistory by Type (Q9 rewrite; Type 4 is deleted)';
    FOR result_row IN
        SELECT "Type", count(*) FROM "XpHistory" GROUP BY 1 ORDER BY 1
    LOOP
        RAISE NOTICE '[P-5] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-5] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-5] failed: %', SQLERRM;
END $$;

-- P-5 MessageHistory by Action (Q9 UPDATE)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-5] -- MessageHistory by Action (Q9 UPDATE)';
    FOR result_row IN
        SELECT "Action", ("DeletedByModeratorId" IS NOT NULL) AS by_moderator, count(*)
        FROM "MessageHistory" GROUP BY 1, 2 ORDER BY 1, 2
    LOOP
        RAISE NOTICE '[P-5] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-5] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-5] failed: %', SQLERRM;
END $$;

-- P-5 ProxiedMessages rows ModelRedesign deletes
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-5] -- ProxiedMessages rows ModelRedesign deletes';
    FOR result_row IN
        SELECT count(*) AS proxied_rows_deleted FROM "ProxiedMessages" WHERE "SystemId" IS NULL OR "MemberId" IS NULL
    LOOP
        RAISE NOTICE '[P-5] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-5] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-5] failed: %', SQLERRM;
END $$;

-- P-5 Pardons (Q8)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-5] -- Pardons (Q8)';
    FOR result_row IN
        SELECT count(*) AS pardons, count(*) FILTER (WHERE "Reason" IS NULL OR "Reason" = '') AS without_reason
        FROM "Pardons"
    LOOP
        RAISE NOTICE '[P-5] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-5] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-5] failed: %', SQLERRM;
END $$;

-- P-5 Mutes (TD-1)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-5] -- Mutes (TD-1)';
    FOR result_row IN
        SELECT count(*) AS mutes, count(*) FILTER (WHERE "EndTime" <= now()) AS overdue FROM "Mutes"
    LOOP
        RAISE NOTICE '[P-5] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-5] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-5] failed: %', SQLERRM;
END $$;

-- P-5 Locks (TD-1)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-5] -- Locks (TD-1)';
    FOR result_row IN
        SELECT count(*) AS locks, count(*) FILTER (WHERE "EndTime" <= now()) AS overdue FROM "Locks"
    LOOP
        RAISE NOTICE '[P-5] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-5] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-5] failed: %', SQLERRM;
END $$;

-- P-6 P-1: roles on unrestricted custom commands
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-6] -- P-1: roles on unrestricted custom commands';
    FOR result_row IN
        SELECT count(*) AS p1_roles_on_unrestricted FROM "CustomCommandsRole" r JOIN "CustomCommands" c
            ON c."Name" = r."CustomCommandName" AND c."GuildId" = r."GuildId" WHERE NOT c."RestrictedUse"
    LOOP
        RAISE NOTICE '[P-6] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-6] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-6] failed: %', SQLERRM;
END $$;

-- P-6 P-1: restricted custom commands with no roles (open decision if not 0)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-6] -- P-1: restricted custom commands with no roles (open decision if not 0)';
    FOR result_row IN
        SELECT count(*) AS p1_restricted_without_roles FROM "CustomCommands" c WHERE c."RestrictedUse" AND NOT EXISTS
            (SELECT 1 FROM "CustomCommandsRole" r WHERE r."CustomCommandName" = c."Name" AND r."GuildId" = c."GuildId")
    LOOP
        RAISE NOTICE '[P-6] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-6] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-6] failed: %', SQLERRM;
END $$;

-- P-6 P-2: reclaimed XP with a moderator
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-6] -- P-2: reclaimed XP with a moderator';
    FOR result_row IN
        SELECT count(*) AS p2_reclaimed_with_moderator FROM "XpHistory" WHERE "Type" = 2 AND "AwarderId" IS NOT NULL
    LOOP
        RAISE NOTICE '[P-6] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-6] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-6] failed: %', SQLERRM;
END $$;

-- P-6 GUARD, must be 0: awarded XP with no awarder (ModelRedesign aborts)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-6] -- GUARD, must be 0: awarded XP with no awarder (ModelRedesign aborts)';
    FOR result_row IN
        SELECT count(*) AS guard_awarded_without_awarder FROM "XpHistory" WHERE "Type" = 1 AND "AwarderId" IS NULL
    LOOP
        RAISE NOTICE '[P-6] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-6] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-6] failed: %', SQLERRM;
END $$;

-- P-6 GUARD, must be 0: pardons with no moderator (RequirePardonModerator aborts)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-6] -- GUARD, must be 0: pardons with no moderator (RequirePardonModerator aborts)';
    FOR result_row IN
        SELECT count(*) AS guard_pardons_without_moderator FROM "Pardons" WHERE "ModeratorId" IS NULL
    LOOP
        RAISE NOTICE '[P-6] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-6] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-6] failed: %', SQLERRM;
END $$;

-- P-7 S-1: level settings out of range
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-7] -- S-1: level settings out of range';
    FOR result_row IN
        SELECT count(*) AS s1_level_settings_out_of_range FROM "GuildLevelSettings"
        WHERE "Base" NOT BETWEEN 1 AND 500 OR "Modifier" NOT BETWEEN 1 AND 200 OR "Amount" NOT BETWEEN 1 AND 100
           OR "TextTime" < INTERVAL '1 minute' OR "TextTime" > INTERVAL '60 minutes'
    LOOP
        RAISE NOTICE '[P-7] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-7] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-7] failed: %', SQLERRM;
END $$;

-- P-7 S-3: locks with no captured permissions (upper bound)
DO $$
DECLARE result_row record;
BEGIN
    RAISE NOTICE '[P-7] -- S-3: locks with no captured permissions (upper bound)';
    FOR result_row IN
        SELECT count(*) AS s3_locks_without_permissions FROM "Locks"
        WHERE "PreviouslyAllowed" = 0 AND "PreviouslyDenied" = 0
    LOOP
        RAISE NOTICE '[P-7] %', regexp_replace(row_to_json(result_row)::text, '\d{17,20}', '<id>', 'g');
    END LOOP;
EXCEPTION
    WHEN query_canceled THEN RAISE NOTICE '[P-7] over 5 minutes';
    WHEN OTHERS THEN RAISE NOTICE '[P-7] failed: %', SQLERRM;
END $$;

DO $$ BEGIN RAISE NOTICE '[end] finished at %', clock_timestamp(); END $$;

ROLLBACK;
