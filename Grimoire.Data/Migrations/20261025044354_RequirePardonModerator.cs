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
    public partial class RequirePardonModerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The model has required a moderator on every pardon since ModelRedesign, but the column was left
            // nullable from 2023. Only /Pardon creates pardons, so a pardon always has a moderator. Older rows
            // without one cannot be repaired automatically, so stop with a clear message instead of deleting
            // pardons (which would un-pardon sins) or inventing a moderator.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE missing bigint;
                BEGIN
                    SELECT count(*) INTO missing FROM "Pardons" WHERE "ModeratorId" IS NULL;
                    IF missing > 0 THEN
                        RAISE EXCEPTION 'Pardons has % row(s) with no ModeratorId. Assign a moderator or delete them, then rerun this migration.', missing;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Pardons",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Pardons",
                type: "numeric(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");
        }
    }
}
