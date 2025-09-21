// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Moderation.Lock.Commands;

public sealed class LockChannel
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly IMediator _mediator = mediator;

        [Command("Lock")]
        [Description("Locks a channel for a specified amount of time.")]
        public async Task LockChannelAsync(
            SlashCommandContext ctx,
            [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days.")]
            DurationType durationType,
            [MinMaxValue(0)] [Parameter("DurationAmount")] [Description("The amount of time the lock will last.")]
            int durationAmount,
            [ChannelTypes(DiscordChannelType.Text, DiscordChannelType.PublicThread, DiscordChannelType.PrivateThread,
                DiscordChannelType.Category, DiscordChannelType.GuildForum)]
            [Parameter("Channel")]
            [Description("The channel to lock. Current channel if not specified.")]
            DiscordChannel? channel = null,
            [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason for the lock.")]
            string? reason = null)
        {
            await ctx.DeferResponseAsync();
            channel ??= ctx.Channel;

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            if (channel.IsThread)
                await ThreadLockAsync(ctx.Guild, ctx.User, channel, reason, durationType, durationAmount);
            else if (channel.Type is DiscordChannelType.Text
                     or DiscordChannelType.Category
                     or DiscordChannelType.GuildForum)
                await ChannelLockAsync(ctx.Guild, ctx.User, channel, reason, durationType, durationAmount);
            else
            {
                await ctx.EditReplyAsync(message: "Channel not of valid type.");
                return;
            }

            await ctx.EditReplyAsync(
                message: $"{channel.Mention} has been locked for {durationAmount} {durationType}");

            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Purple,
                Description =
                    $"{channel.Mention} has been locked for {durationAmount} {durationType} by {ctx.User.Mention}"
                    + (string.IsNullOrWhiteSpace(reason) ? "" : $" for {reason}")
            });
        }

        private async Task ChannelLockAsync(DiscordGuild guild, DiscordUser moderator, DiscordChannel channel,
            string? reason,
            DurationType durationType, long durationAmount)
        {
            var previousSetting = guild.Channels[channel.Id].PermissionOverwrites
                .First(x => x.Id == guild.EveryoneRole.Id);
            await this._mediator.Send(new Request
            {
                ChannelId = channel.Id,
                PreviouslyAllowed = previousSetting.Allowed.GetLockPermissions().ToLong(),
                PreviouslyDenied = previousSetting.Denied.GetLockPermissions().ToLong(),
                ModeratorId = moderator.Id,
                GuildId = guild.Id,
                Reason = reason ?? string.Empty,
                DurationType = durationType,
                DurationAmount = durationAmount
            });
            await channel.AddOverwriteAsync(guild.EveryoneRole,
                previousSetting.Allowed.RevokeLockPermissions(),
                previousSetting.Denied.SetLockPermissions());
        }

        private async Task ThreadLockAsync(DiscordGuild guild, DiscordUser moderator, DiscordChannel channel,
            string? reason,
            DurationType durationType, long durationAmount) =>
            await this._mediator.Send(new Request
            {
                ChannelId = channel.Id,
                ModeratorId = moderator.Id,
                GuildId = guild.Id,
                Reason = reason ?? string.Empty,
                DurationType = durationType,
                DurationAmount = durationAmount
            });
    }

    public sealed record Request : IRequest
    {
        public ChannelId ChannelId { get; init; }
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
        public ulong ModeratorId { get; init; }
        public GuildId GuildId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DurationType DurationType { get; init; }
        public long DurationAmount { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Request command, CancellationToken cancellationToken)
        {
            var lockEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Channels
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
                var local = dbContext.Locks.Local.FirstOrDefault(x => x.ChannelId == command.ChannelId);
                if (local is not null)
                    dbContext.Entry(local).State = EntityState.Detached;
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
                await dbContext.Locks.AddAsync(lockToAdd, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
