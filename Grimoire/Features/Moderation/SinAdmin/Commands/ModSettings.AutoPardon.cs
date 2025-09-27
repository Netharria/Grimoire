// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("AutoPardon")]
    [Description("Updates how long till sins are automatically pardoned.")]
    public async Task AutoPardonAsync(
        SlashCommandContext ctx,
        [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days")]
        Duration durationType,
        [MinMaxValue(0, int.MaxValue)]
        [Parameter("DurationAmount")]
        [Description("The amount of time before sins are auto pardoned.")]
        int durationAmount)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        await this._mediator.Send(new SetAutoPardon.Command
        {
            GuildId = ctx.Guild.Id, DurationAmount = durationType.GetTimeSpan(durationAmount)
        });

        await ctx.EditReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} updated auto pardon to {durationAmount} {durationType}"
        });
    }
}

internal sealed class SetAutoPardon
{
    public sealed record Command : IRequest
    {
        public GuildId GuildId { get; init; }
        public TimeSpan DurationAmount { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guildModerationSettings = await dbContext.GuildModerationSettings
                .FirstOrDefaultAsync(guildModerationSettings => guildModerationSettings.GuildId.Equals(command.GuildId),
                    cancellationToken);
            if (guildModerationSettings is null)
                throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.AutoPardonAfter = command.DurationAmount;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
