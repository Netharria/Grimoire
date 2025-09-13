// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [UsedImplicitly]
    [Command("Forget")]
    [Description("Forget a command that you have saved.")]
    public async Task Forget(
        SlashCommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
        [Parameter("Name")]
        [Description("The name of the command to forget.")]
        string name)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        await this._mediator.Send(new RemoveCustomCommand.Request
        {
            CommandName = name, GuildId = ctx.Guild.Id
        });

        await ctx.EditReplyAsync(GrimoireColor.Green, $"Forgot command: {name}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description = $"{ctx.User.Mention} asked {ctx.Guild.CurrentMember} to forget command: {name}",
            Color = GrimoireColor.Purple
        });
    }
}

public sealed class RemoveCustomCommand
{
    public sealed record Request : IRequest
    {
        public required string CommandName { get; init; }
        public required ulong GuildId { get; init; }
    }

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Request request, CancellationToken cancellationToken)
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
        }
    }
}
