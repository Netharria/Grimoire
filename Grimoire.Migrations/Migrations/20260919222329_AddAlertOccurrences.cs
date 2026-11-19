using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Grimoire.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertOccurrences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AlertType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SampleMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    GuildIds = table.Column<long[]>(type: "bigint[]", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertOccurrences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertOccurrences_AlertKey",
                table: "AlertOccurrences",
                column: "AlertKey",
                unique: true,
                filter: "\"ReportedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AlertOccurrences_LastSeen",
                table: "AlertOccurrences",
                column: "LastSeen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertOccurrences");
        }
    }
}
