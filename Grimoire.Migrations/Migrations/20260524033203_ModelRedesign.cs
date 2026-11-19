// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Migrations
{
    /// <inheritdoc />
    public partial class ModelRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Phase 1: SinReasonHistory ────────────────────────────────────────────
            // Create the table BEFORE migrating reasons so the INSERT can reference it.
            migrationBuilder.CreateTable(
                name: "SinReasonHistory",
                columns: table => new
                {
                    SinId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SinReasonHistory", x => new { x.SinId, x.SetAt });
                    table.ForeignKey(
                        name: "FK_SinReasonHistory_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migrate existing non-empty reasons before Sins.Reason is dropped.
            migrationBuilder.Sql("""
                INSERT INTO "SinReasonHistory" ("SinId", "SetAt", "Reason", "ModeratorId")
                SELECT "Id", "SinOn", "Reason", "ModeratorId"
                FROM "Sins"
                WHERE trim("Reason") <> '';
                """);

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Sins");

            // ── Phase 2 (removed by spec 2.3): pardons keep main's key, SinId (Q8). GrimoireSchema adds their
            // reason history.

            // ── Phase 3: Trackers / Mutes FK ─────────────────────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_Mutes_Sins_SinId",
                table: "Mutes");

            migrationBuilder.DropTable(
                name: "Trackers");

            // ── Phase 4: CustomCommands / CustomCommandsRole redesign ─────────────────
            // Add new columns with safe defaults BEFORE backfilling data.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CustomCommands",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CommandType",
                table: "CustomCommands",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ModeratorId",
                table: "CustomCommands",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "CustomCommandsRole",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CustomCommandsRole",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "RoleType",
                table: "CustomCommandsRole",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            // Backfill CustomCommands from the old IsEmbedded flag.
            migrationBuilder.Sql("""
                UPDATE "CustomCommands"
                SET "CreatedAt"   = NOW(),
                    "CommandType" = CASE WHEN "IsEmbedded" THEN 'Embed' ELSE 'Text' END;
                """);

            // Backfill CustomCommandsRole. A restricted command's roles were the only ones allowed to use it; an
            // unrestricted command's roles were the ones denied it (main's GetCustomCommand.IsUserAuthorized).
            migrationBuilder.Sql("""
                UPDATE "CustomCommandsRole" AS ccr
                SET "Name"      = ccr."CustomCommandName",
                    "CreatedAt" = cc."CreatedAt",
                    "RoleType"  = CASE WHEN cc."RestrictedUse" THEN 'Allow' ELSE 'Deny' END
                FROM "CustomCommands" AS cc
                WHERE cc."Name"    = ccr."CustomCommandName"
                  AND cc."GuildId" = ccr."GuildId";
                """);

            // Swap PKs and rebuild the FK (order: drop FK → drop PKs → drop stale columns →
            // add new PKs → add new FK).
            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommandsRole_CustomCommands_CustomCommandName_GuildId",
                table: "CustomCommandsRole");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomCommandsRole",
                table: "CustomCommandsRole");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomCommands",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "CustomCommandName",
                table: "CustomCommandsRole");

            migrationBuilder.DropColumn(
                name: "HasMention",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "HasMessage",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "IsEmbedded",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "RestrictedUse",
                table: "CustomCommands");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomCommands",
                table: "CustomCommands",
                columns: new[] { "Name", "GuildId", "CreatedAt" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomCommandsRole",
                table: "CustomCommandsRole",
                columns: new[] { "Name", "GuildId", "CreatedAt", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommands_GuildId_Name",
                table: "CustomCommands",
                columns: new[] { "GuildId", "Name" });

            migrationBuilder.CreateTable(
                name: "CustomCommandUsages",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomCommandUsages", x => new { x.Name, x.GuildId, x.UserId, x.UsedAt });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommandUsages_GuildId_Name_UsedAt",
                table: "CustomCommandUsages",
                columns: new[] { "GuildId", "Name", "UsedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommandsRole_CustomCommands_Name_GuildId_CreatedAt",
                table: "CustomCommandsRole",
                columns: new[] { "Name", "GuildId", "CreatedAt" },
                principalTable: "CustomCommands",
                principalColumns: new[] { "Name", "GuildId", "CreatedAt" },
                onDelete: ReferentialAction.Cascade);

            // ── Phase 5: MessageHistory ──────────────────────────────────────────────
            // Action stays an integer here: GrimoireSchema turns it into the Discriminator strings in the same
            // rewrite as the bigint ids (Q9, spec 2.3), so the table is written once and never UPDATEd as a whole.
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "TimeStamp",
                table: "MessageHistory",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.RenameColumn(
                name: "MessageContent",
                table: "MessageHistory",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "DeletedByModeratorId",
                table: "MessageHistory",
                newName: "ModeratorId");

            // ── Phase 6: ProxiedMessages NOT NULL enforcement ──────────────────────────
            // Delete rows with null values BEFORE enforcing the NOT NULL constraint.
            migrationBuilder.Sql("""
                DELETE FROM "ProxiedMessages"
                WHERE "SystemId" IS NULL OR "MemberId" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "SystemId",
                table: "ProxiedMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "ProxiedMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            // ── Phase 7: XpHistory restructuring ──────────────────────────────────────
            // Drop FKs that were never removed from the DB but are gone from the model.
            migrationBuilder.DropForeignKey(
                name: "FK_XpHistory_Guilds_GuildId",
                table: "XpHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_XpHistory_Members_AwarderId_GuildId",
                table: "XpHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_XpHistory_Members_UserId_GuildId",
                table: "XpHistory");

            // Drop indexes that were never removed from the DB but are gone from the model.
            migrationBuilder.DropIndex(
                name: "IX_XpHistory_AwarderId_GuildId",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_XpHistory_GuildId",
                table: "XpHistory");

            // Data cleanup: Type is still INTEGER at this point.
            // Old enum: 0=Earned, 1=Awarded, 2=Reclaimed, 3=Migrated, 4=Created
            // Fail fast if any Awarded row is missing its AwarderId.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "XpHistory"
                        WHERE "Type" = 1 AND "AwarderId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Data integrity error: Awarded XpHistory rows found with NULL AwarderId. '
                            'Resolve these rows manually before running this migration.';
                    END IF;
                END $$;
                """);

            // Null out stale AwarderId values on rows that have no moderator. Reclaimed rows keep theirs: main's
            // ReclaimUserXp stored who took the XP there, and ReclaimedXp.ModeratorId reads it back.
            migrationBuilder.Sql("""
                UPDATE "XpHistory"
                SET "AwarderId" = NULL
                WHERE "Type" NOT IN (1, 2) AND "AwarderId" IS NOT NULL;
                """);

            // Delete Created rows (zero-XP user-join records, no longer used).
            migrationBuilder.Sql("""DELETE FROM "XpHistory" WHERE "Type" = 4;""");

            // Type stays an integer here, and the index is built after the rewrite: GrimoireSchema converts Type
            // in the same ALTER TABLE as the bigint ids, so the table is rewritten once (Q9, spec 2.3).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Phase 7 reversal: XpHistory ───────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_AwarderId_GuildId",
                table: "XpHistory",
                columns: new[] { "AwarderId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_GuildId",
                table: "XpHistory",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_XpHistory_Guilds_GuildId",
                table: "XpHistory",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_XpHistory_Members_AwarderId_GuildId",
                table: "XpHistory",
                columns: new[] { "AwarderId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_XpHistory_Members_UserId_GuildId",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            // ── Phase 6 reversal: ProxiedMessages ─────────────────────────────────────
            migrationBuilder.AlterColumn<string>(
                name: "SystemId",
                table: "ProxiedMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "MemberId",
                table: "ProxiedMessages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // ── Phase 5 reversal: MessageHistory ──────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ModeratorId",
                table: "MessageHistory",
                newName: "DeletedByModeratorId");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "MessageHistory",
                newName: "MessageContent");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "TimeStamp",
                table: "MessageHistory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            // ── Phase 4 reversal: CustomCommands ──────────────────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommandsRole_CustomCommands_Name_GuildId_CreatedAt",
                table: "CustomCommandsRole");

            migrationBuilder.DropTable(
                name: "CustomCommandUsages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomCommandsRole",
                table: "CustomCommandsRole");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CustomCommands",
                table: "CustomCommands");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommands_GuildId_Name",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "CustomCommandsRole");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CustomCommandsRole");

            migrationBuilder.DropColumn(
                name: "RoleType",
                table: "CustomCommandsRole");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "CommandType",
                table: "CustomCommands");

            migrationBuilder.DropColumn(
                name: "ModeratorId",
                table: "CustomCommands");

            migrationBuilder.AddColumn<string>(
                name: "CustomCommandName",
                table: "CustomCommandsRole",
                type: "character varying(24)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasMention",
                table: "CustomCommands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMessage",
                table: "CustomCommands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmbedded",
                table: "CustomCommands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictedUse",
                table: "CustomCommands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomCommandsRole",
                table: "CustomCommandsRole",
                columns: new[] { "CustomCommandName", "GuildId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CustomCommands",
                table: "CustomCommands",
                columns: new[] { "Name", "GuildId" });

            migrationBuilder.CreateTable(
                name: "Trackers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trackers", x => new { x.UserId, x.GuildId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_EndTime",
                table: "Trackers",
                column: "EndTime");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommandsRole_CustomCommands_CustomCommandName_GuildId",
                table: "CustomCommandsRole",
                columns: new[] { "CustomCommandName", "GuildId" },
                principalTable: "CustomCommands",
                principalColumns: new[] { "Name", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mutes_Sins_SinId",
                table: "Mutes",
                column: "SinId",
                principalTable: "Sins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Phase 3 reversal: nothing (Trackers already recreated above) ──────────

            // ── Phase 1 reversal: SinReasonHistory ───────────────────────────────────
            migrationBuilder.DropTable(
                name: "SinReasonHistory");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Sins",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
