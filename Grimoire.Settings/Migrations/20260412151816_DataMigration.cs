using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Settings.Migrations
{
    /// <inheritdoc />
    public partial class DataMigration : Migration
    {
        // Marker values so Down() can target only rows inserted by this migration.
        private const ulong MigrationSetBy = 0;
        private const string MigrationSetAt = "2026-04-12T00:00:00Z";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($$"""
                INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                SELECT x."Type", x."GuildId", x."SetAt", x."SetBy", x."State", x."Value"
                FROM (
                    -- Guild (legacy general settings): default is null, so only non-null.
                    SELECT {{(int)GuildSettingType.ModerationLogChannel}} AS "Type",
                           g."Id" AS "GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           g."ModChannelLog"::text AS "Value"
                    FROM public."Guilds" g
                    WHERE g."ModChannelLog" IS NOT NULL

                    UNION ALL

                    SELECT {{(int)GuildSettingType.UserCommandChannel}} AS "Type",
                           g."Id" AS "GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           g."UserCommandChannelId"::text AS "Value"
                    FROM public."Guilds" g
                    WHERE g."UserCommandChannelId" IS NOT NULL

                    -- GuildCommandsSettings: default ModuleEnabled = false, so only true.
                    UNION ALL
                    SELECT {{(int)GuildSettingType.CustomCommandsModuleEnabled}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           'True' AS "Value"
                    FROM public."GuildCommandsSettings" s
                    WHERE s."ModuleEnabled" = TRUE

                    -- GuildLevelSettings defaults:
                    -- TextTime=00:03:00, Base=15, Modifier=50, Amount=5, LevelChannelLogId=null, ModuleEnabled=false.
                    UNION ALL
                    SELECT {{(int)GuildSettingType.TextTime}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           (EXTRACT(day FROM s."TextTime")::bigint::text || '.' || TO_CHAR(s."TextTime", 'HH24:MI:SS')) AS "Value"
                    FROM public."GuildLevelSettings" s
                    WHERE s."TextTime" <> INTERVAL '00:03:00'

                    UNION ALL
                    SELECT {{(int)GuildSettingType.LevelScalingBase}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."Base"::text AS "Value"
                    FROM public."GuildLevelSettings" s
                    WHERE s."Base" <> 15

                    UNION ALL
                    SELECT {{(int)GuildSettingType.LevelScalingModifier}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."Modifier"::text AS "Value"
                    FROM public."GuildLevelSettings" s
                    WHERE s."Modifier" <> 50

                    UNION ALL
                    SELECT {{(int)GuildSettingType.XpGainAmount}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."Amount"::text AS "Value"
                    FROM public."GuildLevelSettings" s
                    WHERE s."Amount" <> 5

                    UNION ALL
                    SELECT {{(int)GuildSettingType.LevelingLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."LevelChannelLogId"::text AS "Value"
                    FROM public."GuildLevelSettings" s
                    WHERE s."LevelChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.LevelingModuleEnabled}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           'True' AS "Value"
                    FROM public."GuildLevelSettings" s
                    WHERE s."ModuleEnabled" = TRUE

                    -- GuildMessageLogSettings defaults: all channel ids null, ModuleEnabled=false.
                    UNION ALL
                    SELECT {{(int)GuildSettingType.DeleteLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."DeleteChannelLogId"::text AS "Value"
                    FROM public."GuildMessageLogSettings" s
                    WHERE s."DeleteChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.BulkDeleteLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."BulkDeleteChannelLogId"::text AS "Value"
                    FROM public."GuildMessageLogSettings" s
                    WHERE s."BulkDeleteChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.EditLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."EditChannelLogId"::text AS "Value"
                    FROM public."GuildMessageLogSettings" s
                    WHERE s."EditChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.MessageLogModuleEnabled}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           'True' AS "Value"
                    FROM public."GuildMessageLogSettings" s
                    WHERE s."ModuleEnabled" = TRUE

                    -- GuildModerationSettings defaults:
                    -- PublicBanLog=null, AutoPardonAfter=10950 days, MuteRole=null, ModuleEnabled=false.
                    -- AntiSpamEnabled had no DB default; this treats TRUE as non-default to avoid writing explicit false rows.
                    UNION ALL
                    SELECT {{(int)GuildSettingType.PublicModerationLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."PublicBanLog"::text AS "Value"
                    FROM public."GuildModerationSettings" s
                    WHERE s."PublicBanLog" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.SinAutoPardonDuration}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           (EXTRACT(day FROM s."AutoPardonAfter")::bigint::text || '.' || TO_CHAR(s."AutoPardonAfter", 'HH24:MI:SS')) AS "Value"
                    FROM public."GuildModerationSettings" s
                    WHERE s."AutoPardonAfter" <> INTERVAL '10950 days'

                    UNION ALL
                    SELECT {{(int)GuildSettingType.MuteRole}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."MuteRole"::text AS "Value"
                    FROM public."GuildModerationSettings" s
                    WHERE s."MuteRole" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.AntiSpamModuleEnabled}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           'True' AS "Value"
                    FROM public."GuildModerationSettings" s
                    WHERE s."AntiSpamEnabled" = TRUE

                    UNION ALL
                    SELECT {{(int)GuildSettingType.ModerationModuleEnabled}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           'True' AS "Value"
                    FROM public."GuildModerationSettings" s
                    WHERE s."ModuleEnabled" = TRUE

                    -- GuildUserLogSettings defaults: all channel ids null, ModuleEnabled=false.
                    UNION ALL
                    SELECT {{(int)GuildSettingType.JoinLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."JoinChannelLogId"::text AS "Value"
                    FROM public."GuildUserLogSettings" s
                    WHERE s."JoinChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.LeaveLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."LeaveChannelLogId"::text AS "Value"
                    FROM public."GuildUserLogSettings" s
                    WHERE s."LeaveChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.UsernameLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."UsernameChannelLogId"::text AS "Value"
                    FROM public."GuildUserLogSettings" s
                    WHERE s."UsernameChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.NicknameLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."NicknameChannelLogId"::text AS "Value"
                    FROM public."GuildUserLogSettings" s
                    WHERE s."NicknameChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.AvatarLogChannel}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           s."AvatarChannelLogId"::text AS "Value"
                    FROM public."GuildUserLogSettings" s
                    WHERE s."AvatarChannelLogId" IS NOT NULL

                    UNION ALL
                    SELECT {{(int)GuildSettingType.UserLogModuleEnabled}} AS "Type",
                           s."GuildId",
                           TIMESTAMPTZ '{{MigrationSetAt}}' AS "SetAt",
                           {{MigrationSetBy}}::numeric(20,0) AS "SetBy",
                           2 AS "State",
                           'True' AS "Value"
                    FROM public."GuildUserLogSettings" s
                    WHERE s."ModuleEnabled" = TRUE
                ) x
                ON CONFLICT ("Type", "GuildId", "SetAt") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($$"""
                DELETE FROM "Settings"."GuildSettings"
                WHERE "SetBy" = {{MigrationSetBy}}::numeric(20,0)
                  AND "SetAt" = TIMESTAMPTZ '{{MigrationSetAt}}'
                  AND "Type" IN (
                      {{(int)GuildSettingType.CustomCommandsModuleEnabled}},
                      {{(int)GuildSettingType.ModerationLogChannel}},
                      {{(int)GuildSettingType.UserCommandChannel}},
                      {{(int)GuildSettingType.TextTime}},
                      {{(int)GuildSettingType.LevelScalingBase}},
                      {{(int)GuildSettingType.LevelScalingModifier}},
                      {{(int)GuildSettingType.XpGainAmount}},
                      {{(int)GuildSettingType.LevelingLogChannel}},
                      {{(int)GuildSettingType.LevelingModuleEnabled}},
                      {{(int)GuildSettingType.DeleteLogChannel}},
                      {{(int)GuildSettingType.BulkDeleteLogChannel}},
                      {{(int)GuildSettingType.EditLogChannel}},
                      {{(int)GuildSettingType.MessageLogModuleEnabled}},
                      {{(int)GuildSettingType.PublicModerationLogChannel}},
                      {{(int)GuildSettingType.SinAutoPardonDuration}},
                      {{(int)GuildSettingType.MuteRole}},
                      {{(int)GuildSettingType.AntiSpamModuleEnabled}},
                      {{(int)GuildSettingType.ModerationModuleEnabled}},
                      {{(int)GuildSettingType.JoinLogChannel}},
                      {{(int)GuildSettingType.LeaveLogChannel}},
                      {{(int)GuildSettingType.UsernameLogChannel}},
                      {{(int)GuildSettingType.NicknameLogChannel}},
                      {{(int)GuildSettingType.AvatarLogChannel}},
                      {{(int)GuildSettingType.UserLogModuleEnabled}}
                  );
                """);
        }
    }
}
