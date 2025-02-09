// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
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
        return await mediator.Send(new GetModuleStateForGuild.Request { GuildId = ctx.Guild.Id, Module = this.Module });
    }
}

internal sealed class GetModuleStateForGuild
{
    public sealed record Request : IRequest<bool>
    {
        public ulong GuildId { get; init; }
        public Module Module { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, bool>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<bool> Handle(Request request, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Guilds.AsNoTracking().WhereIdIs(request.GuildId)
                .GetModulesOfType(request.Module)
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                .Select(module => module != null && module.ModuleEnabled)
                .FirstOrDefaultAsync(cancellationToken);
            return result;
        }
    }
}
