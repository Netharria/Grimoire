// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;

namespace Grimoire.Features.Moderation.PublishSins;

public sealed partial class PublishCommands
{
    [Command("Kick")]
    [Description("Publish a kick reason to the public ban log.")]
    public Task PublishKickAsync(
        CommandContext ctx,
        [MinMaxValue(1)] [Parameter("sin_id")] [Description("The id of the sin to be published.")]
        SinId sinId)
        => this.PublishSinAsync(ctx, sinId, SinType.Kick, PublishType.Kick);
}
