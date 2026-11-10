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
            await ctx.DeferPrivateAsync();

            var guild = ctx.RequiredGuild;

            Task<string> Describe(GuildLogType logType) => DescribeLogChannelAsync(this._settingsModule, guild, logType);

            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Delete Log:** {await Describe(GuildLogType.MessageDeleted)}\n" +
                $"**Bulk Delete Log:** {await Describe(GuildLogType.BulkMessageDeleted)}\n" +
                $"**Edit Log:** {await Describe(GuildLogType.MessageEdited)}\n");
        }
    }
}
