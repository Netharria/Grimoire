// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("AutoPardon")]
    [Description("Updates how long till sins are automatically pardoned.")]
    public async Task AutoPardonAsync(
        CommandContext ctx,
        [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days")]
        Duration durationType,
        [MinMaxValue(0, int.MaxValue)]
        [Parameter("DurationAmount")]
        [Description("The amount of time before sins are auto pardoned.")]
        int durationAmount)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await this._settingsModule.SetAutoPardonDuration(guild.GetGuildId(), durationType.GetTimeSpan(durationAmount));

        await ctx.EditReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} updated auto pardon to {durationAmount} {durationType}"
        });
    }
}
