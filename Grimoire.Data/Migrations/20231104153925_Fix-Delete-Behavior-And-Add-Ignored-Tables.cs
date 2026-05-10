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
    public sealed partial class FixDeleteBehaviorAndAddIgnoredTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reaction_Members_UserId_GuildId",
                table: "Reaction");

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Trackers",
                type: "numeric(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AddColumn<string>(
                name: "RewardMessage",
                table: "Rewards",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Pardons",
                type: "numeric(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Locks",
                type: "numeric(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.CreateTable(
                name: "IgnoredChannels",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredChannels", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_IgnoredChannels_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IgnoredChannels_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgnoredMembers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredMembers", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_IgnoredMembers_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IgnoredMembers_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgnoredRoles",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredRoles", x => x.RoleId);
                    table.ForeignKey(
                        name: "FK_IgnoredRoles_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IgnoredRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog");

            migrationBuilder.CreateIndex(
                name: "IX_GuildModerationSettings_MuteRole",
                table: "GuildModerationSettings",
                column: "MuteRole",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredChannels_GuildId",
                table: "IgnoredChannels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_GuildId",
                table: "IgnoredMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_UserId_GuildId",
                table: "IgnoredMembers",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredRoles_GuildId",
                table: "IgnoredRoles",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildModerationSettings_Roles_MuteRole",
                table: "GuildModerationSettings",
                column: "MuteRole",
                principalTable: "Roles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reaction_Members_UserId_GuildId",
                table: "Reaction",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildModerationSettings_Roles_MuteRole",
                table: "GuildModerationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropForeignKey(
                name: "FK_Reaction_Members_UserId_GuildId",
                table: "Reaction");

            migrationBuilder.DropTable(
                name: "IgnoredChannels");

            migrationBuilder.DropTable(
                name: "IgnoredMembers");

            migrationBuilder.DropTable(
                name: "IgnoredRoles");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_GuildModerationSettings_MuteRole",
                table: "GuildModerationSettings");

            migrationBuilder.DropColumn(
                name: "RewardMessage",
                table: "Rewards");

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Trackers",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Pardons",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Locks",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reaction_Members_UserId_GuildId",
                table: "Reaction",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
