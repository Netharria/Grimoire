// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Features.Shared.Attributes;

internal sealed class SlashRequireModuleEnabledAttribute(Module module) : SlashCheckBaseAttribute
{
    public readonly Module Module = module;

    public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
    {
        using var scope = ctx.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new GetModuleStateForGuildQuery { GuildId = ctx.Guild.Id, Module = this.Module });
    }
}
