// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.CustomCommands.Queries;
public sealed class GetCommand
{
    public sealed record Query : IQuery<Response>
    {
        public required string Name { get; set; }
        public required ulong GuildId { get; set; }
    }

    public sealed class Handler(GrimoireDbContext GrimoireDbContext) : IQueryHandler<Query, Response?>
    {
        private readonly GrimoireDbContext _grimoireDbContext = GrimoireDbContext;

        public async ValueTask<Response?> Handle(Query query, CancellationToken cancellationToken)
            => await this._grimoireDbContext.CustomCommands
            .AsSplitQuery()
            .Where(x => x.GuildId == query.GuildId && x.Name == query.Name)
            .Select(x => new Response
            {
                Content = x.Content,
                HasMention = x.HasMention,
                HasMessage = x.HasMessage,
                IsEmbedded = x.IsEmbedded,
                EmbedColor = x.EmbedColor,
                RestrictedUse = x.RestrictedUse,
                PermissionRoles = x.CustomCommandRoles.Select(x => x.RoleId).ToArray(),

            }).FirstOrDefaultAsync(cancellationToken);
    }

    public sealed record Response
    {
        public required string Content { get; set; }
        public required bool HasMention { get; set; }
        public required bool HasMessage { get; set; }
        public required bool IsEmbedded { get; set; }
        public required string? EmbedColor { get; set; }
        public required bool RestrictedUse { get; set; }
        public required ulong[] PermissionRoles { get; set; }
    }
}
