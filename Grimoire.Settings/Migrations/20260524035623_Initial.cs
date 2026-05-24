// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Settings.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Settings");

            migrationBuilder.CreateTable(
                name: "ChannelLocks",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EventType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    PreviouslyAllowed = table.Column<long>(type: "bigint", nullable: true),
                    PreviouslyDenied = table.Column<long>(type: "bigint", nullable: true),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelLocks", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                schema: "Settings",
                columns: table => new
                {
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    State = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => new { x.Type, x.GuildId, x.SetAt });
                    table.CheckConstraint("CK_GuildSettings_State_Value", "(\"State\" = 'CustomValue' AND \"Value\" IS NOT NULL)\r\n                OR (\"State\" IN ('Default', 'Disabled') AND \"Value\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "MessageLogChannelOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogChannelOverrides", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "Mutes",
                schema: "Settings",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EventType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SinId = table.Column<long>(type: "bigint", nullable: true),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutes", x => new { x.UserId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                schema: "Settings",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RewardType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RewardLevel = table.Column<int>(type: "integer", nullable: true),
                    RewardMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => new { x.GuildId, x.RoleId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "SpamFilterOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpamFilterOverrides", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "ThreadLocks",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EventType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadLocks", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "XpTrackedItems",
                schema: "Settings",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Type = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpTrackedItems", x => new { x.GuildId, x.Id, x.SetAt });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ChannelLocks",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_GuildSettings_GuildId_Type_SetAt",
                schema: "Settings",
                table: "GuildSettings",
                columns: new[] { "GuildId", "Type", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogChannelOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "MessageLogChannelOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogChannelOverrides_GuildId",
                schema: "Settings",
                table: "MessageLogChannelOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId_SetAt",
                schema: "Settings",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId_RoleId_SetAt",
                schema: "Settings",
                table: "Rewards",
                columns: new[] { "GuildId", "RoleId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "SpamFilterOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_GuildId",
                schema: "Settings",
                table: "SpamFilterOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ThreadLocks",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_XpTrackedItems_GuildId_Id_SetAt",
                schema: "Settings",
                table: "XpTrackedItems",
                columns: new[] { "GuildId", "Id", "SetAt" },
                descending: new[] { false, false, true });

            // ================================================================
            // DATA MIGRATION
            // Copy existing settings from Grimoire.Data (public schema) tables
            // into the new Settings schema. Source tables are still live at this
            // point; the GrimoireDbContext ModelRedesign migration drops them.
            //
            // Sentinel values used where the source table had no equivalent:
            //   SetAt  = DateTimeOffset.MinValue ('0001-01-01T00:00:00Z')
            //   SetBy / ModeratorId = 0 (unknown / system)
            //
            // Module-enabled flags: only insert when true — the Settings layer
            // treats a missing record as disabled (ParseEnabled returns false
            // for null CachedSetting).
            //
            // Nullable channel/role IDs: only insert where NOT NULL — a missing
            // row means "use the default" (no channel configured).
            // ================================================================

            // ---- GuildSettings: CustomCommandsModuleEnabled ----
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'CustomCommandsModuleEnabled', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', 'True'
                FROM public."GuildCommandsSettings"
                WHERE "ModuleEnabled";
                """);

            // ---- GuildSettings: General channel settings (from Guilds) ----
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'ModerationLogChannel', "Id", '0001-01-01T00:00:00Z', 0, 'CustomValue', "ModChannelLog"::text
                FROM public."Guilds"
                WHERE "ModChannelLog" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'UserCommandChannel', "Id", '0001-01-01T00:00:00Z', 0, 'CustomValue', "UserCommandChannelId"::text
                FROM public."Guilds"
                WHERE "UserCommandChannelId" IS NOT NULL;
                """);

            // ---- GuildSettings: Leveling ----
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'LevelingModuleEnabled', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', 'True'
                FROM public."GuildLevelSettings"
                WHERE "ModuleEnabled";
                """);

            // TextTime is a PostgreSQL interval; convert to C# TimeSpan "c" format (HH:MM:SS).
            // XpTimeoutPeriod is constrained 1–60 minutes so a day component never appears.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'XpTimeoutPeriod', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue',
                    lpad(floor(extract(epoch from "TextTime") / 3600)::bigint::text, 2, '0') || ':' ||
                    lpad((floor(extract(epoch from "TextTime") / 60) % 60)::bigint::text, 2, '0') || ':' ||
                    lpad((floor(extract(epoch from "TextTime")) % 60)::bigint::text, 2, '0')
                FROM public."GuildLevelSettings";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'LevelScalingBase', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "Base"::text
                FROM public."GuildLevelSettings";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'LevelScalingModifier', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "Modifier"::text
                FROM public."GuildLevelSettings";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'XpGainAmount', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "Amount"::text
                FROM public."GuildLevelSettings";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'LevelingLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "LevelChannelLogId"::text
                FROM public."GuildLevelSettings"
                WHERE "LevelChannelLogId" IS NOT NULL;
                """);

            // ---- GuildSettings: Message log ----
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'MessageLogModuleEnabled', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', 'True'
                FROM public."GuildMessageLogSettings"
                WHERE "ModuleEnabled";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'DeleteLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "DeleteChannelLogId"::text
                FROM public."GuildMessageLogSettings"
                WHERE "DeleteChannelLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'BulkDeleteLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "BulkDeleteChannelLogId"::text
                FROM public."GuildMessageLogSettings"
                WHERE "BulkDeleteChannelLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'EditLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "EditChannelLogId"::text
                FROM public."GuildMessageLogSettings"
                WHERE "EditChannelLogId" IS NOT NULL;
                """);

            // ---- GuildSettings: Moderation ----
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'ModerationModuleEnabled', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', 'True'
                FROM public."GuildModerationSettings"
                WHERE "ModuleEnabled";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'AntiSpamModuleEnabled', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', 'True'
                FROM public."GuildModerationSettings"
                WHERE "AntiSpamEnabled";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'PublicModerationLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "PublicBanLog"::text
                FROM public."GuildModerationSettings"
                WHERE "PublicBanLog" IS NOT NULL;
                """);

            // AutoPardonAfter is an interval that can span multiple days (default 10950 d = 30 yr).
            // C# TimeSpan "c" format: "d.HH:MM:SS" when days > 0, else "HH:MM:SS".
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'SinAutoPardonDuration', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue',
                    CASE
                        WHEN extract(epoch from "AutoPardonAfter") >= 86400 THEN
                            floor(extract(epoch from "AutoPardonAfter") / 86400)::bigint::text || '.' ||
                            lpad((floor(extract(epoch from "AutoPardonAfter") / 3600) % 24)::bigint::text, 2, '0') || ':' ||
                            lpad((floor(extract(epoch from "AutoPardonAfter") / 60) % 60)::bigint::text, 2, '0') || ':' ||
                            lpad((floor(extract(epoch from "AutoPardonAfter")) % 60)::bigint::text, 2, '0')
                        ELSE
                            lpad(floor(extract(epoch from "AutoPardonAfter") / 3600)::bigint::text, 2, '0') || ':' ||
                            lpad((floor(extract(epoch from "AutoPardonAfter") / 60) % 60)::bigint::text, 2, '0') || ':' ||
                            lpad((floor(extract(epoch from "AutoPardonAfter")) % 60)::bigint::text, 2, '0')
                    END
                FROM public."GuildModerationSettings";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'MuteRole', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "MuteRole"::text
                FROM public."GuildModerationSettings"
                WHERE "MuteRole" IS NOT NULL;
                """);

            // ---- GuildSettings: User log ----
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'UserLogModuleEnabled', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', 'True'
                FROM public."GuildUserLogSettings"
                WHERE "ModuleEnabled";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'JoinLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "JoinChannelLogId"::text
                FROM public."GuildUserLogSettings"
                WHERE "JoinChannelLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'LeaveLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "LeaveChannelLogId"::text
                FROM public."GuildUserLogSettings"
                WHERE "LeaveChannelLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'UsernameLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "UsernameChannelLogId"::text
                FROM public."GuildUserLogSettings"
                WHERE "UsernameChannelLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'NicknameLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "NicknameChannelLogId"::text
                FROM public."GuildUserLogSettings"
                WHERE "NicknameChannelLogId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT 'AvatarLogChannel', "GuildId", '0001-01-01T00:00:00Z', 0, 'CustomValue', "AvatarChannelLogId"::text
                FROM public."GuildUserLogSettings"
                WHERE "AvatarChannelLogId" IS NOT NULL;
                """);

            // ---- XpTrackedItems (IgnoredChannels / IgnoredMembers / IgnoredRoles) ----
            // Old tables have no SetAt/SetBy; use sentinel values.
            // Discord snowflake IDs are globally unique per entity type so channel/user/role
            // ID collisions for the same GuildId are astronomically unlikely.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."XpTrackedItems" ("Id", "GuildId", "SetAt", "SetBy", "Type")
                SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z', 0, 'IgnoredChannel'
                FROM public."IgnoredChannels";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."XpTrackedItems" ("Id", "GuildId", "SetAt", "SetBy", "Type")
                SELECT "UserId", "GuildId", '0001-01-01T00:00:00Z', 0, 'IgnoredMember'
                FROM public."IgnoredMembers";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Settings"."XpTrackedItems" ("Id", "GuildId", "SetAt", "SetBy", "Type")
                SELECT "RoleId", "GuildId", '0001-01-01T00:00:00Z', 0, 'IgnoredRole'
                FROM public."IgnoredRoles";
                """);

            // ---- ChannelLocks (from public.Locks) ----
            // Old Locks.ModeratorId is nullable; COALESCE to 0 for the sentinel.
            // Old Locks.Reason is an empty string when not set; NULLIF maps it to NULL.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."ChannelLocks"
                    ("ChannelId", "GuildId", "SetAt", "ModeratorId", "Reason", "EventType",
                     "PreviouslyAllowed", "PreviouslyDenied", "EndTime")
                SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z',
                    COALESCE("ModeratorId", 0), NULLIF("Reason", ''), 'Locked',
                    "PreviouslyAllowed", "PreviouslyDenied", "EndTime"
                FROM public."Locks";
                """);

            // ---- Mutes (from public.Mutes) ----
            // Old Mutes table has no ModeratorId column; use sentinel 0.
            // SinId is the PK of the old table and is always non-null, but guard anyway.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."Mutes"
                    ("UserId", "GuildId", "SetAt", "ModeratorId", "EventType", "SinId", "EndTime")
                SELECT "UserId", "GuildId", '0001-01-01T00:00:00Z', 0, 'Added', "SinId", "EndTime"
                FROM public."Mutes"
                WHERE "SinId" IS NOT NULL;
                """);

            // ---- Rewards (from public.Rewards) ----
            // Old Rewards table has no SetBy/SetAt; use sentinel values.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."Rewards"
                    ("GuildId", "RoleId", "SetAt", "SetBy", "RewardType", "RewardLevel", "RewardMessage")
                SELECT "GuildId", "RoleId", '0001-01-01T00:00:00Z', 0, 'Added', "RewardLevel", "RewardMessage"
                FROM public."Rewards";
                """);

            // ---- MessageLogChannelOverrides (from public.MessagesLogChannelOverrides) ----
            // Old ChannelOption is stored as int: 0 = AlwaysLog, 1 = NeverLog.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."MessageLogChannelOverrides"
                    ("ChannelId", "GuildId", "SetAt", "ChannelOption", "SetBy")
                SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z',
                    CASE "ChannelOption" WHEN 0 THEN 'AlwaysLog' WHEN 1 THEN 'NeverLog' ELSE 'Inherit' END,
                    0
                FROM public."MessagesLogChannelOverrides";
                """);

            // ---- SpamFilterOverrides (from public.SpamFilterOverrides) ----
            // Old ChannelOption is stored as int: 0 = AlwaysFilter, 1 = NeverFilter.
            migrationBuilder.Sql("""
                INSERT INTO "Settings"."SpamFilterOverrides"
                    ("ChannelId", "GuildId", "SetAt", "ChannelOption", "SetBy")
                SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z',
                    CASE "ChannelOption" WHEN 0 THEN 'AlwaysFilter' WHEN 1 THEN 'NeverFilter' ELSE 'Inherit' END,
                    0
                FROM public."SpamFilterOverrides";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelLocks",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "GuildSettings",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "MessageLogChannelOverrides",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "Mutes",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "Rewards",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "SpamFilterOverrides",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "ThreadLocks",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "XpTrackedItems",
                schema: "Settings");
        }
    }
}
