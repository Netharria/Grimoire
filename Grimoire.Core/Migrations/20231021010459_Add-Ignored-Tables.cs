using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIgnoredTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RewardMessage",
                table: "Rewards",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IgnoredChannels",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredChannels", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_IgnoredChannels_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IgnoredChannels_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgnoredMembers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredMembers", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_IgnoredMembers_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IgnoredMembers_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgnoredRoles",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredRoles", x => x.RoleId);
                    table.ForeignKey(
                        name: "FK_IgnoredRoles_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IgnoredRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog");

            migrationBuilder.CreateIndex(
                name: "IX_GuildModerationSettings_MuteRole",
                table: "GuildModerationSettings",
                column: "MuteRole",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredChannels_GuildId",
                table: "IgnoredChannels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_GuildId",
                table: "IgnoredMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_UserId_GuildId",
                table: "IgnoredMembers",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredRoles_GuildId",
                table: "IgnoredRoles",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildModerationSettings_Roles_MuteRole",
                table: "GuildModerationSettings",
                column: "MuteRole",
                principalTable: "Roles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog",
                principalTable: "Channels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildModerationSettings_Roles_MuteRole",
                table: "GuildModerationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropTable(
                name: "IgnoredChannels");

            migrationBuilder.DropTable(
                name: "IgnoredMembers");

            migrationBuilder.DropTable(
                name: "IgnoredRoles");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_GuildModerationSettings_MuteRole",
                table: "GuildModerationSettings");

            migrationBuilder.DropColumn(
                name: "RewardMessage",
                table: "Rewards");
        }
    }
}
