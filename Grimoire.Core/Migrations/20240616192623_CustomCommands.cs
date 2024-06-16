// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Core.Migrations;

/// <inheritdoc />
public partial class CustomCommands : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:fuzzystrmatch", ",,");

        migrationBuilder.CreateTable(
            name: "CustomCommands",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                HasMention = table.Column<bool>(type: "boolean", nullable: false),
                HasMessage = table.Column<bool>(type: "boolean", nullable: false),
                IsEmbedded = table.Column<bool>(type: "boolean", nullable: false),
                EmbedColor = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                RestrictedUse = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomCommands", x => new { x.Name, x.GuildId });
                table.ForeignKey(
                    name: "FK_CustomCommands_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GuildCommandsSettings",
            columns: table => new
            {
                GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuildCommandsSettings", x => x.GuildId);
                table.ForeignKey(
                    name: "FK_GuildCommandsSettings_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CustomCommandsRole",
            columns: table => new
            {
                RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                CustomCommandName = table.Column<string>(type: "character varying(24)", nullable: false),
                GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                CommandAllowed = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomCommandsRole", x => new { x.CustomCommandName, x.GuildId, x.RoleId });
                table.ForeignKey(
                    name: "FK_CustomCommandsRole_CustomCommands_CustomCommandName_GuildId",
                    columns: x => new { x.CustomCommandName, x.GuildId },
                    principalTable: "CustomCommands",
                    principalColumns: new[] { "Name", "GuildId" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CustomCommandsRole_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CustomCommandsRole_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomCommands_GuildId",
            table: "CustomCommands",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomCommandsRole_GuildId",
            table: "CustomCommandsRole",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomCommandsRole_RoleId",
            table: "CustomCommandsRole",
            column: "RoleId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CustomCommandsRole");

        migrationBuilder.DropTable(
            name: "GuildCommandsSettings");

        migrationBuilder.DropTable(
            name: "CustomCommands");

        migrationBuilder.AlterDatabase()
            .OldAnnotation("Npgsql:PostgresExtension:fuzzystrmatch", ",,");
    }
}
