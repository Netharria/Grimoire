// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Features.CustomCommands;

public sealed class GetCustomCommandOptions
{
    internal sealed class AutocompleteProvider(IServiceProvider serviceProvider) : IAutoCompleteProvider
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        {
            if (context.Guild is null)
                return [];
            await using var scope = this._serviceProvider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetService<IMediator>();

            if (mediator is null)
                return [];

            return await mediator.CreateStream(
                    new Request
                    {
                        EnteredText = context.UserInput ?? string.Empty, GuildId = context.Guild.Id
                    }).Select(x =>
                    new DiscordAutoCompleteChoice(
                        x.Name + " " +
                        (x.HasMention ? "<Mention> " : string.Empty) +
                        (x.HasMessage ? "<Message>" : string.Empty),
                        x.Name))
                .ToListAsync();
        }
    }

    public sealed record Request : IStreamRequest<Response>
    {
        public required string EnteredText { get; init; }
        public required ulong GuildId { get; init; }
    }

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IStreamRequestHandler<Request, Response>
    {
        private static readonly Func<GrimoireDbContext, ulong, string, IAsyncEnumerable<Response>> _getCommandsAsync =
            EF.CompileAsyncQuery((GrimoireDbContext context, ulong guildId, string cleanedText) =>
                context.CustomCommands
                    .AsNoTracking()
                    .Where(x => x.GuildId == guildId)
                    .OrderBy(x => EF.Functions.FuzzyStringMatchLevenshtein(x.Name, cleanedText))
                    .Take(5)
                    .Select(x => new Response { Name = x.Name, HasMention = x.HasMention, HasMessage = x.HasMessage })
            );

        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Request request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var cleanedText = request.EnteredText.Split(' ').FirstOrDefault(string.Empty);
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await foreach (var response in _getCommandsAsync(dbContext, request.GuildId, cleanedText)
                               .WithCancellation(cancellationToken))
                yield return response;
        }
    }

    public sealed record Response
    {
        public required string Name { get; init; }
        public required bool HasMention { get; init; }
        public required bool HasMessage { get; init; }
    }
}
