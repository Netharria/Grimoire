// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{

    [SlashCommand("AutoPardon", "Updates how long till sins are pardoned.")]
    public async Task AutoPardonAsync(
        InteractionContext ctx,
        [Option("DurationType", "Select whether the duration will be in minutes hours or days")]
        Duration durationType,
        [Maximum(int.MaxValue)]
        [Minimum(0)]
        [Option("DurationAmount", "Select the amount of time before sins are auto pardoned.")]
        long durationAmount)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new SetAutoPardon.Command
        {
            GuildId = ctx.Guild.Id, DurationAmount = durationType.GetTimeSpan(durationAmount)
        });

        await ctx.EditReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType.GetName()}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"Auto pardon was updated by {ctx.User.Mention} " +
                     $"to pardon sins after {durationAmount} {durationType.GetName()}.");
    }
}

internal sealed class SetAutoPardon
{
    public sealed record Command : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public TimeSpan DurationAmount { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
                .Include(x => x.Guild)
                .FirstOrDefaultAsync(guildModerationSettings => guildModerationSettings.GuildId.Equals(command.GuildId),
                    cancellationToken);
            if (guildModerationSettings is null)
                throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.AutoPardonAfter = command.DurationAmount;

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
        }
    }

}
