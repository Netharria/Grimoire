// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Moderation.Queries;

public enum SinQueryType
{
    Warn,
    Mute,
    Ban,
    All,
    Mod
}
public sealed record GetUserSinsQuery : IRequest<GetUserSinsQueryResponse>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public SinQueryType SinQueryType { get; init; }
}

public sealed class GetUserSinsQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetUserSinsQuery, GetUserSinsQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<GetUserSinsQueryResponse> Handle(GetUserSinsQuery query, CancellationToken cancellationToken)
    {
        var queryable = this._grimoireDbContext.Sins
            .AsNoTracking().Where(x => x.UserId == query.UserId && x.GuildId == query.GuildId);

        queryable = query.SinQueryType switch
        {
            SinQueryType.Warn => queryable.Where(x => x.SinType == SinType.Warn),
            SinQueryType.Mute => queryable.Where(x => x.SinType == SinType.Mute),
            SinQueryType.Ban => queryable.Where(x => x.SinType == SinType.Ban),
            _ => queryable
        };

        var result = await queryable
            .Where(x => x.SinOn > DateTimeOffset.UtcNow - x.Guild.ModerationSettings.AutoPardonAfter)
            .Select(x => new
            {
                x.Id,
                x.SinType,
                x.SinOn,
                x.Reason,
                Moderator = x.Moderator.Mention(),
                Pardon = x.Pardon != null,
                PardonModerator = x.Pardon != null ? x.Pardon.Moderator.Mention() : "",
                PardonDate = x.Pardon != null ? x.Pardon.PardonDate : DateTimeOffset.MinValue,
            }).ToListAsync(cancellationToken);
        var stringBuilder = new StringBuilder(2048);
        var resultStrings = new List<string>();
        result.ForEach(x =>
        {
            var builder = $"**{x.Id} : {x.SinType}** : <t:{x.SinOn.ToUnixTimeSeconds()}:f>\n" +
                          $"\tReason: {x.Reason}\n" +
                          $"\tModerator: {x.Moderator}\n";
            if (x.Pardon)
                builder = $"~~{builder}~~" +
                $"**Pardoned by: {x.PardonModerator} on <t:{x.PardonDate.ToUnixTimeSeconds()}:f>**\n";
            if (stringBuilder.Length + builder.Length > stringBuilder.Capacity)
            {
                resultStrings.Add(stringBuilder.ToString());
                stringBuilder.Clear();
            }
            stringBuilder.Append(builder);
        });
        if (stringBuilder.Length > 0)
            resultStrings.Add(stringBuilder.ToString());

        return new GetUserSinsQueryResponse
        {
            SinList = [.. resultStrings]
        };
    }
}

public sealed record GetUserSinsQueryResponse : BaseResponse
{
    public string[] SinList { get; init; } = [];
}
