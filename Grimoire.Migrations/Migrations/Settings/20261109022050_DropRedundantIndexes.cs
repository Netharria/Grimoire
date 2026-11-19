using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Settings.Migrations
{
    /// <inheritdoc />
    public partial class DropRedundantIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_XpTrackedItems_GuildId_Id_SetAt",
                schema: "Settings",
                table: "XpTrackedItems");

            migrationBuilder.DropIndex(
                name: "IX_ThreadLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ThreadLocks");

            migrationBuilder.DropIndex(
                name: "IX_SpamFilterOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "SpamFilterOverrides");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_GuildId_RoleId_SetAt",
                schema: "Settings",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Mutes_UserId_GuildId_SetAt",
                schema: "Settings",
                table: "Mutes");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogChannelOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "MessageLogChannelOverrides");

            migrationBuilder.DropIndex(
                name: "IX_ChannelLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ChannelLocks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_XpTrackedItems_GuildId_Id_SetAt",
                schema: "Settings",
                table: "XpTrackedItems",
                columns: new[] { "GuildId", "Id", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ThreadLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ThreadLocks",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SpamFilterOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "SpamFilterOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_GuildId_RoleId_SetAt",
                schema: "Settings",
                table: "Rewards",
                columns: new[] { "GuildId", "RoleId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Mutes_UserId_GuildId_SetAt",
                schema: "Settings",
                table: "Mutes",
                columns: new[] { "UserId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogChannelOverrides_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "MessageLogChannelOverrides",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLocks_ChannelId_GuildId_SetAt",
                schema: "Settings",
                table: "ChannelLocks",
                columns: new[] { "ChannelId", "GuildId", "SetAt" },
                descending: new[] { false, false, true });
        }
    }
}
