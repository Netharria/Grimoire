// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Features.CustomCommands;
public sealed class GetCustomCommandOptions
{

    internal sealed class AutocompleteProvider(IServiceProvider serviceProvider) : IAutocompleteProvider
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            await using var scope = this._serviceProvider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetService<IMediator>();

            if (mediator is null)
                return [];

            return await mediator.CreateStream(
                new Request
                {
                    EnteredText = (string)(ctx.FocusedOption.Value ?? string.Empty),
                    GuildId = ctx.Guild.Id
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

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IStreamRequestHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;
        private static readonly Func<GrimoireDbContext, ulong, string, IAsyncEnumerable<Response>> _getCommandsAsync =
            EF.CompileAsyncQuery((GrimoireDbContext context, ulong guildId, string cleanedText) =>
                context.CustomCommands
                    .AsNoTracking()
                    .Where(x => x.GuildId == guildId)
                    .OrderBy(x => EF.Functions.FuzzyStringMatchLevenshtein(x.Name, cleanedText))
                    .Take(5)
                    .Select(x => new Response
                    {
                        Name = x.Name,
                        HasMention = x.HasMention,
                        HasMessage = x.HasMessage,
                    })
            );

        public async IAsyncEnumerable<Response> Handle(Request request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var cleanedText = request.EnteredText.Split(' ').FirstOrDefault(string.Empty);
            await foreach (var response in _getCommandsAsync(this._grimoireDbContext, request.GuildId, cleanedText)
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
