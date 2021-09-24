// -----------------------------------------------------------------------
// <copyright file="20210914022641_Init.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Migrations;

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
                    ModChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
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
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    UserName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AvatarUrl = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
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
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsXpIgnored = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
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
                name: "GuildLevelSettings",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsLevelingEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    TextTime = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    Base = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    Modifier = table.Column<int>(type: "int", nullable: false, defaultValue: 50),
                    Amount = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    LevelChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildLevelSettings", x => x.GuildId);
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
                    JoinChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    LeaveChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    DeleteChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    BulkDeleteChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    EditChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    UsernameChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    NicknameChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    AvatarChannelLog = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildLogSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildLogSettings_Guilds_GuildId",
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
                name: "OldLogMessages",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OldLogMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OldLogMessages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Role_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GuildUser",
                columns: table => new
                {
                    GuildsId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    UsersId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildUser", x => new { x.GuildsId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_GuildUser_Guilds_GuildsId",
                        column: x => x.GuildsId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildUser_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
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
                    Reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InfractionOn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SinType = table.Column<int>(type: "int", nullable: false),
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
                        name: "FK_Sins_Users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserLevels",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Xp = table.Column<int>(type: "int", nullable: false),
                    TimeOut = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsXpIgnored = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLevels_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLevels_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
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
                    reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                        name: "FK_Locks_Users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Content = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                        name: "FK_Messages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Trackers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    LogChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trackers", x => x.Id);
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
                        name: "FK_Trackers_Users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trackers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
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
                    RewardLevel = table.Column<int>(type: "int", nullable: false),
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
                        name: "FK_Rewards_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Mutes",
                columns: table => new
                {
                    SinId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
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
                        name: "FK_Mutes_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mutes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Pardons",
                columns: table => new
                {
                    SinId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ModeratorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PardonDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pardons", x => x.SinId);
                    table.ForeignKey(
                        name: "FK_Pardons_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pardons_Users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PublishedMessages",
                columns: table => new
                {
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    SinId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PublishType = table.Column<int>(type: "int", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishedMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_PublishedMessages_Sins_SinId",
                        column: x => x.SinId,
                        principalTable: "Sins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Attachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildUser_UsersId",
                table: "GuildUser",
                column: "UsersId");

            migrationBuilder.CreateIndex(
                name: "IX_Locks_GuildId",
                table: "Locks",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Locks_ModeratorId",
                table: "Locks",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChannelId",
                table: "Messages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GuildId",
                table: "Messages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId",
                table: "Messages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_GuildId",
                table: "Mutes",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId",
                table: "Mutes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_GuildId",
                table: "OldLogMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_ModeratorId",
                table: "Pardons",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_PublishedMessages_SinId_PublishType",
                table: "PublishedMessages",
                columns: new[] { "SinId", "PublishType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId",
                table: "Rewards",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Role_GuildId",
                table: "Role",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sins_GuildId",
                table: "Sins",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sins_ModeratorId",
                table: "Sins",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sins_UserId",
                table: "Sins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_GuildId",
                table: "Trackers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_LogChannelId",
                table: "Trackers",
                column: "LogChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_ModeratorId",
                table: "Trackers",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackers_UserId_GuildId",
                table: "Trackers",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_GuildId_UserId",
                table: "UserLevels",
                columns: new[] { "GuildId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_UserId",
                table: "UserLevels",
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
                name: "GuildUser");

            migrationBuilder.DropTable(
                name: "Locks");

            migrationBuilder.DropTable(
                name: "Mutes");

            migrationBuilder.DropTable(
                name: "OldLogMessages");

            migrationBuilder.DropTable(
                name: "Pardons");

            migrationBuilder.DropTable(
                name: "PublishedMessages");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropTable(
                name: "Trackers");

            migrationBuilder.DropTable(
                name: "UserLevels");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Sins");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Guilds");
        }
    }
}
