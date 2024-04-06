// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Core.Migrations
{
    /// <inheritdoc />
    public sealed partial class AddIndexesToHistoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_XpHistory_UserId_GuildId",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_GuildId",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_NicknameHistory_UserId_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropIndex(
                name: "IX_MessageHistory_MessageId",
                table: "MessageHistory");

            migrationBuilder.DropIndex(
                name: "IX_Avatars_UserId_GuildId",
                table: "Avatars");

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_UserId_GuildId_TimeOut",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId", "TimeOut" },
                descending: [false, false, true])
                .Annotation("Npgsql:IndexInclude", new[] { "Xp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistory_UserId_Timestamp",
                table: "UsernameHistory",
                columns: new[] { "UserId", "Timestamp" },
                descending: [false, true]);

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_EndTime",
                table: "Trackers",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsXpIgnored",
                table: "Roles",
                column: "IsXpIgnored",
                filter: "\"IsXpIgnored\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId_RewardLevel",
                table: "Rewards",
                columns: new[] { "GuildId", "RewardLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_CreatedAt",
                table: "OldLogMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_UserId_GuildId_Timestamp",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId", "Timestamp" },
                descending: [false, false, true]);

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                table: "Mutes",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CreatedTimestamp",
                table: "Messages",
                column: "CreatedTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_MessageId_TimeStamp_Action",
                table: "MessageHistory",
                columns: new[] { "MessageId", "TimeStamp", "Action" },
                descending: [false, true, false]);

            migrationBuilder.CreateIndex(
                name: "IX_Members_IsXpIgnored",
                table: "Members",
                column: "IsXpIgnored",
                filter: "\"IsXpIgnored\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Locks_EndTime",
                table: "Locks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_IsXpIgnored",
                table: "Channels",
                column: "IsXpIgnored",
                filter: "\"IsXpIgnored\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Avatars_UserId_GuildId_Timestamp",
                table: "Avatars",
                columns: new[] { "UserId", "GuildId", "Timestamp" },
                descending: [false, false, true]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_XpHistory_UserId_GuildId_TimeOut",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_UsernameHistory_UserId_Timestamp",
                table: "UsernameHistory");

            migrationBuilder.DropIndex(
                name: "IX_Trackers_EndTime",
                table: "Trackers");

            migrationBuilder.DropIndex(
                name: "IX_Roles_IsXpIgnored",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_GuildId_RewardLevel",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_OldLogMessages_CreatedAt",
                table: "OldLogMessages");

            migrationBuilder.DropIndex(
                name: "IX_NicknameHistory_UserId_GuildId_Timestamp",
                table: "NicknameHistory");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_EndTime",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_Messages_CreatedTimestamp",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_MessageHistory_MessageId_TimeStamp_Action",
                table: "MessageHistory");

            migrationBuilder.DropIndex(
                name: "IX_Members_IsXpIgnored",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Locks_EndTime",
                table: "Locks");

            migrationBuilder.DropIndex(
                name: "IX_Channels_IsXpIgnored",
                table: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_Avatars_UserId_GuildId_Timestamp",
                table: "Avatars");

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_UserId_GuildId",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId",
                table: "Rewards",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_UserId_GuildId",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_MessageId",
                table: "MessageHistory",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Avatars_UserId_GuildId",
                table: "Avatars",
                columns: new[] { "UserId", "GuildId" });
        }
    }
}
