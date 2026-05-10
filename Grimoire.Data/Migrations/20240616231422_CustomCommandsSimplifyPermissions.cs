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
    public partial class CustomCommandsSimplifyPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandAllowed",
                table: "CustomCommandsRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CommandAllowed",
                table: "CustomCommandsRole",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
