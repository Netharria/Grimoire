// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using JetBrains.Annotations;
using LanguageExt;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    [UsedImplicitly]
    [Command("View")]
    [Description("View the current states of the modules.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        if (await this._settingsModule.GetAllModuleState(guild.GetGuildId())
            is not Fin<ModuleStates>.Succ { Value: var guildSettings })
        {
            await ctx.SendErrorResponseAsync("Could not load the module states. Please try again later.");
            return;
        }

        await ctx.ReplyAsync(
            title: "Current states of modules.",
            message: $"**Leveling Enabled:** {guildSettings.Leveling}\n" +
                     $"**User Log Enabled:** {guildSettings.UserLog}\n" +
                     $"**Message Log Enabled:** {guildSettings.MessageLog}\n" +
                     $"**Moderation Enabled:** {guildSettings.Moderation}\n" +
                     $"**Commands Enabled:** {guildSettings.Commands}\n" +
                     $"**Anti Spam Enabled:** {guildSettings.AntiSpam}\n");
    }
}
