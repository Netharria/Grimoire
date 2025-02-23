// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("View")]
    [Description("View the current moderation settings for this server.")]
    public async Task ViewSettingsAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new GetModerationSettings.Query { GuildId = ctx.Guild.Id });

        var banLog = response.PublicBanLog is null
            ? "None"
            : ctx.Guild.Channels.GetValueOrDefault(response.PublicBanLog.Value)?.Mention;

        var autoPardonString =
            response.AutoPardonAfter.Days % 365 == 0
                ? $"{response.AutoPardonAfter.Days / 365} years"
                : response.AutoPardonAfter.Days % 30 == 0
                    ? $"{response.AutoPardonAfter.Days / 30} months"
                    : $"{response.AutoPardonAfter.Days} days";

        await ctx.EditReplyAsync(
            title: "Current moderation System Settings",
            message: $"**Module Enabled:** {response.ModuleEnabled}\n" +
                     $"**Auto Pardon Duration:** {autoPardonString}\n" +
                     $"**Ban Log:** {banLog}\n");
    }
}

internal sealed class GetModerationSettings
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Query query,
            CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.GuildModerationSettings
                .AsNoTracking()
                .Where(x => x.GuildId == query.GuildId)
                .Select(x => new Response
                {
                    AutoPardonAfter = x.AutoPardonAfter,
                    PublicBanLog = x.PublicBanLog,
                    ModuleEnabled = x.ModuleEnabled
                }).FirstOrDefaultAsync(cancellationToken);

            if (result is null) throw new AnticipatedException("No settings were found for this server.");

            return result;
        }
    }

    public sealed record Response
    {
        public TimeSpan AutoPardonAfter { get; internal init; }
        public bool ModuleEnabled { get; internal init; }
        public ulong? PublicBanLog { get; internal init; }
    }
}
