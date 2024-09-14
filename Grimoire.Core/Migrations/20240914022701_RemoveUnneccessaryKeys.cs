using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Grimoire.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnneccessaryKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM public."UsernameHistory" a
                USING public."UsernameHistory" b
                Where
                	a."UserId" = b."UserId"
                	AND a."Timestamp" = b."Timestamp"
                	AND a."Id" > b."Id"
                """);

            migrationBuilder.Sql("""
                DELETE FROM public."NicknameHistory" a
                USING public."NicknameHistory" b
                Where
                	a."UserId" = b."UserId"
                	AND a."GuildId" = b."GuildId"
                	AND a."Timestamp" = b."Timestamp"
                	AND a."Id" > b."Id"
                """);

            migrationBuilder.Sql("""
                DELETE FROM public."Avatars" a
                USING public."Avatars" b
                Where
                	a."UserId" = b."UserId"
                	AND a."GuildId" = b."GuildId"
                	AND a."Timestamp" = b."Timestamp"
                	AND a."Id" > b."Id"
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_XpHistory",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_XpHistory_UserId_GuildId_TimeOut",
                table: "XpHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsernameHistory",
                table: "UsernameHistory");

            migrationBuilder.DropIndex(
                name: "IX_UsernameHistory_UserId",
                table: "UsernameHistory");

            migrationBuilder.DropIndex(
                name: "IX_UsernameHistory_UserId_Timestamp",
                table: "UsernameHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NicknameHistory",
                table: "NicknameHistory");

            migrationBuilder.DropIndex(
                name: "IX_NicknameHistory_UserId_GuildId_Timestamp",
                table: "NicknameHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MessageHistory",
                table: "MessageHistory");

            migrationBuilder.DropIndex(
                name: "IX_MessageHistory_MessageId_TimeStamp_Action",
                table: "MessageHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IgnoredMembers",
                table: "IgnoredMembers");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredMembers_UserId_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Avatars",
                table: "Avatars");

            migrationBuilder.DropIndex(
                name: "IX_Avatars_UserId_GuildId_Timestamp",
                table: "Avatars");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "XpHistory");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UsernameHistory");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "NicknameHistory");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "MessageHistory");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Avatars");

            migrationBuilder.AddColumn<bool>(
                name: "AntiSpamEnabled",
                table: "GuildModerationSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_XpHistory",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId", "TimeOut" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsernameHistory",
                table: "UsernameHistory",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_NicknameHistory",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId", "Timestamp" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageHistory",
                table: "MessageHistory",
                columns: new[] { "MessageId", "TimeStamp" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_IgnoredMembers",
                table: "IgnoredMembers",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Avatars",
                table: "Avatars",
                columns: new[] { "UserId", "GuildId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_XpHistory",
                table: "XpHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsernameHistory",
                table: "UsernameHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NicknameHistory",
                table: "NicknameHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MessageHistory",
                table: "MessageHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IgnoredMembers",
                table: "IgnoredMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Avatars",
                table: "Avatars");

            migrationBuilder.DropColumn(
                name: "AntiSpamEnabled",
                table: "GuildModerationSettings");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "XpHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "UsernameHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "NicknameHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "MessageHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "Avatars",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_XpHistory",
                table: "XpHistory",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsernameHistory",
                table: "UsernameHistory",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NicknameHistory",
                table: "NicknameHistory",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageHistory",
                table: "MessageHistory",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IgnoredMembers",
                table: "IgnoredMembers",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Avatars",
                table: "Avatars",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_UserId_GuildId_TimeOut",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId", "TimeOut" },
                descending: new[] { false, false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "Xp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistory_UserId",
                table: "UsernameHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistory_UserId_Timestamp",
                table: "UsernameHistory",
                columns: new[] { "UserId", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_UserId_GuildId_Timestamp",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId", "Timestamp" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_MessageId_TimeStamp_Action",
                table: "MessageHistory",
                columns: new[] { "MessageId", "TimeStamp", "Action" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_UserId_GuildId",
                table: "IgnoredMembers",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Avatars_UserId_GuildId_Timestamp",
                table: "Avatars",
                columns: new[] { "UserId", "GuildId", "Timestamp" },
                descending: new[] { false, false, true });
        }
    }
}
