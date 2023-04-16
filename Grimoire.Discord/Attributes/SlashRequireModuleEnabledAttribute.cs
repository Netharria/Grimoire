// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Shared.Queries.GetModuleStateForGuild;

namespace Grimoire.Discord.Attributes
{
    public class SlashRequireModuleEnabledAttribute : SlashCheckBaseAttribute
    {
        public Module Module;

        public SlashRequireModuleEnabledAttribute(Module module)
        {
            this.Module = module;
        }
        public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
        {
            if (ctx.Services.GetService(typeof(IMediator)) is not IMediator mediator)
                throw new NullReferenceException($"Reflection was not able to grab a mediator instance to check if {this.Module.GetName()} was enabled.");
            return await mediator.Send(new GetModuleStateForGuildQuery { GuildId = ctx.Guild.Id, Module = Module });
        }
    }
}
