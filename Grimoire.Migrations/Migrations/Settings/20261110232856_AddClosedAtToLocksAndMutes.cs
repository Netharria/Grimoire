using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Settings.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedAtToLocksAndMutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "Settings",
                table: "ThreadLocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "Settings",
                table: "Mutes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "Settings",
                table: "ChannelLocks",
                type: "timestamp with time zone",
                nullable: true);

            // Close every lock and mute that a later event for the same channel or member ended or replaced, so only
            // the ones still in force are open. The same rule the old "no later event" queries used (review S-13).
            migrationBuilder.Sql("""
                UPDATE "Settings"."ChannelLocks" AS e
                SET "ClosedAt" = n."NextSetAt"
                FROM (SELECT "ChannelId", "GuildId", "SetAt",
                             LEAD("SetAt") OVER (PARTITION BY "ChannelId", "GuildId" ORDER BY "SetAt") AS "NextSetAt"
                      FROM "Settings"."ChannelLocks") AS n
                WHERE e."ChannelId" = n."ChannelId" AND e."GuildId" = n."GuildId" AND e."SetAt" = n."SetAt"
                  AND e."EventType" = 'Locked' AND n."NextSetAt" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "Settings"."ThreadLocks" AS e
                SET "ClosedAt" = n."NextSetAt"
                FROM (SELECT "ChannelId", "GuildId", "SetAt",
                             LEAD("SetAt") OVER (PARTITION BY "ChannelId", "GuildId" ORDER BY "SetAt") AS "NextSetAt"
                      FROM "Settings"."ThreadLocks") AS n
                WHERE e."ChannelId" = n."ChannelId" AND e."GuildId" = n."GuildId" AND e."SetAt" = n."SetAt"
                  AND e."EventType" = 'Locked' AND n."NextSetAt" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "Settings"."Mutes" AS e
                SET "ClosedAt" = n."NextSetAt"
                FROM (SELECT "UserId", "GuildId", "SetAt",
                             LEAD("SetAt") OVER (PARTITION BY "UserId", "GuildId" ORDER BY "SetAt") AS "NextSetAt"
                      FROM "Settings"."Mutes") AS n
                WHERE e."UserId" = n."UserId" AND e."GuildId" = n."GuildId" AND e."SetAt" = n."SetAt"
                  AND e."EventType" = 'Added' AND n."NextSetAt" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks",
                column: "EndTime",
                filter: "\"EventType\" = 'Locked' AND \"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes",
                column: "EndTime",
                filter: "\"EventType\" = 'Added' AND \"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks",
                column: "EndTime",
                filter: "\"EventType\" = 'Locked' AND \"ClosedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "Settings",
                table: "ThreadLocks");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "Settings",
                table: "Mutes");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "Settings",
                table: "ChannelLocks");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks",
                column: "EndTime");
        }
    }
}
