// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Lock.Commands;

public sealed class LockChannel
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserPermissions(DiscordPermissions.ManageMessages)]
    [SlashRequireBotPermissions(DiscordPermissions.ManageChannels)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Lock", "Prevents users from being able to speak in the channel")]
        public async Task LockChannelAsync(
            InteractionContext ctx,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")]
            DurationType durationType,
            [Minimum(0)] [Option("DurationAmount", "Select the amount of time the lock will last.")]
            long durationAmount,
            [ChannelTypes(DiscordChannelType.Text, DiscordChannelType.PublicThread, DiscordChannelType.PrivateThread,
                DiscordChannelType.Category, DiscordChannelType.GuildForum)]
            [Option("Channel", "The Channel to lock. Current channel if not specified.")]
            DiscordChannel? channel = null,
            [MaximumLength(1000)] [Option("Reason", "The reason why the channel is getting locked")]
            string? reason = null)
        {
            await ctx.DeferAsync();
            channel ??= ctx.Channel;
            BaseResponse? response;

            if (channel.IsThread)
                response = await this.ThreadLockAsync(ctx, channel, reason, durationType, durationAmount);
            else if (channel.Type is DiscordChannelType.Text
                     or DiscordChannelType.Category
                     or DiscordChannelType.GuildForum)
                response = await this.ChannelLockAsync(ctx, channel, reason, durationType, durationAmount);
            else
            {
                await ctx.EditReplyAsync(message: "Channel not of valid type.");
                return;
            }

            await ctx.EditReplyAsync(
                message: $"{channel.Mention} has been locked for {durationAmount} {durationType.GetName()}");

            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message:
                $"{channel.Mention} has been locked for {durationAmount} {durationType.GetName()} by {ctx.User.Mention}"
                + (string.IsNullOrWhiteSpace(reason) ? "" : $"for {reason}"));
        }

        private async Task<BaseResponse> ChannelLockAsync(InteractionContext ctx, DiscordChannel channel,
            string? reason,
            DurationType durationType, long durationAmount)
        {
            var previousSetting = ctx.Guild.Channels[channel.Id].PermissionOverwrites
                .First(x => x.Id == ctx.Guild.EveryoneRole.Id);
            var response = await this._mediator.Send(new Request
            {
                ChannelId = channel.Id,
                PreviouslyAllowed = previousSetting.Allowed.GetLockPermissions().ToLong(),
                PreviouslyDenied = previousSetting.Denied.GetLockPermissions().ToLong(),
                ModeratorId = ctx.User.Id,
                GuildId = ctx.Guild.Id,
                Reason = reason ?? string.Empty,
                DurationType = durationType,
                DurationAmount = durationAmount
            });
            await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole,
                previousSetting.Allowed.RevokeLockPermissions(),
                previousSetting.Denied.SetLockPermissions());

            return response;
        }

        private async Task<BaseResponse> ThreadLockAsync(InteractionContext ctx, DiscordChannel channel, string? reason,
            DurationType durationType, long durationAmount) =>
            await this._mediator.Send(new Request
            {
                ChannelId = channel.Id,
                ModeratorId = ctx.User.Id,
                GuildId = ctx.Guild.Id,
                Reason = reason ?? string.Empty,
                DurationType = durationType,
                DurationAmount = durationAmount
            });
    }

    public sealed record Request : IRequest<BaseResponse>
    {
        public ulong ChannelId { get; init; }
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
        public ulong ModeratorId { get; init; }
        public ulong GuildId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DurationType DurationType { get; init; }
        public long DurationAmount { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            var lockEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);

            var result = await this._grimoireDbContext.Channels
                .Where(x => x.GuildId == command.GuildId)
                .Where(x => x.Id == command.ChannelId)
                .Select(x => new { x.Lock, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find that channel");

            if (result.Lock is not null)
            {
                result.Lock.ModeratorId = command.ModeratorId;
                result.Lock.EndTime = lockEndTime;
                result.Lock.Reason = command.Reason;
            }
            else
            {
                var local = this._grimoireDbContext.Locks.Local.FirstOrDefault(x => x.ChannelId == command.ChannelId);
                if (local is not null)
                    this._grimoireDbContext.Entry(local).State = EntityState.Detached;
                var lockToAdd = new Domain.Lock
                {
                    ChannelId = command.ChannelId,
                    GuildId = command.GuildId,
                    Reason = command.Reason,
                    EndTime = lockEndTime,
                    ModeratorId = command.ModeratorId,
                    PreviouslyAllowed = command.PreviouslyAllowed,
                    PreviouslyDenied = command.PreviouslyDenied
                };
                await this._grimoireDbContext.Locks.AddAsync(lockToAdd, cancellationToken);
            }

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = result.ModChannelLog };
        }
    }
}
