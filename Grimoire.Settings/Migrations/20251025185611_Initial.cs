using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "CustomCommandsSettings",
                schema: "Settings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomCommandsSettings", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                schema: "Settings",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ModLogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserCommandChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => x.Id);
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
                name: "LevelingSettings",
                schema: "Settings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TextTime = table.Column<TimeSpan>(type: "interval", nullable: false, defaultValue: new TimeSpan(0, 0, 3, 0, 0)),
                    Base = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    Modifier = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    Amount = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    LevelChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelingSettings", x => x.GuildId);
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
                name: "MessageLogSettings",
                schema: "Settings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DeleteChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BulkDeleteChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    EditChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogSettings", x => x.GuildId);
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
                name: "ModerationSettings",
                schema: "Settings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PublicBanLog = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AutoPardonAfter = table.Column<TimeSpan>(type: "interval", nullable: false, defaultValue: new TimeSpan(10950, 0, 0, 0, 0)),
                    MuteRole = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AntiSpamEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationSettings", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "Mutes",
                schema: "Settings",
                columns: table => new
                {
                    SinId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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

            migrationBuilder.CreateTable(
                name: "UserLogSettings",
                schema: "Settings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    JoinChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LeaveChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UsernameChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    NicknameChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AvatarChannelLogId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogSettings", x => x.GuildId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locks_EndTime",
                schema: "Settings",
                table: "Locks",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_EndTime",
                schema: "Settings",
                table: "Mutes",
                column: "EndTime");

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
                name: "CustomCommandsSettings",
                schema: "Settings");

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
                name: "LevelingSettings",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "Locks",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "MessageLogSettings",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "MessagesLogChannelOverrides",
                schema: "Settings");

            migrationBuilder.DropTable(
                name: "ModerationSettings",
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
                name: "UserLogSettings",
                schema: "Settings");
        }
    }
}
