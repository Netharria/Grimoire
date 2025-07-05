// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using DSharpPlus.Commands.Processors.SlashCommands;

// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [Command("ViewOverrides")]
        [Description("View the currently Configured log overrides")]
        public async Task ViewOverrides(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var channelOverrideString = new StringBuilder();
            await foreach (var channelOverride in this._mediator.CreateStream(
                               new GetMessageLogOverrides.Query { GuildId = ctx.Guild.Id }))
            {
                var channel = ctx.Guild.Channels.GetValueOrDefault(channelOverride.ChannelId)
                              ?? ctx.Guild.Threads.GetValueOrDefault(channelOverride.ChannelId);
                if (channel is null)
                    continue;

                channelOverrideString.Append(channel.Mention)
                    .Append(channelOverride.ChannelOption switch
                    {
                        MessageLogOverrideOption.AlwaysLog => " - Always Log",
                        MessageLogOverrideOption.NeverLog => " - Never Log",
                        _ => " - Inherit/Default"
                    }).AppendLine();
            }

            await ctx.EditReplyAsync(GrimoireColor.Purple, title: "Channel Override Settings",
                message: channelOverrideString.ToString());
        }
    }
}

public sealed class GetMessageLogOverrides
{
    public sealed record Query : IStreamRequest<Response>
    {
        public required ulong GuildId { get; init; }
    }

    public sealed record Response
    {
        public required ulong ChannelId { get; init; }
        public required MessageLogOverrideOption ChannelOption { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IStreamRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Query query,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var channelOverrides = dbContext.MessagesLogChannelOverrides
                .AsNoTracking()
                .Where(x => x.GuildId == query.GuildId)
                .Select(x => new Response { ChannelId = x.ChannelId, ChannelOption = x.ChannelOption })
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken);

            await foreach (var channelOverride in channelOverrides) yield return channelOverride;
        }
    }
}
