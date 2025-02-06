// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class SinLog
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("SinLog", "Looks up the sin logs for the provided user.")]
        public async Task SinLogAsync(
            InteractionContext ctx,
            [Option("Type", "The Type of logs to lookup.")]
            SinQueryType sinQueryType,
            [Option("User", "The user to look up the logs for. Leave blank for self.")]
            DiscordUser? user = null)
        {
            await ctx.DeferAsync(!ctx.Member.Permissions.HasPermission(DiscordPermissions.ManageMessages));
            user ??= ctx.User;


            if (!ctx.Member.Permissions.HasPermission(DiscordPermissions.ManageMessages) && ctx.User != user)
                throw new AnticipatedException("Only moderators can look up logs for someone else.");
            if (sinQueryType == SinQueryType.Mod)
            {
                var modResponse = await this._mediator.Send(new GetModActionCountsQuery
                {
                    UserId = user.Id, GuildId = ctx.Guild.Id
                });
                if (modResponse is null)
                {
                    await ctx.EditReplyAsync(GrimoireColor.Red, "Did not find a moderator with that id.");
                    return;
                }

                await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
                    .WithAuthor($"Moderation log for {user.GetUsernameWithDiscriminator()}")
                    .AddField("Bans", modResponse.BanCount.ToString(), true)
                    .AddField("Mutes", modResponse.MuteCount.ToString(), true)
                    .AddField("Warns", modResponse.WarnCount.ToString(), true)
                    .WithColor(GrimoireColor.Purple));
                return;
            }

            var response = await this._mediator.Send(new GetUserSinsQuery
            {
                UserId = user.Id, GuildId = ctx.Guild.Id, SinQueryType = sinQueryType
            });
            if (response.SinList.Length == 0)
                await ctx.EditReplyAsync(GrimoireColor.Green, "That user does not have any logs",
                    $"Sin log for {user.GetUsernameWithDiscriminator()}");
            foreach (var message in response.SinList)
                await ctx.EditReplyAsync(GrimoireColor.Green, message,
                    $"Sin log for {user.GetUsernameWithDiscriminator()}");
        }
    }

    public sealed record GetModActionCountsQuery : IRequest<GetModActionCountsQueryResponse?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class GetModActionCountsQueryHandler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<GetModActionCountsQuery, GetModActionCountsQueryResponse?>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<GetModActionCountsQueryResponse?> Handle(GetModActionCountsQuery query,
            CancellationToken cancellationToken)
            => await this._grimoireDbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(x => new GetModActionCountsQueryResponse
                {
                    BanCount = x.ModeratedSins.Count(x => x.SinType == SinType.Ban),
                    MuteCount = x.ModeratedSins.Count(x => x.SinType == SinType.Mute),
                    WarnCount = x.ModeratedSins.Count(x => x.SinType == SinType.Warn)
                }).FirstOrDefaultAsync(cancellationToken);
    }

    public sealed record GetModActionCountsQueryResponse : BaseResponse
    {
        public int BanCount { get; init; }
        public int MuteCount { get; init; }
        public int WarnCount { get; init; }
    }

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

    public sealed class GetUserSinsQueryHandler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<GetUserSinsQuery, GetUserSinsQueryResponse>
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
                    PardonDate = x.Pardon != null ? x.Pardon.PardonDate : DateTimeOffset.MinValue
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

            return new GetUserSinsQueryResponse { SinList = [.. resultStrings] };
        }
    }

    public sealed record GetUserSinsQueryResponse : BaseResponse
    {
        public string[] SinList { get; init; } = [];
    }
}

