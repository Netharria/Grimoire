// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("AutoPardon")]
    [Description("Updates how long till sins are automatically pardoned.")]
    public async Task AutoPardonAsync(
        SlashCommandContext ctx,
        [Parameter("DurationType")]
        [Description("Select whether the duration will be in minutes hours or days")]
        Duration durationType,
        [MinMaxValue(0, int.MaxValue)]
        [Parameter("DurationAmount")]
        [Description("The amount of time before sins are auto pardoned.")]
        long durationAmount)
    {
        await ctx.DeferResponseAsync();

        if(ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new SetAutoPardon.Command
        {
            GuildId = ctx.Guild.Id, DurationAmount = durationType.GetTimeSpan(durationAmount)
        });

        await ctx.EditReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"Auto pardon was updated by {ctx.User.Mention} " +
                     $"to pardon sins after {durationAmount} {durationType}.");
    }
}

internal sealed class SetAutoPardon
{
    public sealed record Command : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public TimeSpan DurationAmount { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guildModerationSettings = await dbContext.GuildModerationSettings
                .Include(x => x.Guild)
                .FirstOrDefaultAsync(guildModerationSettings => guildModerationSettings.GuildId.Equals(command.GuildId),
                    cancellationToken);
            if (guildModerationSettings is null)
                throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.AutoPardonAfter = command.DurationAmount;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
        }
    }
}
