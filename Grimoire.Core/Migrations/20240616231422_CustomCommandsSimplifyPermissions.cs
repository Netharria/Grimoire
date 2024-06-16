using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Core.Migrations
{
    /// <inheritdoc />
    public partial class CustomCommandsSimplifyPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandAllowed",
                table: "CustomCommandsRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CommandAllowed",
                table: "CustomCommandsRole",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
