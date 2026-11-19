-- This file is part of the Grimoire Project.
--
-- Copyright (c) Netharia 2021-Present.
--
-- All rights reserved.
-- Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

-- Migration spec 2.1: the copy run's C-2 and C-3. WRITES. Never run this against production.
--
-- NOT FOR PGADMIN. This is a psql script (it uses \timing and \gexec), run by Claude against the throwaway clone. The
-- script for pgAdmin is 2.1-production.sql. The first statement below stops the script on any database not named
-- grimoire_21.
--
-- Runbook (spec section 2, step 4), against a throwaway clone of a production backup:
--   1. Clone:   CREATE DATABASE grimoire_21; then pg_dump -Fc <restore> | pg_restore -d grimoire_21 --no-owner;
--               VACUUM ANALYZE.
--   2. C-1:     dotnet ef migrations script 20250705185227_SpamFilterOverrides --context GrimoireMigrationsDbContext
--                 --project Grimoire.Migrations --startup-project Grimoire -o c1.sql
--               (one chain since spec 2.0; 2.1 itself ran the two old chains, Grimoire first. The design-time host
--               needs ConnectionStrings__Grimoire set; strip the file's byte-order mark), then run it with
--               psql -v ON_ERROR_STOP=1 -e and \timing on, sampling pg_database_size every 0.5 s for the peak.
--   3. C-2, C-3: psql -X -v ON_ERROR_STOP=1 -e -d grimoire_21 -f 2.1-copy.sql, with the same size sampling.
--   4. Drop the clone.

DO $$
BEGIN
    IF current_database() <> 'grimoire_21' THEN
        RAISE EXCEPTION '2.1-copy.sql writes, and runs only on the throwaway clone grimoire_21, not on %. '
            'The read-only script is 2.1-production.sql.', current_database();
    END IF;
END $$;

\timing on

-- C-2 The bigint prototype (P-9). One transaction, as one EF migration would be, so the disk peak is realistic:
-- every rewritten table's old files are held until COMMIT. Covers both schemas, so it is an upper bound (2.2 creates
-- Settings with bigint from the start).
BEGIN;

DROP MATERIALIZED VIEW leaderboard_view;

-- A foreign key can't join bigint to numeric, so the keys over id columns are dropped and rebuilt around the rewrite.
CREATE TEMP TABLE fk_defs ON COMMIT DROP AS
SELECT conrelid::regclass AS tbl, conname, pg_get_constraintdef(oid) AS def
FROM pg_constraint
WHERE contype = 'f' AND connamespace IN ('public'::regnamespace, '"Settings"'::regnamespace);

SELECT format('ALTER TABLE %s DROP CONSTRAINT %I', tbl, conname) FROM fk_defs \gexec

-- One ALTER per table, covering all of its id columns, so each table is rewritten once. Largest first.
SELECT format('ALTER TABLE %I.%I %s', n.nspname, c.relname,
              string_agg(format('ALTER COLUMN %I TYPE bigint USING %I::bigint', a.attname, a.attname), ', '))
FROM pg_attribute a
JOIN pg_class c ON c.oid = a.attrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname IN ('public', 'Settings') AND c.relkind = 'r' AND NOT a.attisdropped
  AND format_type(a.atttypid, a.atttypmod) = 'numeric(20,0)'
GROUP BY n.nspname, c.relname, c.oid
ORDER BY pg_total_relation_size(c.oid) DESC \gexec

SELECT format('ALTER TABLE %s ADD CONSTRAINT %I %s', tbl, conname, def) FROM fk_defs \gexec

-- The view and its unique index, as LeaderboardView creates them.
CREATE MATERIALIZED VIEW leaderboard_view AS
SELECT "GuildId", "UserId", SUM("Xp") AS "TotalXp",
       ROW_NUMBER() OVER (PARTITION BY "GuildId" ORDER BY SUM("Xp") DESC) AS "Rank"
FROM "XpHistory"
GROUP BY "GuildId", "UserId";
CREATE UNIQUE INDEX idx_leaderboard_view_guild_user ON leaderboard_view ("GuildId", "UserId");

SELECT pg_size_pretty(pg_database_size(current_database())) AS size_before_commit;
COMMIT;

SELECT pg_size_pretty(pg_database_size(current_database())) AS size_after_commit;
VACUUM ANALYZE;

-- C-3 The view. First the refresh as the worker runs it today (current shape, bigint ids).
REFRESH MATERIALIZED VIEW CONCURRENTLY leaderboard_view;
REFRESH MATERIALIZED VIEW CONCURRENTLY leaderboard_view;   -- warm, the steady state

-- 2.3's shape, for its cost only: tie-break (defect 23), 0 XP excluded (Q12b), names from the database (TD-9).
EXPLAIN (ANALYZE, BUFFERS)
SELECT x."GuildId", x."UserId", x."TotalXp",
       ROW_NUMBER() OVER (PARTITION BY x."GuildId" ORDER BY x."TotalXp" DESC, x."UserId") AS "Rank",
       n."Nickname" AS "DisplayName", u."Username"
FROM (SELECT "GuildId", "UserId", sum("Xp") AS "TotalXp" FROM "XpHistory"
      GROUP BY 1, 2 HAVING sum("Xp") > 0) x
LEFT JOIN LATERAL (SELECT "Nickname" FROM "NicknameHistory" h
                   WHERE h."UserId" = x."UserId" AND h."GuildId" = x."GuildId"
                   ORDER BY h."Timestamp" DESC LIMIT 1) n ON true
LEFT JOIN LATERAL (SELECT "Username" FROM "UsernameHistory" h
                   WHERE h."UserId" = x."UserId" ORDER BY h."Timestamp" DESC LIMIT 1) u ON true;

-- The reference for the hardware ratio after the rewrite (P-4a's query on bigint ids).
EXPLAIN (ANALYZE, BUFFERS) SELECT "GuildId", "UserId", sum("Xp") FROM "XpHistory" GROUP BY 1, 2;
