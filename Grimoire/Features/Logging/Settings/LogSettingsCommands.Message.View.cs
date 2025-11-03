// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace

using Grimoire.Settings.Enums;

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [Command("View")]
        [Description("View the current settings for the Message Log Module.")]
        public async Task ViewAsync(CommandContext ctx)
        {
            if (ctx is SlashCommandContext slashContext)
                await slashContext.DeferResponseAsync(true);
            else
                await ctx.DeferResponseAsync();


            var guild = ctx.Guild!;

            var deleteChannelLogId =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.MessageDeleted, guild.GetGuildId());
            var bulkDeleteChannelLogId =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.BulkMessageDeleted, guild.GetGuildId());
            var editChannelLogId =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.MessageEdited, guild.GetGuildId());

            var deleteChannelLog =
                deleteChannelLogId is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(deleteChannelLogId))?
                    .Mention ?? "Deleted Channel";
            var bulkDeleteChannelLog =
                bulkDeleteChannelLogId is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(bulkDeleteChannelLogId))?
                    .Mention ?? "Deleted Channel";
            var editChannelLog =
                editChannelLogId is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(editChannelLogId))?
                    .Mention ?? "Deleted Channel";
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {await this._settingsModule.IsModuleEnabled(Module.MessageLog, guild.GetGuildId())}\n" +
                $"**Delete Log:** {deleteChannelLog}\n" +
                $"**Bulk Delete Log:** {bulkDeleteChannelLog}\n" +
                $"**Edit Log:** {editChannelLog}\n");
        }
    }
}
