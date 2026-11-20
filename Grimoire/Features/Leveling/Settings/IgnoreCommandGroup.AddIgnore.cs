// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public partial class IgnoreCommandGroup
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Add")]
    [Description("Adds a user, channel, or role to the ignored xp list.")]
    public Task IgnoreAsync(CommandContext ctx,
        [Parameter("channel")] [Description("The channel to ignore.")]
        DiscordChannel? channel = null,
        [VariadicArgument(24)] [Parameter("items")] [Description("The users or roles to ignore.")]
        SnowflakeObject[]? items = null)
        => this.UpdateIgnoredAsync(ctx, channel, items,
            (guildId, setBy, targets) => this._settingsModule.IgnoreXpAsync(guildId, setBy, targets),
            "are now ignored for xp gain.", "were already ignored.");
}
