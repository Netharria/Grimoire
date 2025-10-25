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
        public async Task ViewAsync(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync(true);

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var deleteChannelLogId =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.MessageDeleted, ctx.Guild.Id);
            var bulkDeleteChannelLogId =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.BulkMessageDeleted, ctx.Guild.Id);
            var editChannelLogId =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.MessageEdited, ctx.Guild.Id);

            var deleteChannelLog =
                deleteChannelLogId is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(deleteChannelLogId.Value))
                    .Mention;
            var bulkDeleteChannelLog =
                bulkDeleteChannelLogId is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(bulkDeleteChannelLogId.Value))
                    .Mention;
            var editChannelLog =
                editChannelLogId is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(editChannelLogId.Value))
                    .Mention;
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {await this._settingsModule.IsModuleEnabled(Module.MessageLog, ctx.Guild.Id)}\n" +
                $"**Delete Log:** {deleteChannelLog}\n" +
                $"**Bulk Delete Log:** {bulkDeleteChannelLog}\n" +
                $"**Edit Log:** {editChannelLog}\n");
        }
    }
}
