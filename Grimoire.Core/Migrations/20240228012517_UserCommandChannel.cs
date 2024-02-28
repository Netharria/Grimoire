using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Core.Migrations
{
    /// <inheritdoc />
    public partial class UserCommandChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UserCommandChannelId",
                table: "Guilds",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_UserCommandChannelId",
                table: "Guilds",
                column: "UserCommandChannelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_UserCommandChannelId",
                table: "Guilds",
                column: "UserCommandChannelId",
                principalTable: "Channels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_UserCommandChannelId",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_UserCommandChannelId",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "UserCommandChannelId",
                table: "Guilds");
        }
    }
}
