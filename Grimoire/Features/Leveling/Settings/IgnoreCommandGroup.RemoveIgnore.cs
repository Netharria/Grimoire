// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public partial class IgnoreCommandGroup
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public Task WatchAsync(CommandContext ctx,
        [Parameter("channel")] [Description("The channel to remove from the ignore xp list.")]
        DiscordChannel? channel = null,
        [VariadicArgument(24)] [Parameter("items")] [Description("The users or roles to remove from the ignore xp list.")]
        SnowflakeObject[]? items = null)
        => this.UpdateIgnoredAsync(ctx, channel, items, (item, guildId, setBy, setAt) => item switch
        {
            DiscordUser => WatchedMember.Create(new UserId(item.Id), guildId, setBy, setAt).Map(XpTrackedItem (x) => x),
            DiscordRole => WatchedRole.Create(new RoleId(item.Id), guildId, setBy, setAt).Map(XpTrackedItem (x) => x),
            DiscordChannel => WatchedChannel.Create(new ChannelId(item.Id), guildId, setBy, setAt)
                .Map(XpTrackedItem (x) => x),
            _ => throw new InvariantViolationException($"Unexpected ignore option {item.GetType().Name}.")
        }, "are no longer ignored for xp gain.", "were not ignored.");
}
