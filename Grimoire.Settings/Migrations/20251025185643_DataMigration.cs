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
            migrationBuilder.Sql(@"
            INSERT INTO ""Settings"".""CustomCommandsSettings""
            (
                ""GuildId"",
                ""ModuleEnabled""
            )
            SELECT
                ""GuildId"",
                ""ModuleEnabled""
            FROM ""GuildCommandsSettings"";

            INSERT INTO ""Settings"".""LevelingSettings""
            (
                ""GuildId"",
                ""TextTime"",
                ""Base"",
                ""Modifier"",
                ""Amount"",
                ""LevelChannelLogId"",
                ""ModuleEnabled""
            )
            SELECT
                ""GuildId"",
                ""TextTime"",
                ""Base"",
                ""Modifier"",
                ""Amount"",
                ""LevelChannelLogId"",
                ""ModuleEnabled""
            FROM ""GuildLevelSettings"";

            INSERT INTO ""Settings"".""UserLogSettings""
            (
                ""GuildId"",
                ""JoinChannelLogId"",
                ""LeaveChannelLogId"",
                ""UsernameChannelLogId"",
                ""NicknameChannelLogId"",
                ""AvatarChannelLogId"",
                ""ModuleEnabled""
            )
            SELECT
                ""GuildId"",
                ""JoinChannelLogId"",
                ""LeaveChannelLogId"",
                ""UsernameChannelLogId"",
                ""NicknameChannelLogId"",
                ""AvatarChannelLogId"",
                ""ModuleEnabled""
            FROM ""GuildUserLogSettings"";

            INSERT INTO ""Settings"".""MessageLogSettings""
            (
                ""GuildId"",
                ""DeleteChannelLogId"",
                ""BulkDeleteChannelLogId"",
                ""EditChannelLogId"",
                ""ModuleEnabled""
            )
            SELECT
                ""GuildId"",
                ""DeleteChannelLogId"",
                ""BulkDeleteChannelLogId"",
                ""EditChannelLogId"",
                ""ModuleEnabled""
            FROM ""GuildMessageLogSettings"";

            INSERT INTO ""Settings"".""ModerationSettings""
            (
                 ""GuildId"",
                 ""PublicBanLog"",
                 ""AutoPardonAfter"",
                 ""MuteRole"",
                 ""ModuleEnabled"",
                 ""AntiSpamEnabled""
            )
            SELECT
                 ""GuildId"",
                 ""PublicBanLog"",
                 ""AutoPardonAfter"",
                 ""MuteRole"",
                 ""ModuleEnabled"",
                 ""AntiSpamEnabled""
            FROM ""GuildModerationSettings"";

            INSERT INTO ""Settings"".""IgnoredChannels""
            (
                ""ChannelId"",
                ""GuildId""
            )
            SELECT
                ""ChannelId"",
                ""GuildId""
            FROM ""IgnoredChannels"";

            INSERT INTO ""Settings"".""IgnoredMembers""
            (
                ""UserId"",
                ""GuildId""
            )
            SELECT
                ""UserId"",
                ""GuildId""
            FROM ""IgnoredMembers"";

            INSERT INTO ""Settings"".""IgnoredRoles""
            (
                ""RoleId"",
                ""GuildId""
            )
            SELECT
                ""RoleId"",
                ""GuildId""
            FROM ""IgnoredRoles"";

            INSERT INTO ""Settings"".""Locks""
            (
                 ""ChannelId"",
                 ""PreviouslyAllowed"",
                 ""PreviouslyDenied"",
                 ""ModeratorId"",
                 ""GuildId"",
                 ""Reason"",
                 ""EndTime""
            )
            SELECT
                 ""ChannelId"",
                 ""PreviouslyAllowed"",
                 ""PreviouslyDenied"",
                 ""ModeratorId"",
                 ""GuildId"",
                 ""Reason"",
                 ""EndTime""
            FROM ""Locks"";

            INSERT INTO ""Settings"".""MessagesLogChannelOverrides""
            (
                ""GuildId"",
                ""ChannelId"",
                ""ChannelOption""
            )
            SELECT
                ""GuildId"",
                ""ChannelId"",
                ""ChannelOption""
            FROM ""MessagesLogChannelOverrides"";

            INSERT INTO ""Settings"".""Mutes""
            (
                ""SinId"",
                ""UserId"",
                ""GuildId"",
                ""EndTime""
            )
            SELECT
                ""SinId"",
                ""UserId"",
                ""GuildId"",
                ""EndTime""
            FROM ""Mutes"";

            INSERT INTO ""Settings"".""Rewards""
            (
                ""GuildId"",
                ""RoleId"",
                ""RewardLevel"",
                ""RewardMessage""
            )
            SELECT
                ""GuildId"",
                ""RoleId"",
                ""RewardLevel"",
                ""RewardMessage""
            FROM ""Rewards"";

            INSERT INTO ""Settings"".""SpamFilterOverrides""
            (
                ""GuildId"",
                ""ChannelId"",
                ""ChannelOption""
            )
            SELECT
                ""GuildId"",
                ""ChannelId"",
                ""ChannelOption""
            FROM ""SpamFilterOverrides"";

            INSERT INTO ""Settings"".""Trackers""
            (
                ""UserId"",
                ""GuildId"",
                ""LogChannelId"",
                ""EndTime"",
                ""ModeratorId""
            )
            SELECT
                ""UserId"",
                ""GuildId"",
                ""LogChannelId"",
                ""EndTime"",
                ""ModeratorId""
            FROM ""Trackers"";

            INSERT INTO ""Settings"".""GuildSettings""
            (
                ""Id"",
                ""ModLogChannelId"",
                ""UserCommandChannelId""
            )
            SELECT

                ""Id"",
                ""ModChannelLog"",
                ""UserCommandChannelId""
            FROM ""Guilds"";


            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
