using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Settings.Migrations
{
    /// <inheritdoc />
    public partial class DataMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==================== GuildSettings ====================

            // Module flags: old default is false (disabled).
            // Insert a 'CustomValue' of 'true' only when ModuleEnabled = true.
            // Guilds with ModuleEnabled = false keep no row, preserving the old default.
            // Each block is wrapped in a DO/EXCEPTION to gracefully skip when the source
            // table does not exist (fresh install or test environment).

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'CustomCommandsModuleEnabled', "GuildId", NOW(), 0, 'CustomValue', 'True'
                    FROM public."GuildCommandsSettings"
                    WHERE "ModuleEnabled" = true;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'LevelingModuleEnabled', "GuildId", NOW(), 0, 'CustomValue', 'True'
                    FROM public."GuildLevelSettings"
                    WHERE "ModuleEnabled" = true;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'MessageLogModuleEnabled', "GuildId", NOW(), 0, 'CustomValue', 'True'
                    FROM public."GuildMessageLogSettings"
                    WHERE "ModuleEnabled" = true;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'ModerationModuleEnabled', "GuildId", NOW(), 0, 'CustomValue', 'True'
                    FROM public."GuildModerationSettings"
                    WHERE "ModuleEnabled" = true;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'AntiSpamModuleEnabled', "GuildId", NOW(), 0, 'CustomValue', 'True'
                    FROM public."GuildModerationSettings"
                    WHERE "AntiSpamEnabled" = true;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'UserLogModuleEnabled', "GuildId", NOW(), 0, 'CustomValue', 'True'
                    FROM public."GuildUserLogSettings"
                    WHERE "ModuleEnabled" = true;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // Leveling numeric settings — skip if value matches new system defaults
            // (TextTime default = 3 min, LevelScalingBase default = 15,
            //  LevelScalingModifier default = 50, XpGainAmount default = 5)

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'TextTime', "GuildId", NOW(), 0, 'CustomValue', TO_CHAR("TextTime", 'HH24:MI:SS')
                    FROM public."GuildLevelSettings"
                    WHERE "TextTime" <> INTERVAL '3 minutes';
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'LevelScalingBase', "GuildId", NOW(), 0, 'CustomValue', "Base"::text
                    FROM public."GuildLevelSettings"
                    WHERE "Base" <> 15;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'LevelScalingModifier', "GuildId", NOW(), 0, 'CustomValue', "Modifier"::text
                    FROM public."GuildLevelSettings"
                    WHERE "Modifier" <> 50;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'XpGainAmount', "GuildId", NOW(), 0, 'CustomValue', "Amount"::text
                    FROM public."GuildLevelSettings"
                    WHERE "Amount" <> 5;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // Leveling log channel — only if configured
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'LevelingLogChannel', "GuildId", NOW(), 0, 'CustomValue', "LevelChannelLogId"::text
                    FROM public."GuildLevelSettings"
                    WHERE "LevelChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // Message log channels — only if configured
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'DeleteLogChannel', "GuildId", NOW(), 0, 'CustomValue', "DeleteChannelLogId"::text
                    FROM public."GuildMessageLogSettings"
                    WHERE "DeleteChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'BulkDeleteLogChannel', "GuildId", NOW(), 0, 'CustomValue', "BulkDeleteChannelLogId"::text
                    FROM public."GuildMessageLogSettings"
                    WHERE "BulkDeleteChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'EditLogChannel', "GuildId", NOW(), 0, 'CustomValue', "EditChannelLogId"::text
                    FROM public."GuildMessageLogSettings"
                    WHERE "EditChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // Moderation settings — only if configured
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'PublicModerationLogChannel', "GuildId", NOW(), 0, 'CustomValue', "PublicBanLog"::text
                    FROM public."GuildModerationSettings"
                    WHERE "PublicBanLog" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'MuteRole', "GuildId", NOW(), 0, 'CustomValue', "MuteRole"::text
                    FROM public."GuildModerationSettings"
                    WHERE "MuteRole" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // SinAutoPardonDuration — skip if matches legacy default (10950 days / 30 years).
            // Converts PostgreSQL interval to C# TimeSpan "c" constant format (d.HH:MM:SS).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'SinAutoPardonDuration', "GuildId", NOW(), 0, 'CustomValue',
                        FLOOR(EXTRACT(epoch FROM "AutoPardonAfter") / 86400)::bigint::text || '.' ||
                        TO_CHAR(
                            MAKE_INTERVAL(secs => (FLOOR(EXTRACT(epoch FROM "AutoPardonAfter"))::bigint % 86400)::double precision),
                            'HH24:MI:SS'
                        )
                    FROM public."GuildModerationSettings"
                    WHERE "AutoPardonAfter" <> INTERVAL '10950 days';
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // User log channels — only if configured
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'JoinLogChannel', "GuildId", NOW(), 0, 'CustomValue', "JoinChannelLogId"::text
                    FROM public."GuildUserLogSettings"
                    WHERE "JoinChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'LeaveLogChannel', "GuildId", NOW(), 0, 'CustomValue', "LeaveChannelLogId"::text
                    FROM public."GuildUserLogSettings"
                    WHERE "LeaveChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'UsernameLogChannel', "GuildId", NOW(), 0, 'CustomValue', "UsernameChannelLogId"::text
                    FROM public."GuildUserLogSettings"
                    WHERE "UsernameChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'NicknameLogChannel', "GuildId", NOW(), 0, 'CustomValue', "NicknameChannelLogId"::text
                    FROM public."GuildUserLogSettings"
                    WHERE "NicknameChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'AvatarLogChannel', "GuildId", NOW(), 0, 'CustomValue', "AvatarChannelLogId"::text
                    FROM public."GuildUserLogSettings"
                    WHERE "AvatarChannelLogId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // General guild settings from the Guilds table
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'ModerationLogChannel', "Id", NOW(), 0, 'CustomValue', "ModChannelLog"::text
                    FROM public."Guilds"
                    WHERE "ModChannelLog" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."GuildSettings" ("Type", "GuildId", "SetAt", "SetBy", "State", "Value")
                    SELECT 'UserCommandChannel', "Id", NOW(), 0, 'CustomValue', "UserCommandChannelId"::text
                    FROM public."Guilds"
                    WHERE "UserCommandChannelId" IS NOT NULL;
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== XpIgnoredItems ====================

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."XpIgnoredItems" ("GuildId", "Id", "SetAt", "SetBy", "Enabled", "Type")
                    SELECT "GuildId", "ChannelId", NOW(), 0, true, 'Channel'
                    FROM public."IgnoredChannels";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."XpIgnoredItems" ("GuildId", "Id", "SetAt", "SetBy", "Enabled", "Type")
                    SELECT "GuildId", "UserId", NOW(), 0, true, 'Member'
                    FROM public."IgnoredMembers";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."XpIgnoredItems" ("GuildId", "Id", "SetAt", "SetBy", "Enabled", "Type")
                    SELECT "GuildId", "RoleId", NOW(), 0, true, 'Role'
                    FROM public."IgnoredRoles";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== MessagesLogChannelOverrides ====================
            // Old ChannelOption was stored as integer (0=AlwaysLog, 1=NeverLog).

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."MessagesLogChannelOverrides" ("ChannelId", "GuildId", "SetAt", "SetBy", "ChannelOption")
                    SELECT "ChannelId", "GuildId", NOW(), 0,
                        CASE "ChannelOption"
                            WHEN 0 THEN 'AlwaysLog'
                            WHEN 1 THEN 'NeverLog'
                        END
                    FROM public."MessagesLogChannelOverrides";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== SpamFilterOverrides ====================
            // Old ChannelOption was stored as integer (0=AlwaysFilter, 1=NeverFilter).

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."SpamFilterOverrides" ("ChannelId", "GuildId", "SetAt", "SetBy", "ChannelOption")
                    SELECT "ChannelId", "GuildId", NOW(), 0,
                        CASE "ChannelOption"
                            WHEN 0 THEN 'AlwaysFilter'
                            WHEN 1 THEN 'NeverFilter'
                        END
                    FROM public."SpamFilterOverrides";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== Rewards ====================
            // Old Rewards had no Enabled flag; existence implied enabled.

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."Rewards" ("GuildId", "RoleId", "SetAt", "SetBy", "RewardLevel", "RewardMessage", "Enabled")
                    SELECT "GuildId", "RoleId", NOW(), 0, "RewardLevel", "RewardMessage", true
                    FROM public."Rewards";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== Mutes ====================

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."Mutes" ("SinId", "EndTime", "UserId", "GuildId")
                    SELECT "SinId", "EndTime", "UserId", "GuildId"
                    FROM public."Mutes";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== Locks ====================
            // Old ModeratorId was nullable; new schema requires it — fall back to 0.

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."Locks" ("ChannelId", "PreviouslyAllowed", "PreviouslyDenied", "ModeratorId", "Reason", "EndTime", "GuildId")
                    SELECT "ChannelId", "PreviouslyAllowed", "PreviouslyDenied", COALESCE("ModeratorId", 0), "Reason", "EndTime", "GuildId"
                    FROM public."Locks";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);

            // ==================== Trackers ====================
            // Old ModeratorId was nullable; new schema requires it — fall back to 0.

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    INSERT INTO "Settings"."Trackers" ("UserId", "GuildId", "LogChannelId", "EndTime", "ModeratorId")
                    SELECT "UserId", "GuildId", "LogChannelId", "EndTime", COALESCE("ModeratorId", 0)
                    FROM public."Trackers";
                EXCEPTION WHEN undefined_table THEN NULL;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Settings"."GuildSettings";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."XpIgnoredItems";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."MessagesLogChannelOverrides";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."SpamFilterOverrides";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."Rewards";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."Mutes";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."Locks";""");
            migrationBuilder.Sql("""DELETE FROM "Settings"."Trackers";""");
        }
    }
}
