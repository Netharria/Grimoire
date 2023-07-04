// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Grimoire.Core.Features.Shared.Queries.GetModuleStateForGuild;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Discord.Attributes;

public class SlashRequireModuleEnabledAttribute : SlashCheckBaseAttribute
{
    public Module Module;

    public SlashRequireModuleEnabledAttribute(Module module)
    {
        this.Module = module;
    }
    public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
    {
        using var scope = ctx.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new GetModuleStateForGuildQuery { GuildId = ctx.Guild.Id, Module = Module });
    }
}

public class RequireModuleEnabledAttribute : CheckBaseAttribute
{
    public Module Module;

    public RequireModuleEnabledAttribute(Module module)
    {
        this.Module = module;
    }

    public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
    {
        using var scope = ctx.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new GetModuleStateForGuildQuery { GuildId = ctx.Guild.Id, Module = Module });
    }
}
