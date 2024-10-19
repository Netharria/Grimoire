// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Logging.Settings;
public partial class LogSettingsCommands
{
    public partial class Message
    {
        [SlashCommand("ViewOverrides", "View the currently Configured log overrides")]
        public async Task ViewOverrides(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            var response = await this._mediator.Send(new GetMessageLogOverrides.Query{ GuildId = ctx.Guild.Id });

            var overrideOptions = response
               .Select(x => new
               {
                   Channel = ctx.Guild.Channels.GetValueOrDefault(x.ChannelId) ?? ctx.Guild.Threads.GetValueOrDefault(x.ChannelId),
                   x.ChannelOption
               }).OrderBy(x => x.Channel?.Position)
                .ToList();

            var channelOverrideString = new StringBuilder();
            foreach (var overrideOption in overrideOptions)
            {
                if (overrideOption.Channel is null)
                    continue;

                channelOverrideString.Append(overrideOption.Channel.Mention)
                    .Append(overrideOption.ChannelOption switch
                    {
                        MessageLogOverrideOption.AlwaysLog => " - Always Log",
                        MessageLogOverrideOption.NeverLog => " - Never Log",
                        _ => " - Inherit/Default"
                    }).AppendLine();
            }
            await ctx.EditReplyAsync(GrimoireColor.Purple, title: "Channel Override Settings", message: channelOverrideString.ToString());
        }
    }
}

public sealed class GetMessageLogOverrides
{
    public sealed record Query : IQuery<List<Response>>
    {
        public required ulong GuildId { get; init; }
    }
    public sealed record Response
    {
        public required ulong ChannelId { get; init; }
        public required MessageLogOverrideOption ChannelOption { get; init; }
    }

    public sealed class Handler(GrimoireDbContext dbContext) : IQueryHandler<Query, List<Response>>
    {
        private readonly GrimoireDbContext _dbContext = dbContext;

        public async ValueTask<List<Response>> Handle(Query query, CancellationToken cancellationToken)
            => await this._dbContext.MessagesLogChannelOverrides
            .AsNoTracking()
            .Where(x => x.GuildId == query.GuildId)
            .Select(x => new Response
            {
                ChannelId = x.ChannelId,
                ChannelOption = x.ChannelOption
            })
            .ToListAsync(cancellationToken);
    }
}
