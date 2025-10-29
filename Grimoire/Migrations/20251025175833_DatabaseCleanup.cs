using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        // Create new tables in Settings schema with new structure
        // (EF will generate these based on your SettingsDbContext models)

        // Then copy data from old tables to new tables


            migrationBuilder.DropForeignKey(
                name: "FK_Avatars_Members_UserId_GuildId",
                table: "Avatars");

            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommands_Guilds_GuildId",
                table: "CustomCommands");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommandsRole_Guilds_GuildId",
                table: "CustomCommandsRole");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomCommandsRole_Roles_RoleId",
                table: "CustomCommandsRole");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildCommandsSettings_Guilds_GuildId",
                table: "GuildCommandsSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildLevelSettings_Channels_LevelChannelLogId",
                table: "GuildLevelSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildLevelSettings_Guilds_GuildId",
                table: "GuildLevelSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_DeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_EditChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMessageLogSettings_Guilds_GuildId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildModerationSettings_Guilds_GuildId",
                table: "GuildModerationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Channels_UserCommandChannelId",
                table: "Guilds");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_AvatarChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_JoinChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_LeaveChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_NicknameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Channels_UsernameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildUserLogSettings_Guilds_GuildId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredChannels_Channels_ChannelId",
                table: "IgnoredChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredChannels_Guilds_GuildId",
                table: "IgnoredChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredMembers_Guilds_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredMembers_Members_UserId_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredRoles_Guilds_GuildId",
                table: "IgnoredRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredRoles_Roles_RoleId",
                table: "IgnoredRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Locks_Channels_ChannelId",
                table: "Locks");

            migrationBuilder.DropForeignKey(
                name: "FK_Locks_Guilds_GuildId",
                table: "Locks");

            migrationBuilder.DropForeignKey(
                name: "FK_Locks_Members_ModeratorId_GuildId",
                table: "Locks");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Guilds_GuildId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageHistory_Guilds_GuildId",
                table: "MessageHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageHistory_Members_GuildId_DeletedByModeratorId",
                table: "MessageHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Channels_ChannelId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Guilds_GuildId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Members_UserId_GuildId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_MessagesLogChannelOverrides_Channels_ChannelId",
                table: "MessagesLogChannelOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_MessagesLogChannelOverrides_Guilds_GuildId",
                table: "MessagesLogChannelOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_Mutes_Guilds_GuildId",
                table: "Mutes");

            migrationBuilder.DropForeignKey(
                name: "FK_Mutes_Members_UserId_GuildId",
                table: "Mutes");

            migrationBuilder.DropForeignKey(
                name: "FK_NicknameHistory_Guilds_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_NicknameHistory_Members_UserId_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_OldLogMessages_Channels_ChannelId",
                table: "OldLogMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_OldLogMessages_Guilds_GuildId",
                table: "OldLogMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pardons_Guilds_GuildId",
                table: "Pardons");

            migrationBuilder.DropForeignKey(
                name: "FK_Pardons_Members_ModeratorId_GuildId",
                table: "Pardons");

            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Guilds_GuildId",
                table: "Rewards");

            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Roles_RoleId",
                table: "Rewards");

            migrationBuilder.DropForeignKey(
                name: "FK_Sins_Guilds_GuildId",
                table: "Sins");

            migrationBuilder.DropForeignKey(
                name: "FK_Sins_Members_ModeratorId_GuildId",
                table: "Sins");

            migrationBuilder.DropForeignKey(
                name: "FK_Sins_Members_UserId_GuildId",
                table: "Sins");

            migrationBuilder.DropForeignKey(
                name: "FK_SpamFilterOverrides_Channels_ChannelId",
                table: "SpamFilterOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_SpamFilterOverrides_Guilds_GuildId",
                table: "SpamFilterOverrides");

            migrationBuilder.DropForeignKey(
                name: "FK_Trackers_Channels_LogChannelId",
                table: "Trackers");

            migrationBuilder.DropForeignKey(
                name: "FK_Trackers_Guilds_GuildId",
                table: "Trackers");

            migrationBuilder.DropForeignKey(
                name: "FK_Trackers_Members_ModeratorId_GuildId",
                table: "Trackers");

            migrationBuilder.DropForeignKey(
                name: "FK_Trackers_Members_UserId_GuildId",
                table: "Trackers");

            migrationBuilder.DropForeignKey(
                name: "FK_UsernameHistory_Users_UserId",
                table: "UsernameHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_XpHistory_Guilds_GuildId",
                table: "XpHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_XpHistory_Members_AwarderId_GuildId",
                table: "XpHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_XpHistory_Members_UserId_GuildId",
                table: "XpHistory");

            migrationBuilder.DropTable(
                name: "Reaction");

            migrationBuilder.DropIndex(
                name: "IX_XpHistory_AwarderId_GuildId",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_XpHistory_GuildId",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_Trackers_GuildId",
                table: "Trackers");

            migrationBuilder.DropIndex(
                name: "IX_Trackers_LogChannelId",
                table: "Trackers");

            migrationBuilder.DropIndex(
                name: "IX_Trackers_ModeratorId_GuildId",
                table: "Trackers");

            migrationBuilder.DropIndex(
                name: "IX_SpamFilterOverrides_GuildId",
                table: "SpamFilterOverrides");

            migrationBuilder.DropIndex(
                name: "IX_Sins_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sins_ModeratorId_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sins_UserId_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Pardons_GuildId",
                table: "Pardons");

            migrationBuilder.DropIndex(
                name: "IX_Pardons_ModeratorId_GuildId",
                table: "Pardons");

            migrationBuilder.DropIndex(
                name: "IX_OldLogMessages_ChannelId",
                table: "OldLogMessages");

            migrationBuilder.DropIndex(
                name: "IX_OldLogMessages_GuildId",
                table: "OldLogMessages");

            migrationBuilder.DropIndex(
                name: "IX_NicknameHistory_GuildId",
                table: "NicknameHistory");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_GuildId",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_UserId_GuildId",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_MessagesLogChannelOverrides_GuildId",
                table: "MessagesLogChannelOverrides");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChannelId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_GuildId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_UserId_GuildId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_MessageHistory_GuildId_DeletedByModeratorId",
                table: "MessageHistory");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Members_GuildId_UserId",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Locks_GuildId",
                table: "Locks");

            migrationBuilder.DropIndex(
                name: "IX_Locks_ModeratorId_GuildId",
                table: "Locks");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredRoles_GuildId",
                table: "IgnoredRoles");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredMembers_GuildId",
                table: "IgnoredMembers");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredChannels_GuildId",
                table: "IgnoredChannels");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_AvatarChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_JoinChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_LeaveChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_NicknameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildUserLogSettings_UsernameChannelLogId",
                table: "GuildUserLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_UserCommandChannelId",
                table: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_GuildMessageLogSettings_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildMessageLogSettings_DeleteChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildMessageLogSettings_EditChannelLogId",
                table: "GuildMessageLogSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildLevelSettings_LevelChannelLogId",
                table: "GuildLevelSettings");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommandsRole_GuildId",
                table: "CustomCommandsRole");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommandsRole_RoleId",
                table: "CustomCommandsRole");

            migrationBuilder.DropIndex(
                name: "IX_CustomCommands_GuildId",
                table: "CustomCommands");

            migrationBuilder.DropIndex(
                name: "IX_Channels_GuildId",
                table: "Channels");

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Pardons",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_GuildId_Xp",
                table: "XpHistory",
                columns: new[] { "GuildId", "Xp" });

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_UserId_GuildId_Xp",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId", "Xp" });

            migrationBuilder.CreateIndex(
                name: "IX_Sin_Id_GuildId",
                table: "Sins",
                columns: new[] { "Id", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sin_ModeratorId_GuildId_SinType",
                table: "Sins",
                columns: new[] { "ModeratorId", "GuildId", "SinType" });

            migrationBuilder.CreateIndex(
                name: "IX_Sin_UserId_GuildId_SinOn",
                table: "Sins",
                columns: new[] { "UserId", "GuildId", "SinOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_XpHistory_GuildId_Xp",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_XpHistory_UserId_GuildId_Xp",
                table: "XpHistory");

            migrationBuilder.DropIndex(
                name: "IX_Sin_Id_GuildId",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sin_ModeratorId_GuildId_SinType",
                table: "Sins");

            migrationBuilder.DropIndex(
                name: "IX_Sin_UserId_GuildId_SinOn",
                table: "Sins");

            migrationBuilder.AlterColumn<decimal>(
                name: "ModeratorId",
                table: "Pardons",
                type: "numeric(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Members_GuildId_UserId",
                table: "Members",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateTable(
                name: "Reaction",
                columns: table => new
                {
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmojiId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reaction_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_AwarderId_GuildId",
                table: "XpHistory",
                columns: new[] { "AwarderId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_XpHistory_GuildId",
                table: "XpHistory",
                column: "GuildId");

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
                name: "IX_SpamFilterOverrides_GuildId",
                table: "SpamFilterOverrides",
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
                name: "IX_Pardons_GuildId",
                table: "Pardons",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pardons_ModeratorId_GuildId",
                table: "Pardons",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_ChannelId",
                table: "OldLogMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_OldLogMessages_GuildId",
                table: "OldLogMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_NicknameHistory_GuildId",
                table: "NicknameHistory",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_GuildId",
                table: "Mutes",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagesLogChannelOverrides_GuildId",
                table: "MessagesLogChannelOverrides",
                column: "GuildId");

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
                name: "IX_MessageHistory_GuildId_DeletedByModeratorId",
                table: "MessageHistory",
                columns: new[] { "GuildId", "DeletedByModeratorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locks_GuildId",
                table: "Locks",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Locks_ModeratorId_GuildId",
                table: "Locks",
                columns: new[] { "ModeratorId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredRoles_GuildId",
                table: "IgnoredRoles",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredMembers_GuildId",
                table: "IgnoredMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredChannels_GuildId",
                table: "IgnoredChannels",
                column: "GuildId");

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
                name: "IX_Guilds_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_UserCommandChannelId",
                table: "Guilds",
                column: "UserCommandChannelId");

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
                name: "IX_GuildLevelSettings_LevelChannelLogId",
                table: "GuildLevelSettings",
                column: "LevelChannelLogId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommandsRole_GuildId",
                table: "CustomCommandsRole",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommandsRole_RoleId",
                table: "CustomCommandsRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCommands_GuildId",
                table: "CustomCommands",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_GuildId",
                table: "Reaction",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_UserId_GuildId",
                table: "Reaction",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Avatars_Members_UserId_GuildId",
                table: "Avatars",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Guilds_GuildId",
                table: "Channels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommands_Guilds_GuildId",
                table: "CustomCommands",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommandsRole_Guilds_GuildId",
                table: "CustomCommandsRole",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomCommandsRole_Roles_RoleId",
                table: "CustomCommandsRole",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildCommandsSettings_Guilds_GuildId",
                table: "GuildCommandsSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildLevelSettings_Channels_LevelChannelLogId",
                table: "GuildLevelSettings",
                column: "LevelChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildLevelSettings_Guilds_GuildId",
                table: "GuildLevelSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_BulkDeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "BulkDeleteChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_DeleteChannelLogId",
                table: "GuildMessageLogSettings",
                column: "DeleteChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Channels_EditChannelLogId",
                table: "GuildMessageLogSettings",
                column: "EditChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMessageLogSettings_Guilds_GuildId",
                table: "GuildMessageLogSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildModerationSettings_Guilds_GuildId",
                table: "GuildModerationSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_ModChannelLog",
                table: "Guilds",
                column: "ModChannelLog",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Channels_UserCommandChannelId",
                table: "Guilds",
                column: "UserCommandChannelId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_AvatarChannelLogId",
                table: "GuildUserLogSettings",
                column: "AvatarChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_JoinChannelLogId",
                table: "GuildUserLogSettings",
                column: "JoinChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_LeaveChannelLogId",
                table: "GuildUserLogSettings",
                column: "LeaveChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_NicknameChannelLogId",
                table: "GuildUserLogSettings",
                column: "NicknameChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Channels_UsernameChannelLogId",
                table: "GuildUserLogSettings",
                column: "UsernameChannelLogId",
                principalTable: "Channels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildUserLogSettings_Guilds_GuildId",
                table: "GuildUserLogSettings",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredChannels_Channels_ChannelId",
                table: "IgnoredChannels",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredChannels_Guilds_GuildId",
                table: "IgnoredChannels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredMembers_Guilds_GuildId",
                table: "IgnoredMembers",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredMembers_Members_UserId_GuildId",
                table: "IgnoredMembers",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredRoles_Guilds_GuildId",
                table: "IgnoredRoles",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredRoles_Roles_RoleId",
                table: "IgnoredRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Locks_Channels_ChannelId",
                table: "Locks",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Locks_Guilds_GuildId",
                table: "Locks",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Locks_Members_ModeratorId_GuildId",
                table: "Locks",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Guilds_GuildId",
                table: "Members",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageHistory_Guilds_GuildId",
                table: "MessageHistory",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageHistory_Members_GuildId_DeletedByModeratorId",
                table: "MessageHistory",
                columns: new[] { "GuildId", "DeletedByModeratorId" },
                principalTable: "Members",
                principalColumns: new[] { "GuildId", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Channels_ChannelId",
                table: "Messages",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Guilds_GuildId",
                table: "Messages",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Members_UserId_GuildId",
                table: "Messages",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessagesLogChannelOverrides_Channels_ChannelId",
                table: "MessagesLogChannelOverrides",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessagesLogChannelOverrides_Guilds_GuildId",
                table: "MessagesLogChannelOverrides",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mutes_Guilds_GuildId",
                table: "Mutes",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mutes_Members_UserId_GuildId",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NicknameHistory_Guilds_GuildId",
                table: "NicknameHistory",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NicknameHistory_Members_UserId_GuildId",
                table: "NicknameHistory",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OldLogMessages_Channels_ChannelId",
                table: "OldLogMessages",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OldLogMessages_Guilds_GuildId",
                table: "OldLogMessages",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pardons_Guilds_GuildId",
                table: "Pardons",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pardons_Members_ModeratorId_GuildId",
                table: "Pardons",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Guilds_GuildId",
                table: "Rewards",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Roles_RoleId",
                table: "Rewards",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sins_Guilds_GuildId",
                table: "Sins",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sins_Members_ModeratorId_GuildId",
                table: "Sins",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sins_Members_UserId_GuildId",
                table: "Sins",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpamFilterOverrides_Channels_ChannelId",
                table: "SpamFilterOverrides",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpamFilterOverrides_Guilds_GuildId",
                table: "SpamFilterOverrides",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trackers_Channels_LogChannelId",
                table: "Trackers",
                column: "LogChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trackers_Guilds_GuildId",
                table: "Trackers",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trackers_Members_ModeratorId_GuildId",
                table: "Trackers",
                columns: new[] { "ModeratorId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trackers_Members_UserId_GuildId",
                table: "Trackers",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsernameHistory_Users_UserId",
                table: "UsernameHistory",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_XpHistory_Guilds_GuildId",
                table: "XpHistory",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_XpHistory_Members_AwarderId_GuildId",
                table: "XpHistory",
                columns: new[] { "AwarderId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_XpHistory_Members_UserId_GuildId",
                table: "XpHistory",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "Members",
                principalColumns: new[] { "UserId", "GuildId" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
