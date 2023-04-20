// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.Moderation.Queries.GetModLogsForUser
{
    public class GetUserSinsQueryHandler : IQueryHandler<GetUserSinsQuery, GetUserSinsQueryResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetUserSinsQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetUserSinsQueryResponse> Handle(GetUserSinsQuery query, CancellationToken cancellationToken)
        {
            IQueryable<Sin> queryable;
            if (query.SinQueryType == SinQueryType.Mod)
            {
                queryable = this._grimoireDbContext.Sins.Where(x => x.ModeratorId == query.UserId && x.GuildId == query.GuildId);
            }
            else
            {
                queryable = this._grimoireDbContext.Sins.Where(x => x.UserId == query.UserId && x.GuildId == query.GuildId);
            }

            queryable = query.SinQueryType switch
            {
                SinQueryType.Warn => queryable.Where(x => x.SinType == SinType.Warn),
                SinQueryType.Mute => queryable.Where(x => x.SinType == SinType.Mute),
                SinQueryType.Ban => queryable.Where(x => x.SinType == SinType.Ban),
                _ => queryable
            };

            var result = await queryable.Select(x => new
            {
                x.Id,
                x.SinType,
                x.InfractionOn,
                x.Reason,
                Moderator = x.Moderator.Mention(),
                Pardon = x.Pardon != null,
                PardonModerator = x.Pardon!.Moderator.Mention(),
                x.Pardon!.PardonDate
            }).ToListAsync(cancellationToken);
            var stringBuilder = new StringBuilder(2048);
            var resultStrings = new List<string>();
            result.ForEach(x =>
            {
                var builder = $"**{x.Id} : {x.SinType}** : {x.InfractionOn}\n" +
                              $"Reason: {x.Reason}\n" +
                              $"Moderator: {x.Moderator}\n";
                if (x.Pardon)
                {
                    builder = $"~~{builder}~~" +
                    $"**Pardoned by: {x.PardonModerator} on {x.PardonDate}**\n";
                }
                if (stringBuilder.Length + builder.Length > stringBuilder.MaxCapacity)
                {
                    resultStrings.Add(stringBuilder.ToString());
                    stringBuilder.Clear();
                }
            });
            if (stringBuilder.Length > 0)
                resultStrings.Add(stringBuilder.ToString());

            return new GetUserSinsQueryResponse
            {
                SinList = resultStrings.ToArray()
            };
        }
    }
}
