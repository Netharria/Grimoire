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
    public partial class DropOrphanedLegacyForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DomainRemodel dropped these, but ModelRedesign replaced it without those drops, so they
            // survived in the database while the model snapshot forgot them (see also
            // DropOrphanedCustomCommandGuildFKs). They point at the obsolete Guilds, Members, Users,
            // Channels, Roles and Messages tables, which nothing writes to any more, so every insert for
            // a guild or member that joined after the redesign failed. The tables and indexes stay.
            migrationBuilder.DropForeignKey(
                name: "FK_Avatars_Members_UserId_GuildId",
                table: "Avatars");

            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildCommandsSettings_Guilds_GuildId",
                table: "GuildCommandsSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildLevelSettings_Channels_LevelChannelLogId",
                table: "GuildLevelSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildLevelSettings_Guilds_GuildId",
                table: "GuildLevelSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_DeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_EditChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Guilds_GuildId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildModerationSettings_Guilds_GuildId",
                table: "GuildModerationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_AvatarChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_JoinChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_LeaveChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_NicknameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_UsernameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Guilds_GuildId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_UserCommandChannelId",
                table: "Guilds");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredChannels_Channels_ChannelId",
                table: "IgnoredChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredChannels_Guilds_GuildId",
                table: "IgnoredChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredMembers_Guilds_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredMembers_Members_UserId_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredRoles_Guilds_GuildId",
                table: "IgnoredRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredRoles_Roles_RoleId",
                table: "IgnoredRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Locks_Channels_ChannelId",
                table: "Locks");

            migrationBuilder.DropForeignKey(
                name: "FK_Locks_Guilds_GuildId",
                table: "Locks");

            migrationBuilder.DropForeignKey(
                name: "FK_Locks_Members_ModeratorId_GuildId",
                table: "Locks");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Guilds_GuildId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageHistory_Guilds_GuildId",
                table: "MessageHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageHistory_Members_GuildId_DeletedByModeratorId",
                table: "MessageHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Channels_ChannelId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Guilds_GuildId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Members_UserId_GuildId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_MessagesLogChannelOverrides_Channels_ChannelId",
                table: "MessagesLogChannelOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_MessagesLogChannelOverrides_Guilds_GuildId",
                table: "MessagesLogChannelOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_Mutes_Guilds_GuildId",
                table: "Mutes");

            migrationBuilder.DropForeignKey(
                name: "FK_Mutes_Members_UserId_GuildId",
                table: "Mutes");

            migrationBuilder.DropForeignKey(
                name: "FK_NicknameHistory_Guilds_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_NicknameHistory_Members_UserId_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_OldLogMessages_Channels_ChannelId",
                table: "OldLogMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_OldLogMessages_Guilds_GuildId",
                table: "OldLogMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pardons_Guilds_GuildId",
                table: "Pardons");

            migrationBuilder.DropForeignKey(
                name: "FK_Pardons_Members_ModeratorId_GuildId",
                table: "Pardons");

            migrationBuilder.DropForeignKey(
                name: "FK_Reaction_Guilds_GuildId",
                table: "Reaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Reaction_Members_UserId_GuildId",
                table: "Reaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Reaction_Messages_MessageId",
                table: "Reaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Guilds_GuildId",
                table: "Rewards");

            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Roles_RoleId",
                table: "Rewards");

            migrationBuilder.DropForeignKey(
                name: "FK_Sins_Guilds_GuildId",
                table: "Sins");

            migrationBuilder.DropForeignKey(
                name: "FK_Sins_Members_ModeratorId_GuildId",
                table: "Sins");

            migrationBuilder.DropForeignKey(
                name: "FK_Sins_Members_UserId_GuildId",
                table: "Sins");

            migrationBuilder.DropForeignKey(
                name: "FK_SpamFilterOverrides_Channels_ChannelId",
                table: "SpamFilterOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_SpamFilterOverrides_Guilds_GuildId",
                table: "SpamFilterOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_UsernameHistory_Users_UserId",
                table: "UsernameHistory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_UsernameHistory_Users_UserId",
                table: "UsernameHistory",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpamFilterOverrides_Guilds_GuildId",
                table: "SpamFilterOverrides",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpamFilterOverrides_Channels_ChannelId",
                table: "SpamFilterOverrides",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sins_Members_UserId_GuildId",
                table: "Sins",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sins_Members_ModeratorId_GuildId",
                table: "Sins",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sins_Guilds_GuildId",
                table: "Sins",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Roles_RoleId",
                table: "Rewards",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Guilds_GuildId",
                table: "Rewards",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reaction_Messages_MessageId",
                table: "Reaction",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reaction_Members_UserId_GuildId",
                table: "Reaction",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reaction_Guilds_GuildId",
                table: "Reaction",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pardons_Members_ModeratorId_GuildId",
                table: "Pardons",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pardons_Guilds_GuildId",
                table: "Pardons",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OldLogMessages_Guilds_GuildId",
                table: "OldLogMessages",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OldLogMessages_Channels_ChannelId",
                table: "OldLogMessages",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NicknameHistory_Members_UserId_GuildId",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NicknameHistory_Guilds_GuildId",
                table: "NicknameHistory",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mutes_Members_UserId_GuildId",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mutes_Guilds_GuildId",
                table: "Mutes",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessagesLogChannelOverrides_Guilds_GuildId",
                table: "MessagesLogChannelOverrides",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessagesLogChannelOverrides_Channels_ChannelId",
                table: "MessagesLogChannelOverrides",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Members_UserId_GuildId",
                table: "Messages",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Guilds_GuildId",
                table: "Messages",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Channels_ChannelId",
                table: "Messages",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageHistory_Members_GuildId_DeletedByModeratorId",
                table: "MessageHistory",
                columns: new[] { "GuildId", "ModeratorId" },
                principalTable: "Members",
                principalColumns: new[] { "GuildId", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageHistory_Guilds_GuildId",
                table: "MessageHistory",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Guilds_GuildId",
                table: "Members",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Locks_Members_ModeratorId_GuildId",
                table: "Locks",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locks_Guilds_GuildId",
                table: "Locks",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Locks_Channels_ChannelId",
                table: "Locks",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredRoles_Roles_RoleId",
                table: "IgnoredRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredRoles_Guilds_GuildId",
                table: "IgnoredRoles",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredMembers_Members_UserId_GuildId",
                table: "IgnoredMembers",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredMembers_Guilds_GuildId",
                table: "IgnoredMembers",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredChannels_Guilds_GuildId",
                table: "IgnoredChannels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredChannels_Channels_ChannelId",
                table: "IgnoredChannels",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_UserCommandChannelId",
                table: "Guilds",
                column: "UserCommandChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Guilds_GuildId",
                table: "GuildUserLogSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_UsernameChannelLogId",
                table: "GuildUserLogSettings",
                column: "UsernameChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_NicknameChannelLogId",
                table: "GuildUserLogSettings",
                column: "NicknameChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_LeaveChannelLogId",
                table: "GuildUserLogSettings",
                column: "LeaveChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_JoinChannelLogId",
                table: "GuildUserLogSettings",
                column: "JoinChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_AvatarChannelLogId",
                table: "GuildUserLogSettings",
                column: "AvatarChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildModerationSettings_Guilds_GuildId",
                table: "GuildModerationSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Guilds_GuildId",
                table: "GuildMessageLogSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_EditChannelLogId",
                table: "GuildMessageLogSettings",
                column: "EditChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_DeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "DeleteChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "BulkDeleteChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildLevelSettings_Guilds_GuildId",
                table: "GuildLevelSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildLevelSettings_Channels_LevelChannelLogId",
                table: "GuildLevelSettings",
                column: "LevelChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildCommandsSettings_Guilds_GuildId",
                table: "GuildCommandsSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Avatars_Members_UserId_GuildId",
                table: "Avatars",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
