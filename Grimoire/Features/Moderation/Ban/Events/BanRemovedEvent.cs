// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.Ban.Shared;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Moderation.Ban.Events;

public class BanRemovedEvent(IMediator mediator, GuildLog guildLog) : IEventHandler<GuildBanRemovedEventArgs>
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly IMediator _mediator = mediator;

    public async Task HandleEventAsync(DiscordClient sender, GuildBanRemovedEventArgs args)
    {
        var response = await this._mediator.Send(new GetLastBan.Query
        {
            UserId = args.Member.Id, GuildId = args.Guild.Id
        });

        if (response is null || !response.ModerationModuleEnabled)
            return;

        if (response.LastSin is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unbanned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{response.LastSin.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green);
        if (response.LastSin.ModeratorId is not null)
            embed.AddField("Mod", UserExtensions.Mention(response.LastSin.ModeratorId), true);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }
}
