// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{
    [Command("View")]
    [Description("View the current settings for the leveling module.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new GetLevelSettings.Request { GuildId = ctx.Guild.Id });
        if (response is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Red, "Leveling settings could not be found for this server.");
            return;
        }

        var levelLogMention =
            response.LevelChannelLog is null
                ? "None"
                : ctx.Guild.Channels.GetValueOrDefault(response.LevelChannelLog.Value)?.Mention;
        await ctx.EditReplyAsync(
            title: "Current Level System Settings",
            message: $"**Module Enabled:** {response.ModuleEnabled}\n" +
                     $"**Text Time:** {response.TextTime.TotalMinutes} minutes.\n" +
                     $"**Base:** {response.Base}\n" +
                     $"**Modifier:** {response.Modifier}\n" +
                     $"**Reward Amount:** {response.Amount}\n" +
                     $"**Log-Channel:** {levelLogMention}\n");
    }
}

public sealed class GetLevelSettings
{
    public sealed record Request : IRequest<Response?>
    {
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Request request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.GuildLevelSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    ModuleEnabled = x.ModuleEnabled,
                    TextTime = x.TextTime,
                    Base = x.Base,
                    Modifier = x.Modifier,
                    Amount = x.Amount,
                    LevelChannelLog = x.LevelChannelLogId
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public required bool ModuleEnabled { get; init; }
        public required TimeSpan TextTime { get; init; }
        public required int Base { get; init; }
        public required int Modifier { get; init; }
        public required int Amount { get; init; }
        public ulong? LevelChannelLog { get; init; }
    }
}
