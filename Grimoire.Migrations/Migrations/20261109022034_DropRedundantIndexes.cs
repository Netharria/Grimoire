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
    public partial class DropRedundantIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_XpHistory_GuildId_Xp",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_Sin_Id_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_SinReasonHistory_SinId_SetAt",
                table: "SinReasonHistory");

            migrationBuilder.DropIndex(
                name: "IX_Pardons_SinId_SetAt",
                table: "Pardons");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommandUsages_GuildId_Name_UserId",
                table: "CustomCommandUsages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_GuildId_Xp",
                table: "XpHistory",
                columns: new[] { "GuildId", "Xp" });

            migrationBuilder.CreateIndex(
                name: "IX_Sin_Id_GuildId",
                table: "Sins",
                columns: new[] { "Id", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_SinReasonHistory_SinId_SetAt",
                table: "SinReasonHistory",
                columns: new[] { "SinId", "SetAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_SinId_SetAt",
                table: "Pardons",
                columns: new[] { "SinId", "SetAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommandUsages_GuildId_Name_UserId",
                table: "CustomCommandUsages",
                columns: new[] { "GuildId", "Name", "UserId" });
        }
    }
}
