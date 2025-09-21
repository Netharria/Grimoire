// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Commands.ContextChecks;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class SinLog
{
    public enum SinQueryType
    {
        Warn,
        Mute,
        Ban,
        All,
        Mod
    }

    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    internal sealed class Command(IMediator mediator)
    {
        private readonly IMediator _mediator = mediator;

        [Command("SinLog")]
        [Description("Looks up the sin logs for the provided user.")]
        public async Task SinLogAsync(
            SlashCommandContext ctx,
            [Parameter("Type")] [Description("The type of logs to look up.")]
            SinQueryType sinQueryType,
            [Parameter("User")] [Description("The user to look up the logs for. Leave blank for self.")]
            DiscordUser? user = null)
        {
            if (ctx.Guild is null || ctx.Member is null)
                throw new AnticipatedException("This command can only be used in a server.");

            await ctx.DeferResponseAsync(!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages));
            user ??= ctx.User;


            if (!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages) && ctx.User != user)
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
                    .WithAuthor($"Moderation log for {user.Username}")
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
                    $"Sin log for {user.Username}");
            foreach (var message in response.SinList)
                await ctx.EditReplyAsync(GrimoireColor.Green, message,
                    $"Sin log for {user.Username}");
        }
    }

    public sealed record GetModActionCountsQuery : IRequest<GetModActionCountsQueryResponse?>
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
    }

    public sealed class GetModActionCountsQueryHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<GetModActionCountsQuery, GetModActionCountsQueryResponse?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<GetModActionCountsQueryResponse?> Handle(GetModActionCountsQuery query,
            CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(member => new GetModActionCountsQueryResponse
                {
                    BanCount = member.ModeratedSins.Count(sin => sin.SinType == SinType.Ban),
                    MuteCount = member.ModeratedSins.Count(sin => sin.SinType == SinType.Mute),
                    WarnCount = member.ModeratedSins.Count(sin => sin.SinType == SinType.Warn)
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record GetModActionCountsQueryResponse
    {
        public int BanCount { get; init; }
        public int MuteCount { get; init; }
        public int WarnCount { get; init; }
    }

    public sealed record GetUserSinsQuery : IRequest<GetUserSinsQueryResponse>
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
        public SinQueryType SinQueryType { get; init; }
    }

    public sealed class GetUserSinsQueryHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<GetUserSinsQuery, GetUserSinsQueryResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<GetUserSinsQueryResponse> Handle(GetUserSinsQuery query, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var queryable = dbContext.Sins
                .AsNoTracking().Where(x => x.UserId == query.UserId && x.GuildId == query.GuildId);

            queryable = query.SinQueryType switch
            {
                SinQueryType.Warn => queryable.Where(x => x.SinType == SinType.Warn),
                SinQueryType.Mute => queryable.Where(x => x.SinType == SinType.Mute),
                SinQueryType.Ban => queryable.Where(x => x.SinType == SinType.Ban),
                SinQueryType.All => queryable,
                _ => throw new ArgumentOutOfRangeException(nameof(query.SinQueryType), query.SinQueryType, null)
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
                    PardonModerator = x.Pardon != null ? x.Pardon.Moderator.Mention() : null,
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

    public sealed record GetUserSinsQueryResponse
    {
        public string[] SinList { get; init; } = [];
    }
}
