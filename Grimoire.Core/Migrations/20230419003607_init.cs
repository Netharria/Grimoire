using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Grimoire.Core.Migrations
{
    public partial class init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ModChannelLog = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "GuildModerationSettings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PublicBanLog = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DurationType = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Duration = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    MuteRole = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ModuleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => new { x.UserId, x.GuildId });
                    table.UniqueConstraint("AK_Members_GuildId_UserId", x => new { x.GuildId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Members_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Members_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsernameHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                });

            migrationBuilder.CreateTable(
                name: "GuildLevelSettings",
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
                    table.PrimaryKey("PK_GuildLevelSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildLevelSettings_Channels_LevelChannelLogId",
                        column: x => x.LevelChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GuildLevelSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMessageLogSettings",
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
                    table.PrimaryKey("PK_GuildMessageLogSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildMessageLogSettings_Channels_BulkDeleteChannelLogId",
                        column: x => x.BulkDeleteChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildMessageLogSettings_Channels_DeleteChannelLogId",
                        column: x => x.DeleteChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildMessageLogSettings_Channels_EditChannelLogId",
                        column: x => x.EditChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildMessageLogSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildUserLogSettings",
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
                    table.PrimaryKey("PK_GuildUserLogSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildUserLogSettings_Channels_AvatarChannelLogId",
                        column: x => x.AvatarChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildUserLogSettings_Channels_JoinChannelLogId",
                        column: x => x.JoinChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildUserLogSettings_Channels_LeaveChannelLogId",
                        column: x => x.LeaveChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildUserLogSettings_Channels_NicknameChannelLogId",
                        column: x => x.NicknameChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildUserLogSettings_Channels_UsernameChannelLogId",
                        column: x => x.UsernameChannelLogId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuildUserLogSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OldLogMessages",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RewardLevel = table.Column<int>(type: "integer", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Locks",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PreviouslyAllowed = table.Column<long>(type: "bigint", nullable: false),
                    PreviouslyDenied = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                        name: "FK_Locks_Members_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ReferencedMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
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
                        name: "FK_Messages_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NicknameHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
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
                        name: "FK_NicknameHistory_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sins",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    InfractionOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    SinType = table.Column<int>(type: "integer", nullable: false)
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
                        name: "FK_Sins_Members_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sins_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trackers",
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
                        name: "FK_Trackers_Members_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trackers_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false),
                    TimeOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AwarderId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpHistory_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_XpHistory_Members_AwarderId_GuildId",
                        columns: x => new { x.AwarderId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_XpHistory_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    MessageContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DeletedByModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    TimeStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                        name: "FK_MessageHistory_Members_GuildId_DeletedByModeratorId",
                        columns: x => new { x.GuildId, x.DeletedByModeratorId },
                        principalTable: "Members",
                        principalColumns: new[] { "GuildId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageHistory_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reaction",
                columns: table => new
                {
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmojiId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
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
                        name: "FK_Reaction_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reaction_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mutes",
                columns: table => new
                {
                    SinId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                        name: "FK_Mutes_Members_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mutes_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pardons",
                columns: table => new
                {
                    SinId = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PardonDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
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
                        name: "FK_Pardons_Members_ModeratorId_GuildId",
                        columns: x => new { x.ModeratorId, x.GuildId },
                        principalTable: "Members",
                        principalColumns: new[] { "UserId", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pardons_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublishedMessages",
                columns: table => new
                {
                    SinId = table.Column<long>(type: "bigint", nullable: false),
                    PublishType = table.Column<int>(type: "integer", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_MessageId",
                table: "Attachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildLevelSettings_LevelChannelLogId",
                table: "GuildLevelSettings",
                column: "LevelChannelLogId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMessageLogSettings_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "BulkDeleteChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMessageLogSettings_DeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "DeleteChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMessageLogSettings_EditChannelLogId",
                table: "GuildMessageLogSettings",
                column: "EditChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_AvatarChannelLogId",
                table: "GuildUserLogSettings",
                column: "AvatarChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_JoinChannelLogId",
                table: "GuildUserLogSettings",
                column: "JoinChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_LeaveChannelLogId",
                table: "GuildUserLogSettings",
                column: "LeaveChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_NicknameChannelLogId",
                table: "GuildUserLogSettings",
                column: "NicknameChannelLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserLogSettings_UsernameChannelLogId",
                table: "GuildUserLogSettings",
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
                name: "IX_Messages_ChannelId",
                table: "Messages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GuildId",
                table: "Messages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId_GuildId",
                table: "Messages",
                columns: new[] { "UserId", "GuildId" });

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
                name: "IX_Reaction_UserId_GuildId",
                table: "Reaction",
                columns: new[] { "UserId", "GuildId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_AwarderId_GuildId",
                table: "XpHistory",
                columns: new[] { "AwarderId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_GuildId",
                table: "XpHistory",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_UserId_GuildId",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "GuildLevelSettings");

            migrationBuilder.DropTable(
                name: "GuildMessageLogSettings");

            migrationBuilder.DropTable(
                name: "GuildModerationSettings");

            migrationBuilder.DropTable(
                name: "GuildUserLogSettings");

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
                name: "XpHistory");

            migrationBuilder.DropTable(
                name: "Sins");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
