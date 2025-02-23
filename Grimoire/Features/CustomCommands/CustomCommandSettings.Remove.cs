// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [UsedImplicitly]
    [Command("Forget")]
    [Description("Forget a command that you have saved. This will remove the command from the bot's memory and it will no longer be available.")]
    internal async Task Forget(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
        [Parameter("Name")]
        [Description("The name of the command to forget.")]
        string name)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

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
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
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
