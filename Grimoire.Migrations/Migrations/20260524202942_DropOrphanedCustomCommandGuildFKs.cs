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
    public partial class DropOrphanedCustomCommandGuildFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These three FKs were created in the original CustomCommands migration but were
            // not dropped in ModelRedesign.  Guild and Role are scheduled for removal; the EF
            // model snapshot no longer references them, so they are pure database artifacts.
            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommands_Guilds_GuildId",
                table: "CustomCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommandsRole_Guilds_GuildId",
                table: "CustomCommandsRole");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommandsRole_Roles_RoleId",
                table: "CustomCommandsRole");

            // The original single-column index was superseded by the composite
            // IX_CustomCommands_GuildId_Name added in ModelRedesign.
            migrationBuilder.DropIndex(
                name: "IX_CustomCommands_GuildId",
                table: "CustomCommands");

            // Same for the role table.
            migrationBuilder.DropIndex(
                name: "IX_CustomCommandsRole_GuildId",
                table: "CustomCommandsRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CustomCommandsRole_GuildId",
                table: "CustomCommandsRole",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommands_GuildId",
                table: "CustomCommands",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommandsRole_Roles_RoleId",
                table: "CustomCommandsRole",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommandsRole_Guilds_GuildId",
                table: "CustomCommandsRole",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommands_Guilds_GuildId",
                table: "CustomCommands",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
