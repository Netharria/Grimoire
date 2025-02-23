// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Features.Shared.Attributes;

internal sealed class RequireModuleEnabledAttribute(Module module) : ContextCheckAttribute
{
    public readonly Module Module = module;
}

internal sealed class RequireModuleEnabledCheck : IContextCheck<RequireModuleEnabledAttribute>
{
    public async ValueTask<string?> ExecuteCheckAsync(RequireModuleEnabledAttribute attribute, CommandContext context)
    {
        if (context.Guild is null)
        {
            return "This command can only be used in a server.";
        }
        var mediator = context.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new GetModuleStateForGuild.Request { GuildId = context.Guild.Id, Module = attribute.Module });
        return !result ? "This module is disabled in this server." : null;
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
