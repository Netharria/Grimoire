// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{
    [SlashCommand("View", "View the current settings for the leveling module.")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new GetLevelSettings.Request{ GuildId = ctx.Guild.Id });
        var levelLogMention =
                response.LevelChannelLog is null ?
                "None" :
                ctx.Guild.Channels.GetValueOrDefault(response.LevelChannelLog.Value)?.Mention;
        await ctx.EditReplyAsync(
            title: "Current Level System Settings",
            message: $"**Module Enabled:** {response.ModuleEnabled}\n" +
            $"**Texttime:** {response.TextTime.TotalMinutes} minutes.\n" +
            $"**Base:** {response.Base}\n" +
            $"**Modifier:** {response.Modifier}\n" +
            $"**Reward Amount:** {response.Amount}\n" +
            $"**Log-Channel:** {levelLogMention}\n");
    }
}

public sealed class GetLevelSettings
{
    public sealed record Request : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var guildLevelSettings = await this._grimoireDbContext.GuildLevelSettings
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new
            {
                x.ModuleEnabled,
                x.TextTime,
                x.Base,
                x.Modifier,
                x.Amount,
                x.LevelChannelLogId
            }).FirstAsync(cancellationToken: cancellationToken);
            return new Response
            {
                ModuleEnabled = guildLevelSettings.ModuleEnabled,
                TextTime = guildLevelSettings.TextTime,
                Base = guildLevelSettings.Base,
                Modifier = guildLevelSettings.Modifier,
                Amount = guildLevelSettings.Amount,
                LevelChannelLog = guildLevelSettings.LevelChannelLogId
            };
        }

    }

    public sealed record Response : BaseResponse
    {
        public bool ModuleEnabled { get; init; }
        public TimeSpan TextTime { get; init; }
        public int Base { get; init; }
        public int Modifier { get; init; }
        public int Amount { get; init; }
        public ulong? LevelChannelLog { get; init; }
    }


}

