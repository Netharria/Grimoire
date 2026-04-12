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
                name: "GuildSettings",
                schema: "Settings",
                columns: table => new
                {
                    Type = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => new { x.Type, x.GuildId, x.SetAt });
                    table.CheckConstraint("CK_GuildSettings_State_Value", "\"State\" = 2 AND \"Value\" IS NOT NULL\r\n                OR \"State\" IN (0, 1) AND \"Value\" IS NULL");
                });

            migrationBuilder.CreateTable(
                name: "IgnoredChannels",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredChannels", x => x.ChannelId);
                });

            migrationBuilder.CreateTable(
                name: "IgnoredMembers",
                schema: "Settings",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredMembers", x => new { x.UserId, x.GuildId });
                });

            migrationBuilder.CreateTable(
                name: "IgnoredRoles",
                schema: "Settings",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredRoles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "Locks",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PreviouslyAllowed = table.Column<long>(type: "bigint", nullable: false),
                    PreviouslyDenied = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locks", x => x.ChannelId);
                });

            migrationBuilder.CreateTable(
                name: "MessagesLogChannelOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelOption = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagesLogChannelOverrides", x => x.ChannelId);
                });

            migrationBuilder.CreateTable(
                name: "Mutes",
                schema: "Settings",
                columns: table => new
                {
                    SinId = table.Column<long>(type: "bigint", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutes", x => x.SinId);
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                schema: "Settings",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RewardLevel = table.Column<int>(type: "integer", nullable: false),
                    RewardMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "SpamFilterOverrides",
                schema: "Settings",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelOption = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpamFilterOverrides", x => x.ChannelId);
                });

            migrationBuilder.CreateTable(
                name: "Trackers",
                schema: "Settings",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trackers", x => new { x.UserId, x.GuildId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildSettings_GuildId_Type_SetAt",
                schema: "Settings",
                table: "GuildSettings",
                columns: new[] { "GuildId", "Type", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Locks_EndTime",
                schema: "Settings",
                table: "Locks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId",
                schema: "Settings",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId_RewardLevel",
                schema: "Settings",
                table: "Rewards",
                columns: new[] { "GuildId", "RewardLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_EndTime",
                schema: "Settings",
                table: "Trackers",
                column: "EndTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildSettings",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "IgnoredChannels",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "IgnoredMembers",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "IgnoredRoles",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "Locks",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "MessagesLogChannelOverrides",
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
                name: "Trackers",
                schema: "Settings");
        }
    }
}
