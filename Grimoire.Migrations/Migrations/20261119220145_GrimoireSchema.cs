// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Migrations
{
    /// <summary>
    ///     The public schema's final shape before the first deploy (spec 2.3): pardons as one row plus reason history
    ///     (Q8), the unread GuildId copies dropped (Q11), NicknameHistory renamed (TD-9), bigint ids (P-9) with the
    ///     string discriminators in the same rewrite (Q9), and the leaderboard view (Q12, Q12b, defect 23).
    /// </summary>
    /// <remarks>
    ///     Hand-written: EF emits one ALTER TABLE per column, and Postgres rewrites a table once per statement. It runs
    ///     after DropOrphanedLegacyForeignKeys because Postgres refuses a foreign key from bigint to numeric. The
    ///     legacy tables are left alone: only the snapshot forgets them (D-20). P-7 drops them in a later release.
    /// </remarks>
    public partial class GrimoireSchema : Migration
    {
        /// <summary>
        ///     The id columns converted to bigint, per table, largest table first. A unit test checks this against the
        ///     model, so a new id column on one of these tables can't be missed.
        /// </summary>
        public static readonly IReadOnlyList<(string Table, string[] Columns)> BigintColumns =
        [
            ("XpHistory", ["GuildId", "UserId", "AwarderId"]),
            ("Messages", ["Id", "GuildId", "ChannelId", "UserId", "ReferencedMessageId"]),
            ("MessageHistory", ["MessageId", "ModeratorId"]),
            ("Avatars", ["UserId", "GuildId"]),
            ("Attachments", ["Id", "MessageId"]),
            ("NicknameHistory", ["UserId", "GuildId"]),
            ("UsernameHistory", ["UserId"]),
            ("Sins", ["UserId", "GuildId", "ModeratorId"]),
            ("Pardons", ["ModeratorId"]),
            ("Reactions", ["MessageId", "UserId", "GuildId"]),
            ("ProxiedMessages", ["ProxyMessageId", "OriginalMessageId"]),
            ("PublishedMessages", ["MessageId"]),
            ("OldLogMessages", ["Id", "ChannelId", "GuildId"]),
            ("CustomCommands", ["GuildId", "ModeratorId"]),
            ("CustomCommandsRole", ["GuildId", "RoleId"])
        ];

        /// <summary>The converted tables, quoted as <c>regclass</c> prints them, for the foreign key query.</summary>
        private static string TableList => string.Join(", ", BigintColumns.Select(entry => $"'\"{entry.Table}\"'"));

        /// <summary>Clauses a table's rewrite carries besides its ids (Q9, Q11).</summary>
        private static readonly Dictionary<string, string[]> _extraClauses = new()
        {
            ["XpHistory"] =
            [
                """
                ALTER COLUMN "Type" TYPE character varying(21) USING CASE "Type"
                    WHEN 0 THEN 'Earned' WHEN 1 THEN 'Awarded' WHEN 2 THEN 'Reclaimed' WHEN 3 THEN 'Migrated' END
                """
            ],
            ["MessageHistory"] =
            [
                """DROP COLUMN "GuildId" """,
                """ALTER COLUMN "Action" DROP DEFAULT""",
                """
                ALTER COLUMN "Action" TYPE character varying(34) USING CASE
                    WHEN "Action" = 0 THEN 'Created'
                    WHEN "Action" = 1 THEN 'Edited'
                    WHEN "Action" = 2 AND "ModeratorId" IS NOT NULL THEN 'DeletedByModerator'
                    WHEN "Action" = 2 THEN 'Deleted' END
                """
            ],
            ["Pardons"] =
            [
                """DROP COLUMN "Reason" """,
                """DROP COLUMN "GuildId" """,
                """ALTER COLUMN "SetAt" DROP DEFAULT"""
            ]
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Guards. The conversions below have no ELSE: a value they don't know stops the deploy here, with a
            // message, instead of quietly becoming Earned or Deleted. ModelRedesign has deleted Type 4 already.
            migrationBuilder.Sql("""
                DO $$
                DECLARE unknown bigint;
                BEGIN
                    SELECT count(*) INTO unknown FROM "XpHistory" WHERE "Type" NOT IN (0, 1, 2, 3);
                    IF unknown > 0 THEN
                        RAISE EXCEPTION 'XpHistory has % row(s) with a Type outside 0-3. Resolve them, then rerun this migration.', unknown;
                    END IF;
                    SELECT count(*) INTO unknown FROM "MessageHistory" WHERE "Action" NOT IN (0, 1, 2);
                    IF unknown > 0 THEN
                        RAISE EXCEPTION 'MessageHistory has % row(s) with an Action outside 0-2. Resolve them, then rerun this migration.', unknown;
                    END IF;
                END $$;
                """);

            // 2. Keep the definitions of the foreign keys on or into the converted tables, as the database has them,
            // then drop them. Postgres refuses a key between bigint and numeric, even for a moment.
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE grimoire_schema_fks ON COMMIT DROP AS
                SELECT conrelid::regclass AS tbl, conname, pg_get_constraintdef(oid) AS def
                FROM pg_constraint
                WHERE contype = 'f' AND connamespace = 'public'::regnamespace
                  AND (conrelid::regclass::text IN ({TableList}) OR confrelid::regclass::text IN ({TableList}));

                DO $$
                DECLARE fk record;
                BEGIN
                    FOR fk IN SELECT * FROM grimoire_schema_fks LOOP
                        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', fk.tbl, fk.conname);
                    END LOOP;
                END $$;
                """);

            // 3. Pardons (Q8): main's key (SinId) stays; the reason moves to its own history. An empty legacy reason
            // gets no row (spec 2.3, OQ-1), as ModelRedesign does for sins.
            migrationBuilder.Sql("""
                CREATE TABLE "PardonReasonHistory" (
                    "SinId"       bigint NOT NULL,
                    "SetAt"       timestamp with time zone NOT NULL,
                    "Reason"      character varying(1000) NOT NULL,
                    "ModeratorId" bigint NOT NULL,
                    CONSTRAINT "PK_PardonReasonHistory" PRIMARY KEY ("SinId", "SetAt"),
                    CONSTRAINT "FK_PardonReasonHistory_Pardons_SinId" FOREIGN KEY ("SinId")
                        REFERENCES "Pardons" ("SinId") ON DELETE CASCADE
                );

                INSERT INTO "PardonReasonHistory" ("SinId", "SetAt", "Reason", "ModeratorId")
                SELECT "SinId", "PardonDate", "Reason", "ModeratorId"::bigint
                FROM "Pardons"
                WHERE trim("Reason") <> '';

                ALTER TABLE "Pardons" RENAME COLUMN "PardonDate" TO "SetAt";
                """);

            // 4. One ALTER TABLE per table: its ids, and on XpHistory and MessageHistory the discriminator, so each
            // table is rewritten once (Q9). MessageHistory drops GuildId in the same statement, so it isn't copied.
            foreach (var (table, columns) in BigintColumns)
                migrationBuilder.Sql(AlterTable(table, columns));
            migrationBuilder.Sql("""ALTER TABLE "MessageHistory" RENAME COLUMN "Action" TO "Discriminator";""");

            // 5. TD-9: metadata only, after the table's rewrite above.
            migrationBuilder.Sql("""
                ALTER TABLE "NicknameHistory" RENAME TO "ServerDisplayNameHistory";
                ALTER TABLE "ServerDisplayNameHistory" RENAME CONSTRAINT "PK_NicknameHistory" TO "PK_ServerDisplayNameHistory";
                ALTER TABLE "ServerDisplayNameHistory" RENAME COLUMN "Nickname" TO "DisplayName";
                """);

            // 6. Built once, on the rewritten table.
            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_UserId_GuildId_Xp",
                table: "XpHistory",
                columns: ["UserId", "GuildId", "Xp"]);

            // 7. The foreign keys back, as they were. One that pointed at a legacy table would fail here, loudly.
            migrationBuilder.Sql("""
                DO $$
                DECLARE fk record;
                BEGIN
                    FOR fk IN SELECT * FROM grimoire_schema_fks LOOP
                        EXECUTE format('ALTER TABLE %s ADD CONSTRAINT %I %s', fk.tbl, fk.conname, fk.def);
                    END LOOP;
                END $$;
                """);

            // 8. The view, once, in its final shape: ties broken by UserId (defect 23), members at 0 XP or less left
            // out (Q12b), and the latest names (TD-9). The rank index makes the tie-break a guarantee (OQ-5).
            migrationBuilder.Sql(CreateLeaderboardView);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the shape after RenameAttachmentFileNameToAttachmentUrl. Production's rollback is the backup
            // (2.4), not this. Pardons' GuildId comes back from the sin, and a reason from its latest wording.
            migrationBuilder.Sql("""DROP MATERIALIZED VIEW IF EXISTS leaderboard_view;""");
            migrationBuilder.DropIndex(name: "IX_XpHistory_UserId_GuildId_Xp", table: "XpHistory");
            migrationBuilder.Sql("""
                ALTER TABLE "ServerDisplayNameHistory" RENAME COLUMN "DisplayName" TO "Nickname";
                ALTER TABLE "ServerDisplayNameHistory" RENAME CONSTRAINT "PK_ServerDisplayNameHistory" TO "PK_NicknameHistory";
                ALTER TABLE "ServerDisplayNameHistory" RENAME TO "NicknameHistory";
                ALTER TABLE "MessageHistory" RENAME COLUMN "Discriminator" TO "Action";
                """);

            migrationBuilder.Sql($"""
                CREATE TEMP TABLE grimoire_schema_fks ON COMMIT DROP AS
                SELECT conrelid::regclass AS tbl, conname, pg_get_constraintdef(oid) AS def
                FROM pg_constraint
                WHERE contype = 'f' AND connamespace = 'public'::regnamespace
                  AND (conrelid::regclass::text IN ({TableList}) OR confrelid::regclass::text IN ({TableList}));

                DO $$
                DECLARE fk record;
                BEGIN
                    FOR fk IN SELECT * FROM grimoire_schema_fks LOOP
                        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', fk.tbl, fk.conname);
                    END LOOP;
                END $$;
                """);

            foreach (var (table, columns) in BigintColumns)
            {
                var clauses = columns.Select(column =>
                    $"""ALTER COLUMN "{column}" TYPE numeric(20,0) USING "{column}"::numeric(20,0)""").ToList();
                if (table == "XpHistory")
                    clauses.Add("""
                        ALTER COLUMN "Type" TYPE integer USING CASE "Type"
                            WHEN 'Earned' THEN 0 WHEN 'Awarded' THEN 1 WHEN 'Reclaimed' THEN 2 WHEN 'Migrated' THEN 3 END
                        """);
                if (table == "MessageHistory")
                {
                    clauses.Add("""ADD COLUMN "GuildId" numeric(20,0) NOT NULL DEFAULT 0""");
                    clauses.Add("""
                        ALTER COLUMN "Action" TYPE integer USING CASE "Action"
                            WHEN 'Created' THEN 0 WHEN 'Edited' THEN 1 ELSE 2 END
                        """);
                }

                if (table == "Pardons")
                {
                    clauses.Add("""ADD COLUMN "GuildId" numeric(20,0) NOT NULL DEFAULT 0""");
                    clauses.Add("""ADD COLUMN "Reason" character varying(1000) NOT NULL DEFAULT ''""");
                }

                migrationBuilder.Sql($"""ALTER TABLE "{table}" {string.Join(", ", clauses)};""");
            }

            migrationBuilder.Sql("""
                UPDATE "MessageHistory" h SET "GuildId" = m."GuildId" FROM "Messages" m WHERE m."Id" = h."MessageId";
                UPDATE "Pardons" p SET "GuildId" = s."GuildId" FROM "Sins" s WHERE s."Id" = p."SinId";
                UPDATE "Pardons" p SET "Reason" = r."Reason"
                FROM (SELECT DISTINCT ON ("SinId") "SinId", "Reason" FROM "PardonReasonHistory"
                      ORDER BY "SinId", "SetAt" DESC) r
                WHERE r."SinId" = p."SinId";
                ALTER TABLE "Pardons" RENAME COLUMN "SetAt" TO "PardonDate";
                ALTER TABLE "Pardons" ALTER COLUMN "PardonDate" SET DEFAULT now();
                DROP TABLE "PardonReasonHistory";
                DELETE FROM grimoire_schema_fks WHERE conname = 'FK_PardonReasonHistory_Pardons_SinId';

                DO $$
                DECLARE fk record;
                BEGIN
                    FOR fk IN SELECT * FROM grimoire_schema_fks LOOP
                        EXECUTE format('ALTER TABLE %s ADD CONSTRAINT %I %s', fk.tbl, fk.conname, fk.def);
                    END LOOP;
                END $$;
                """);
        }

        /// <summary>The view and its two unique indexes (spec 2.3, section 6.3).</summary>
        public const string CreateLeaderboardView = """
            CREATE MATERIALIZED VIEW leaderboard_view AS
            SELECT x."GuildId", x."UserId", x."TotalXp",
                   ROW_NUMBER() OVER (PARTITION BY x."GuildId" ORDER BY x."TotalXp" DESC, x."UserId") AS "Rank",
                   d."DisplayName", u."Username"
            FROM (SELECT "GuildId", "UserId", sum("Xp") AS "TotalXp"
                  FROM "XpHistory" GROUP BY "GuildId", "UserId" HAVING sum("Xp") > 0) x
            LEFT JOIN LATERAL (SELECT h."DisplayName" FROM "ServerDisplayNameHistory" h
                               WHERE h."UserId" = x."UserId" AND h."GuildId" = x."GuildId"
                               ORDER BY h."Timestamp" DESC LIMIT 1) d ON true
            LEFT JOIN LATERAL (SELECT h."Username" FROM "UsernameHistory" h
                               WHERE h."UserId" = x."UserId"
                               ORDER BY h."Timestamp" DESC LIMIT 1) u ON true;

            CREATE UNIQUE INDEX idx_leaderboard_view_guild_user ON leaderboard_view ("GuildId", "UserId");
            CREATE UNIQUE INDEX idx_leaderboard_view_guild_rank ON leaderboard_view ("GuildId", "Rank");
            """;

        private static string AlterTable(string table, string[] columns)
        {
            var clauses = _extraClauses.GetValueOrDefault(table, [])
                .Concat(columns.Select(column => $"""ALTER COLUMN "{column}" TYPE bigint USING "{column}"::bigint"""));
            return $"""ALTER TABLE "{table}" {string.Join(", ", clauses)};""";
        }
    }
}
