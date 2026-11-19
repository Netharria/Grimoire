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
    public partial class MessageLogOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_IsXpIgnored",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Members_IsXpIgnored",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Channels_IsXpIgnored",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "IsXpIgnored",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsXpIgnored",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "IsXpIgnored",
                table: "Channels");

            migrationBuilder.CreateTable(
                name: "MessagesLogChannelOverrides",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelOption = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagesLogChannelOverrides", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_MessagesLogChannelOverrides_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessagesLogChannelOverrides_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesLogChannelOverrides_GuildId",
                table: "MessagesLogChannelOverrides",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessagesLogChannelOverrides");

            migrationBuilder.AddColumn<bool>(
                name: "IsXpIgnored",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsXpIgnored",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsXpIgnored",
                table: "Channels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsXpIgnored",
                table: "Roles",
                column: "IsXpIgnored",
                filter: "\"IsXpIgnored\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Members_IsXpIgnored",
                table: "Members",
                column: "IsXpIgnored",
                filter: "\"IsXpIgnored\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_IsXpIgnored",
                table: "Channels",
                column: "IsXpIgnored",
                filter: "\"IsXpIgnored\" = TRUE");
        }
    }
}
