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
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChannelOption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagesLogChannelOverrides", x => new { x.ChannelId, x.GuildId, x.SetAt });
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
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RewardLevel = table.Column<int>(type: "integer", nullable: false),
                    RewardMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "XpIgnoredItems",
                schema: "Settings",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpIgnoredItems", x => new { x.GuildId, x.Id, x.SetAt });
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
                name: "IX_MessagesLogChannelOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "MessagesLogChannelOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesLogChannelOverrides_GuildId",
                schema: "Settings",
                table: "MessagesLogChannelOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId",
                schema: "Settings",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

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
                name: "IX_Trackers_EndTime",
                schema: "Settings",
                table: "Trackers",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_XpIgnoredItems_GuildId_Id_SetAt",
                schema: "Settings",
                table: "XpIgnoredItems",
                columns: new[] { "GuildId", "Id", "SetAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildSettings",
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

            migrationBuilder.DropTable(
                name: "XpIgnoredItems",
                schema: "Settings");
        }
    }
}
