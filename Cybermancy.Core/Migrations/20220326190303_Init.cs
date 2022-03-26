using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cybermancy.Core.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ModChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GuildModerationSettings",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PublicBanLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    DurationType = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    Duration = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    MuteRole = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    IsModerationEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildModerationSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildModerationSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GuildUsers",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Xp = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 0ul),
                    TimeOut = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildUsers", x => new { x.UserId, x.GuildId });
                    table.UniqueConstraint("AK_GuildUsers_GuildId_UserId", x => new { x.GuildId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GuildUsers_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UsernameHistory",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    NewUsername = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsernameHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsernameHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GuildLevelSettings",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsLevelingEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    TextTime = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 3u),
                    Base = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 15u),
                    Modifier = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 50u),
                    Amount = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 5u),
                    LevelChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    LevelChannelLogsId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildLevelSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildLevelSettings_Channels_LevelChannelLogsId",
                        column: x => x.LevelChannelLogsId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLevelSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GuildLogSettings",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    JoinChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    LeaveChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    DeleteChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    BulkDeleteChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    EditChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    UsernameChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    NicknameChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    AvatarChannelLogId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    IsLoggingEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildLogSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_AvatarChannelLogId",
                        column: x => x.AvatarChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_BulkDeleteChannelLogId",
                        column: x => x.BulkDeleteChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_DeleteChannelLogId",
                        column: x => x.DeleteChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_EditChannelLogId",
                        column: x => x.EditChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_JoinChannelLogId",
                        column: x => x.JoinChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_LeaveChannelLogId",
                        column: x => x.LeaveChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_NicknameChannelLogId",
                        column: x => x.NicknameChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Channels_UsernameChannelLogId",
                        column: x => x.UsernameChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OldLogMessages",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OldLogMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OldLogMessages_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OldLogMessages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    RoleId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    RewardLevel = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.RoleId);
                    table.ForeignKey(
                        name: "FK_Rewards_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rewards_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Locks",
                columns: table => new
                {
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PreviousSetting = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    ModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locks", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_Locks_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Locks_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Locks_GuildUsers_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    AuthorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReferencedMessageId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_GuildUsers_AuthorId_GuildId",
                        columns: x => new { x.AuthorId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NicknameHistory",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Nickname = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NicknameHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NicknameHistory_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NicknameHistory_GuildUsers_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Sins",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InfractionOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SinType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sins_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sins_GuildUsers_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sins_GuildUsers_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Trackers",
                columns: table => new
                {
                    GuildUserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    LogChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trackers", x => new { x.GuildUserId, x.GuildId });
                    table.ForeignKey(
                        name: "FK_Trackers_Channels_LogChannelId",
                        column: x => x.LogChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trackers_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trackers_GuildUsers_GuildUserId_GuildId",
                        columns: x => new { x.GuildUserId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trackers_GuildUsers_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => new { x.MessageId, x.AttachmentUrl });
                    table.ForeignKey(
                        name: "FK_Attachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MessageHistory",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    MessageContent = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeletedByModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageHistory_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageHistory_GuildUsers_GuildId_DeletedByModeratorId",
                        columns: x => new { x.GuildId, x.DeletedByModeratorId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "GuildId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageHistory_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Reaction",
                columns: table => new
                {
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EmojiId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildUserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reaction", x => new { x.MessageId, x.EmojiId });
                    table.ForeignKey(
                        name: "FK_Reaction_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reaction_GuildUsers_GuildUserId_GuildId",
                        columns: x => new { x.GuildUserId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reaction_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Mutes",
                columns: table => new
                {
                    SinId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutes", x => x.SinId);
                    table.ForeignKey(
                        name: "FK_Mutes_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mutes_GuildUsers_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mutes_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Pardons",
                columns: table => new
                {
                    SinId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PardonDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pardons", x => x.SinId);
                    table.ForeignKey(
                        name: "FK_Pardons_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pardons_GuildUsers_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "GuildUsers",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pardons_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PublishedMessages",
                columns: table => new
                {
                    SinId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PublishType = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishedMessages", x => new { x.SinId, x.PublishType });
                    table.ForeignKey(
                        name: "FK_PublishedMessages_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildLevelSettings_LevelChannelLogsId",
                table: "GuildLevelSettings",
                column: "LevelChannelLogsId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_AvatarChannelLogId",
                table: "GuildLogSettings",
                column: "AvatarChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_BulkDeleteChannelLogId",
                table: "GuildLogSettings",
                column: "BulkDeleteChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_DeleteChannelLogId",
                table: "GuildLogSettings",
                column: "DeleteChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_EditChannelLogId",
                table: "GuildLogSettings",
                column: "EditChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_JoinChannelLogId",
                table: "GuildLogSettings",
                column: "JoinChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_LeaveChannelLogId",
                table: "GuildLogSettings",
                column: "LeaveChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_NicknameChannelLogId",
                table: "GuildLogSettings",
                column: "NicknameChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildLogSettings_UsernameChannelLogId",
                table: "GuildLogSettings",
                column: "UsernameChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locks_GuildId",
                table: "Locks",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Locks_ModeratorId_GuildId",
                table: "Locks",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_GuildId_DeletedByModeratorId",
                table: "MessageHistory",
                columns: new[] { "GuildId", "DeletedByModeratorId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_MessageId",
                table: "MessageHistory",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AuthorId_GuildId",
                table: "Messages",
                columns: new[] { "AuthorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChannelId",
                table: "Messages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GuildId",
                table: "Messages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_GuildId",
                table: "Mutes",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_GuildId",
                table: "NicknameHistory",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_UserId_GuildId",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_ChannelId",
                table: "OldLogMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_GuildId",
                table: "OldLogMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_GuildId",
                table: "Pardons",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_ModeratorId_GuildId",
                table: "Pardons",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_GuildId",
                table: "Reaction",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_GuildUserId_GuildId",
                table: "Reaction",
                columns: new[] { "GuildUserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId",
                table: "Rewards",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_GuildId",
                table: "Roles",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sins_GuildId",
                table: "Sins",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sins_ModeratorId_GuildId",
                table: "Sins",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sins_UserId_GuildId",
                table: "Sins",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_GuildId",
                table: "Trackers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_LogChannelId",
                table: "Trackers",
                column: "LogChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_ModeratorId_GuildId",
                table: "Trackers",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistory_UserId",
                table: "UsernameHistory",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "GuildLevelSettings");

            migrationBuilder.DropTable(
                name: "GuildLogSettings");

            migrationBuilder.DropTable(
                name: "GuildModerationSettings");

            migrationBuilder.DropTable(
                name: "Locks");

            migrationBuilder.DropTable(
                name: "MessageHistory");

            migrationBuilder.DropTable(
                name: "Mutes");

            migrationBuilder.DropTable(
                name: "NicknameHistory");

            migrationBuilder.DropTable(
                name: "OldLogMessages");

            migrationBuilder.DropTable(
                name: "Pardons");

            migrationBuilder.DropTable(
                name: "PublishedMessages");

            migrationBuilder.DropTable(
                name: "Reaction");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropTable(
                name: "Trackers");

            migrationBuilder.DropTable(
                name: "UsernameHistory");

            migrationBuilder.DropTable(
                name: "Sins");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "GuildUsers");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
