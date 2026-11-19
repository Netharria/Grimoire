// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Migrations
{
    /// <inheritdoc />
    public partial class SyncIndexesWithModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Indexes and the Members alternate key left behind by the foreign keys that
            // DropOrphanedLegacyForeignKeys removed. The model snapshot no longer lists any of them.
            // idx_leaderboard_view_guild_user is kept: REFRESH MATERIALIZED VIEW CONCURRENTLY needs it.
            migrationBuilder.DropIndex(
                name: "IX_Channels_GuildId",
                table: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommandsRole_RoleId",
                table: "CustomCommandsRole");

            migrationBuilder.DropIndex(
                name: "IX_GuildLevelSettings_LevelChannelLogId",
                table: "GuildLevelSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildMessageLogSettings_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildMessageLogSettings_DeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildMessageLogSettings_EditChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_AvatarChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_JoinChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_LeaveChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_NicknameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_UsernameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_UserCommandChannelId",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredChannels_GuildId",
                table: "IgnoredChannels");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredMembers_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredRoles_GuildId",
                table: "IgnoredRoles");

            migrationBuilder.DropIndex(
                name: "IX_Locks_GuildId",
                table: "Locks");

            migrationBuilder.DropIndex(
                name: "IX_Locks_ModeratorId_GuildId",
                table: "Locks");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Members_GuildId_UserId",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_MessageHistory_GuildId_DeletedByModeratorId",
                table: "MessageHistory");

            migrationBuilder.DropIndex(
                name: "IX_MessagesLogChannelOverrides_GuildId",
                table: "MessagesLogChannelOverrides");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChannelId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_GuildId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_UserId_GuildId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_GuildId",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_UserId_GuildId",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_NicknameHistory_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropIndex(
                name: "IX_OldLogMessages_ChannelId",
                table: "OldLogMessages");

            migrationBuilder.DropIndex(
                name: "IX_OldLogMessages_GuildId",
                table: "OldLogMessages");

            migrationBuilder.DropIndex(
                name: "IX_Pardons_GuildId",
                table: "Pardons");

            migrationBuilder.DropIndex(
                name: "IX_Pardons_ModeratorId_GuildId",
                table: "Pardons");

            migrationBuilder.DropIndex(
                name: "IX_Reaction_GuildId",
                table: "Reaction");

            migrationBuilder.DropIndex(
                name: "IX_Reaction_UserId_GuildId",
                table: "Reaction");

            migrationBuilder.DropIndex(
                name: "IX_Sins_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sins_ModeratorId_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sins_UserId_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_SpamFilterOverrides_GuildId",
                table: "SpamFilterOverrides");

            // The model has declared these since ModelRedesign, but no migration ever created them.
            // They replace IX_Sins_GuildId, IX_Sins_ModeratorId_GuildId and IX_Sins_UserId_GuildId above.
            migrationBuilder.CreateIndex(
                name: "IX_Sin_Id_GuildId",
                table: "Sins",
                columns: new[] { "Id", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sin_ModeratorId_GuildId_SinType",
                table: "Sins",
                columns: new[] { "ModeratorId", "GuildId", "SinType" });

            migrationBuilder.CreateIndex(
                name: "IX_Sin_UserId_GuildId_SinOn",
                table: "Sins",
                columns: new[] { "UserId", "GuildId", "SinOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sin_UserId_GuildId_SinOn",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sin_ModeratorId_GuildId_SinType",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sin_Id_GuildId",
                table: "Sins");

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_GuildId",
                table: "SpamFilterOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sins_UserId_GuildId",
                table: "Sins",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sins_ModeratorId_GuildId",
                table: "Sins",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sins_GuildId",
                table: "Sins",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_UserId_GuildId",
                table: "Reaction",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_GuildId",
                table: "Reaction",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_ModeratorId_GuildId",
                table: "Pardons",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_GuildId",
                table: "Pardons",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_GuildId",
                table: "OldLogMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_ChannelId",
                table: "OldLogMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_GuildId",
                table: "NicknameHistory",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_GuildId",
                table: "Mutes",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId_GuildId",
                table: "Messages",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GuildId",
                table: "Messages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChannelId",
                table: "Messages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesLogChannelOverrides_GuildId",
                table: "MessagesLogChannelOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_GuildId_DeletedByModeratorId",
                table: "MessageHistory",
                columns: new[] { "GuildId", "ModeratorId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Members_GuildId_UserId",
                table: "Members",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locks_ModeratorId_GuildId",
                table: "Locks",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locks_GuildId",
                table: "Locks",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredRoles_GuildId",
                table: "IgnoredRoles",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_GuildId",
                table: "IgnoredMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredChannels_GuildId",
                table: "IgnoredChannels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_UserCommandChannelId",
                table: "Guilds",
                column: "UserCommandChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog");

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_UsernameChannelLogId",
                table: "GuildUserLogSettings",
                column: "UsernameChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_NicknameChannelLogId",
                table: "GuildUserLogSettings",
                column: "NicknameChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_LeaveChannelLogId",
                table: "GuildUserLogSettings",
                column: "LeaveChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_JoinChannelLogId",
                table: "GuildUserLogSettings",
                column: "JoinChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_AvatarChannelLogId",
                table: "GuildUserLogSettings",
                column: "AvatarChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMessageLogSettings_EditChannelLogId",
                table: "GuildMessageLogSettings",
                column: "EditChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMessageLogSettings_DeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "DeleteChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMessageLogSettings_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "BulkDeleteChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLevelSettings_LevelChannelLogId",
                table: "GuildLevelSettings",
                column: "LevelChannelLogId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommandsRole_RoleId",
                table: "CustomCommandsRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");
        }
    }
}
