using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Settings.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Settings");

            migrationBuilder.CreateTable(
                name: "ChannelLocks",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EventType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    PreviouslyAllowed = table.Column<long>(type: "bigint", nullable: true),
                    PreviouslyDenied = table.Column<long>(type: "bigint", nullable: true),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelLocks", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                schema: "Settings",
                columns: table => new
                {
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    State = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => new { x.Type, x.GuildId, x.SetAt });
                    table.CheckConstraint("CK_GuildSettings_State_Value", "(\"State\" = 'CustomValue' AND \"Value\" IS NOT NULL)\r\n                OR (\"State\" IN ('Default', 'Disabled') AND \"Value\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "MessageLogChannelOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogChannelOverrides", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "Mutes",
                schema: "Settings",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EventType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SinId = table.Column<long>(type: "bigint", nullable: true),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutes", x => new { x.UserId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                schema: "Settings",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RewardType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RewardLevel = table.Column<int>(type: "integer", nullable: true),
                    RewardMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => new { x.GuildId, x.RoleId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "SpamFilterOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpamFilterOverrides", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "ThreadLocks",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EventType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadLocks", x => new { x.ChannelId, x.GuildId, x.SetAt });
                });

            migrationBuilder.CreateTable(
                name: "XpTrackedItems",
                schema: "Settings",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Type = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpTrackedItems", x => new { x.GuildId, x.Id, x.SetAt });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ChannelLocks",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_EndTime",
                schema: "Settings",
                table: "ChannelLocks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_GuildSettings_GuildId_Type_SetAt",
                schema: "Settings",
                table: "GuildSettings",
                columns: new[] { "GuildId", "Type", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogChannelOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "MessageLogChannelOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogChannelOverrides_GuildId",
                schema: "Settings",
                table: "MessageLogChannelOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId_SetAt",
                schema: "Settings",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId_RoleId_SetAt",
                schema: "Settings",
                table: "Rewards",
                columns: new[] { "GuildId", "RoleId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "SpamFilterOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_GuildId",
                schema: "Settings",
                table: "SpamFilterOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ThreadLocks",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_EndTime",
                schema: "Settings",
                table: "ThreadLocks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_XpTrackedItems_GuildId_Id_SetAt",
                schema: "Settings",
                table: "XpTrackedItems",
                columns: new[] { "GuildId", "Id", "SetAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelLocks",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "GuildSettings",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "MessageLogChannelOverrides",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "Mutes",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "Rewards",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "SpamFilterOverrides",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "ThreadLocks",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "XpTrackedItems",
                schema: "Settings");
        }
    }
}
