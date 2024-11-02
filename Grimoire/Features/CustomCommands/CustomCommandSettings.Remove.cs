// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [UsedImplicitly]
    [SlashCommand("Forget", "Forget a command")]
    internal async Task Forget(
        InteractionContext ctx,
        [Autocomplete(typeof(GetCustomCommandOptions.AutocompleteProvider))]
        [Option("Name", "The name that the command is called.", true)]
        string name)
    {
        await ctx.DeferAsync();

        var response = await this._mediator.Send(new RemoveCustomCommand.Request
        {
            CommandName = name, GuildId = ctx.Guild.Id
        });

        await ctx.EditReplyAsync(GrimoireColor.Green, response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }
}

public sealed class RemoveCustomCommand
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public required string CommandName { get; init; }
        public required ulong GuildId { get; init; }
    }

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.CustomCommands
                .Include(x => x.CustomCommandRoles)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Name == request.CommandName && x.GuildId == request.GuildId,
                    cancellationToken);
            if (result is null)
                throw new AnticipatedException($"Did not find a saved command with name {request.CommandName}");

            dbContext.CustomCommands.Remove(result);
            await dbContext.SaveChangesAsync(cancellationToken);
            var modChannelLog = await dbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(request.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Removed command {request.CommandName}", LogChannelId = modChannelLog
            };
        }
    }
}
