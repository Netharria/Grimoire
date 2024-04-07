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
    public partial class ProxyMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProxiedMessages",
                columns: table => new
                {
                    ProxyMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OriginalMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SystemId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MemberId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxiedMessages", x => new { x.ProxyMessageId, x.OriginalMessageId });
                    table.ForeignKey(
                        name: "FK_ProxiedMessages_Messages_OriginalMessageId",
                        column: x => x.OriginalMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProxiedMessages_Messages_ProxyMessageId",
                        column: x => x.ProxyMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProxiedMessages_OriginalMessageId",
                table: "ProxiedMessages",
                column: "OriginalMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProxiedMessages_ProxyMessageId",
                table: "ProxiedMessages",
                column: "ProxyMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProxiedMessages");
        }
    }
}
