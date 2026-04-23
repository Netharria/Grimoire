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
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.MessageDeleted,
                    guild.GetGuildId());
            var bulkDeleteChannelLogId =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.BulkMessageDeleted,
                    guild.GetGuildId());
            var editChannelLogId =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.MessageEdited,
                    guild.GetGuildId());

            var deleteChannelLog =
                deleteChannelLogId.OrElse(null) is not { } deleteChannel
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(deleteChannel))?
                    .Mention ?? "Deleted Channel";
            var bulkDeleteChannelLog =
                bulkDeleteChannelLogId.OrElse(null) is not { } bulkChannel
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(bulkChannel))?
                    .Mention ?? "Deleted Channel";
            var editChannelLog =
                editChannelLogId.OrElse(null) is not { } editChannel
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(editChannel))?
                    .Mention ?? "Deleted Channel";
            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {(await this._settingsModule.IsModuleEnabled(Module.MessageLog, guild.GetGuildId())).OrElse(false)}\n" +
                $"**Delete Log:** {deleteChannelLog}\n" +
                $"**Bulk Delete Log:** {bulkDeleteChannelLog}\n" +
                $"**Edit Log:** {editChannelLog}\n");
        }
    }
}
