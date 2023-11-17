// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Grimoire.Core.Features.Shared.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Discord.Attributes;

public class SlashRequireModuleEnabledAttribute(Module module) : SlashCheckBaseAttribute
{
    public Module Module = module;

    public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
    {
        using var scope = ctx.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new GetModuleStateForGuildQuery { GuildId = ctx.Guild.Id, Module = Module });
    }
}

public class RequireModuleEnabledAttribute(Module module) : CheckBaseAttribute
{
    public Module Module = module;

    public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
    {
        using var scope = ctx.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new GetModuleStateForGuildQuery { GuildId = ctx.Guild.Id, Module = Module });
    }
}
