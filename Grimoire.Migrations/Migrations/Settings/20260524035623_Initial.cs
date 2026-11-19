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
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PreviouslyAllowed = table.Column<long>(type: "bigint", nullable: false),
                    PreviouslyDenied = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedBy = table.Column<long>(type: "bigint", nullable: true),
                    RemoveReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelLocks", x => new { x.GuildId, x.ChannelId, x.SetAt });
                    table.CheckConstraint("CK_ChannelLocks_State", "(\"State\" = 'Pending' AND \"ActivatedAt\" IS NULL AND \"EndedAt\" IS NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"EndedAt\" IS NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Expired' AND \"EndedAt\" IS NOT NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Removed' AND \"EndedAt\" IS NOT NULL AND \"RemovedBy\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                schema: "Settings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    Document = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "jsonb", nullable: false),
                    SetBy = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => new { x.GuildId, x.Document, x.Version });
                    table.CheckConstraint("CK_GuildSettings_Value", "jsonb_typeof(\"Value\") = 'object'");
                    table.CheckConstraint("CK_GuildSettings_Version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "MessageLogChannelOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<long>(type: "bigint", nullable: false)
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
                    SinId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    MuteRoleId = table.Column<long>(type: "bigint", nullable: true),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedBy = table.Column<long>(type: "bigint", nullable: true),
                    RemoveReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutes", x => x.SinId);
                    table.CheckConstraint("CK_Mutes_State", "(\"State\" = 'Pending' AND \"ActivatedAt\" IS NULL AND \"EndedAt\" IS NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"EndedAt\" IS NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Expired' AND \"EndedAt\" IS NOT NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Removed' AND \"EndedAt\" IS NOT NULL AND \"RemovedBy\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                schema: "Settings",
                columns: table => new
                {
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<long>(type: "bigint", nullable: false),
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
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<long>(type: "bigint", nullable: false)
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
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedBy = table.Column<long>(type: "bigint", nullable: true),
                    RemoveReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadLocks", x => new { x.GuildId, x.ChannelId, x.SetAt });
                    table.CheckConstraint("CK_ThreadLocks_State", "(\"State\" = 'Pending' AND \"ActivatedAt\" IS NULL AND \"EndedAt\" IS NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"EndedAt\" IS NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Expired' AND \"EndedAt\" IS NOT NULL AND \"RemovedBy\" IS NULL AND \"RemoveReason\" IS NULL)\nOR (\"State\" = 'Removed' AND \"EndedAt\" IS NOT NULL AND \"RemovedBy\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "XpTrackedItems",
                schema: "Settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpTrackedItems", x => new { x.GuildId, x.Id, x.SetAt });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks",
                column: "EndTime",
                filter: "\"State\" IN ('Pending', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_InForce",
                schema: "Settings",
                table: "ChannelLocks",
                columns: new[] { "GuildId", "ChannelId" },
                unique: true,
                filter: "\"State\" IN ('Pending', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogChannelOverrides_GuildId",
                schema: "Settings",
                table: "MessageLogChannelOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes",
                column: "EndTime",
                filter: "\"State\" IN ('Pending', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_InForce",
                schema: "Settings",
                table: "Mutes",
                columns: new[] { "GuildId", "UserId" },
                unique: true,
                filter: "\"State\" IN ('Pending', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_GuildId",
                schema: "Settings",
                table: "SpamFilterOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks",
                column: "EndTime",
                filter: "\"State\" IN ('Pending', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_InForce",
                schema: "Settings",
                table: "ThreadLocks",
                columns: new[] { "GuildId", "ChannelId" },
                unique: true,
                filter: "\"State\" IN ('Pending', 'Active')");

            // ================================================================
            // DATA MIGRATION (spec 2.2, section 2.5)
            // Copies main's settings out of the legacy public tables into the documents and state rows. The source
            // tables stay; a later release drops them (P-7), and the one chain (C12) keeps that drop after this copy.
            //
            // Documents: one version-1 document per guild and kind, holding only the values that differ from today's
            // defaults, so a default stays a default. A document with nothing in it isn't written: a missing document
            // means all defaults. Ids are strings and durations whole seconds (SettingsJson). A module flag is written
            // only when it is on.
            //
            // Sentinels where the source has no equivalent: SetAt = '0001-01-01T00:00:00Z', SetBy/ModeratorId = 0.
            //
            // Each block checks its source tables exist. They always do once main's migrations have run; the guards
            // stay as cheap insurance.
            // ================================================================

            // ---- Modules: every guild's module flags, in one document (C13) ----
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('Guilds', 'GuildLevelSettings', 'GuildUserLogSettings', 'GuildModerationSettings', 'GuildMessageLogSettings', 'GuildCommandsSettings')) = 6 THEN
                        INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                        SELECT g."GuildId", 'Modules', 1, d.doc, 0, '0001-01-01T00:00:00Z'
                        FROM (SELECT gu."Id"::bigint AS "GuildId", l."ModuleEnabled" AS leveling, u."ModuleEnabled" AS userlog,
                                     mo."ModuleEnabled" AS moderation, mo."AntiSpamEnabled" AS antispam,
                                     ml."ModuleEnabled" AS messagelog, c."ModuleEnabled" AS commands
                              FROM public."Guilds" gu
                              LEFT JOIN public."GuildLevelSettings" l ON l."GuildId" = gu."Id"
                              LEFT JOIN public."GuildUserLogSettings" u ON u."GuildId" = gu."Id"
                              LEFT JOIN public."GuildModerationSettings" mo ON mo."GuildId" = gu."Id"
                              LEFT JOIN public."GuildMessageLogSettings" ml ON ml."GuildId" = gu."Id"
                              LEFT JOIN public."GuildCommandsSettings" c ON c."GuildId" = gu."Id") g
                        CROSS JOIN LATERAL (SELECT jsonb_strip_nulls(jsonb_build_object(
                            'Leveling', NULLIF(g.leveling, false),
                            'UserLog', NULLIF(g.userlog, false),
                            'Moderation', NULLIF(g.moderation, false),
                            'MessageLog', NULLIF(g.messagelog, false),
                            'Commands', NULLIF(g.commands, false),
                            'AntiSpam', NULLIF(g.antispam, false))) AS doc) d
                        WHERE d.doc <> '{}'::jsonb;
                    END IF;
                END $$;
                """);

            // ---- General: the moderation log and the user command channel ----
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('Guilds')) = 1 THEN
                        INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                        SELECT g."GuildId", 'General', 1, d.doc, 0, '0001-01-01T00:00:00Z'
                        FROM (SELECT "Id"::bigint AS "GuildId", * FROM public."Guilds") g
                        CROSS JOIN LATERAL (SELECT jsonb_strip_nulls(jsonb_build_object(
                            'ModerationLogChannel', "ModChannelLog"::bigint::text,
                            'UserCommandChannel', "UserCommandChannelId"::bigint::text)) AS doc) d
                        WHERE d.doc <> '{}'::jsonb;
                    END IF;
                END $$;
                """);

            // ---- Leveling: defaults are 3 minutes, base 15, modifier 50 and 5 XP ----
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('GuildLevelSettings')) = 1 THEN
                        INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                        SELECT g."GuildId", 'Leveling', 1, d.doc, 0, '0001-01-01T00:00:00Z'
                        FROM (SELECT "GuildId"::bigint AS "GuildId", "TextTime", "Base", "Modifier", "Amount", "LevelChannelLogId" FROM public."GuildLevelSettings") g
                        CROSS JOIN LATERAL (SELECT jsonb_strip_nulls(jsonb_build_object(
                            'XpTimeoutSeconds', NULLIF(extract(epoch FROM g."TextTime")::bigint, 180),
                            'LevelScalingBase', NULLIF(g."Base", 15),
                            'LevelScalingModifier', NULLIF(g."Modifier", 50),
                            'XpGainAmount', NULLIF(g."Amount", 5),
                            'LogChannel', g."LevelChannelLogId"::bigint::text)) AS doc) d
                        WHERE d.doc <> '{}'::jsonb;
                    END IF;
                END $$;
                """);

            // ---- MessageLog: the three log channels ----
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('GuildMessageLogSettings')) = 1 THEN
                        INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                        SELECT g."GuildId", 'MessageLog', 1, d.doc, 0, '0001-01-01T00:00:00Z'
                        FROM (SELECT "GuildId"::bigint AS "GuildId", "DeleteChannelLogId", "BulkDeleteChannelLogId", "EditChannelLogId" FROM public."GuildMessageLogSettings") g
                        CROSS JOIN LATERAL (SELECT jsonb_strip_nulls(jsonb_build_object(
                            'DeleteLogChannel', g."DeleteChannelLogId"::bigint::text,
                            'BulkDeleteLogChannel', g."BulkDeleteChannelLogId"::bigint::text,
                            'EditLogChannel', g."EditChannelLogId"::bigint::text)) AS doc) d
                        WHERE d.doc <> '{}'::jsonb;
                    END IF;
                END $$;
                """);

            // ---- Moderation: the public log, auto-pardon (default 10,950 days) and the mute role ----
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('GuildModerationSettings')) = 1 THEN
                        INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                        SELECT g."GuildId", 'Moderation', 1, d.doc, 0, '0001-01-01T00:00:00Z'
                        FROM (SELECT "GuildId"::bigint AS "GuildId", "PublicBanLog", "AutoPardonAfter", "MuteRole" FROM public."GuildModerationSettings") g
                        CROSS JOIN LATERAL (SELECT jsonb_strip_nulls(jsonb_build_object(
                            'PublicModerationLogChannel', g."PublicBanLog"::bigint::text,
                            'AutoPardonAfterSeconds', NULLIF(extract(epoch FROM g."AutoPardonAfter")::bigint, 946080000),
                            'MuteRole', g."MuteRole"::bigint::text)) AS doc) d
                        WHERE d.doc <> '{}'::jsonb;
                    END IF;
                END $$;
                """);

            // ---- UserLog: the five log channels ----
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('GuildUserLogSettings')) = 1 THEN
                        INSERT INTO "Settings"."GuildSettings" ("GuildId", "Document", "Version", "Value", "SetBy", "SetAt")
                        SELECT g."GuildId", 'UserLog', 1, d.doc, 0, '0001-01-01T00:00:00Z'
                        FROM (SELECT "GuildId"::bigint AS "GuildId", "JoinChannelLogId", "LeaveChannelLogId", "UsernameChannelLogId", "NicknameChannelLogId", "AvatarChannelLogId" FROM public."GuildUserLogSettings") g
                        CROSS JOIN LATERAL (SELECT jsonb_strip_nulls(jsonb_build_object(
                            'JoinLogChannel', g."JoinChannelLogId"::bigint::text,
                            'LeaveLogChannel', g."LeaveChannelLogId"::bigint::text,
                            'UsernameLogChannel', g."UsernameChannelLogId"::bigint::text,
                            'NicknameLogChannel', g."NicknameChannelLogId"::bigint::text,
                            'AvatarLogChannel', g."AvatarChannelLogId"::bigint::text)) AS doc) d
                        WHERE d.doc <> '{}'::jsonb;
                    END IF;
                END $$;
                """);

            // ---- XpTrackedItems (IgnoredChannels / IgnoredMembers / IgnoredRoles) ----
            // Old tables have no SetAt/SetBy; use sentinel values.
            // Discord snowflake IDs are globally unique per entity type so channel/user/role
            // ID collisions for the same GuildId are astronomically unlikely.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'IgnoredChannels') THEN
                        INSERT INTO "Settings"."XpTrackedItems" ("Id", "GuildId", "SetAt", "SetBy", "Type")
                        SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z', 0, 'IgnoredChannel'
                        FROM public."IgnoredChannels";
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'IgnoredMembers') THEN
                        INSERT INTO "Settings"."XpTrackedItems" ("Id", "GuildId", "SetAt", "SetBy", "Type")
                        SELECT "UserId", "GuildId", '0001-01-01T00:00:00Z', 0, 'IgnoredMember'
                        FROM public."IgnoredMembers";
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'IgnoredRoles') THEN
                        INSERT INTO "Settings"."XpTrackedItems" ("Id", "GuildId", "SetAt", "SetBy", "Type")
                        SELECT "RoleId", "GuildId", '0001-01-01T00:00:00Z', 0, 'IgnoredRole'
                        FROM public."IgnoredRoles";
                    END IF;
                END $$;
                """);

            // ---- Rewards (from public.Rewards) ----
            // Old Rewards table has no SetBy/SetAt; use sentinel values.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'Rewards') THEN
                        INSERT INTO "Settings"."Rewards"
                            ("GuildId", "RoleId", "SetAt", "SetBy", "RewardType", "RewardLevel", "RewardMessage")
                        SELECT "GuildId", "RoleId", '0001-01-01T00:00:00Z', 0, 'Added', "RewardLevel", "RewardMessage"
                        FROM public."Rewards";
                    END IF;
                END $$;
                """);

            // ---- MessageLogChannelOverrides (from public.MessagesLogChannelOverrides) ----
            // Old ChannelOption is stored as int: 0 = AlwaysLog, 1 = NeverLog.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'MessagesLogChannelOverrides') THEN
                        INSERT INTO "Settings"."MessageLogChannelOverrides"
                            ("ChannelId", "GuildId", "SetAt", "ChannelOption", "SetBy")
                        SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z',
                            CASE "ChannelOption" WHEN 0 THEN 'AlwaysLog' WHEN 1 THEN 'NeverLog' ELSE 'Inherit' END,
                            0
                        FROM public."MessagesLogChannelOverrides";
                    END IF;
                END $$;
                """);

            // ---- SpamFilterOverrides (from public.SpamFilterOverrides) ----
            // Old ChannelOption is stored as int: 0 = AlwaysFilter, 1 = NeverFilter.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'SpamFilterOverrides') THEN
                        INSERT INTO "Settings"."SpamFilterOverrides"
                            ("ChannelId", "GuildId", "SetAt", "ChannelOption", "SetBy")
                        SELECT "ChannelId", "GuildId", '0001-01-01T00:00:00Z',
                            CASE "ChannelOption" WHEN 0 THEN 'AlwaysFilter' WHEN 1 THEN 'NeverFilter' ELSE 'Inherit' END,
                            0
                        FROM public."SpamFilterOverrides";
                    END IF;
                END $$;
                """);
            // ---- ChannelLocks (from public.Locks): each one Active ----
            // The legacy table has no lock time, so the sentinel stands in. Its ModeratorId is nullable (COALESCE to
            // the sentinel), and its Reason is an empty string when not set (NULLIF maps it to NULL).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('Locks')) = 1 THEN
                        INSERT INTO "Settings"."ChannelLocks"
                            ("GuildId", "ChannelId", "Reason", "ModeratorId", "SetAt", "EndTime",
                             "PreviouslyAllowed", "PreviouslyDenied", "State", "ActivatedAt")
                        SELECT "GuildId", "ChannelId", NULLIF("Reason", ''), COALESCE("ModeratorId", 0),
                            '0001-01-01T00:00:00Z', "EndTime", "PreviouslyAllowed", "PreviouslyDenied",
                            'Active', '0001-01-01T00:00:00Z'
                        FROM public."Locks";
                    END IF;
                END $$;
                """);

            // ---- Mutes (from public.Mutes): every one Active, in force or overdue (OQ-2) ----
            // On main a finished mute is deleted, so each row is one or the other. An overdue one expires on the
            // first poll, which takes the role off if the member still has it. The moderator and time come from the
            // mute's sin, and the role from the guild's settings, so the expiry removes the role that was given.
            // main's IX_Mutes_UserId_GuildId (unique since Init) allows one mute per member, so none collides on
            // IX_Mutes_InForce.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM pg_tables
                        WHERE schemaname = 'public' AND tablename IN ('Mutes', 'Sins', 'GuildModerationSettings')) = 3 THEN
                        INSERT INTO "Settings"."Mutes"
                            ("GuildId", "UserId", "SinId", "MuteRoleId", "ModeratorId", "SetAt", "EndTime", "State",
                             "ActivatedAt")
                        SELECT m."GuildId", m."UserId", m."SinId", ms."MuteRole",
                            COALESCE(s."ModeratorId", 0), COALESCE(s."SinOn", '0001-01-01T00:00:00Z'), m."EndTime",
                            'Active', COALESCE(s."SinOn", '0001-01-01T00:00:00Z')
                        FROM public."Mutes" m
                        LEFT JOIN public."Sins" s ON s."Id" = m."SinId"
                        LEFT JOIN public."GuildModerationSettings" ms ON ms."GuildId" = m."GuildId";
                    END IF;
                END $$;
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
