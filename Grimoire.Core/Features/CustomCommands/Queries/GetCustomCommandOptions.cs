// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Core.Features.CustomCommands.Queries;
public sealed class GetCustomCommandOptions
{
    public sealed record Query : IQuery<IEnumerable<Response>>
    {
        public required string EnteredText { get; set; }
        public required ulong GuildId { get; set; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IQueryHandler<Query, IEnumerable<Response>>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<IEnumerable<Response>> Handle(Query command, CancellationToken cancellationToken)
        {
            var cleanedText = command.EnteredText.Split(" ").FirstOrDefault("");
            return await this._grimoireDbContext.CustomCommands
                .Where(x => x.GuildId == command.GuildId)
                .OrderBy(x => EF.Functions.FuzzyStringMatchLevenshtein(x.Name, cleanedText))
                .Take(5)
                .Select(x => new Response
                {
                    Name = x.Name,
                    HasMention = x.HasMention,
                    HasMessage = x.HasMessage,
                })
                .ToListAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public required string Name { get; set; }
        public required bool HasMention { get; set; }
        public required bool HasMessage { get; set; }
    }
}
