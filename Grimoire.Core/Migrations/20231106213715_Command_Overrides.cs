using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Grimoire.Core.Migrations
{
    /// <inheritdoc />
    public partial class Command_Overrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberCommandOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CommandPermissions = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCommandOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberCommandOverrides_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MemberCommandOverrides_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberCommandOverrides_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleCommandOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CommandPermissions = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleCommandOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleCommandOverrides_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoleCommandOverrides_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleCommandOverrides_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberCommandOverrides_ChannelId",
                table: "MemberCommandOverrides",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCommandOverrides_GuildId",
                table: "MemberCommandOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCommandOverrides_UserId_GuildId_ChannelId",
                table: "MemberCommandOverrides",
                columns: new[] { "UserId", "GuildId", "ChannelId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_RoleCommandOverrides_ChannelId",
                table: "RoleCommandOverrides",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleCommandOverrides_GuildId",
                table: "RoleCommandOverrides",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleCommandOverrides_RoleId_GuildId_ChannelId",
                table: "RoleCommandOverrides",
                columns: new[] { "RoleId", "GuildId", "ChannelId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberCommandOverrides");

            migrationBuilder.DropTable(
                name: "RoleCommandOverrides");
        }
    }
}
