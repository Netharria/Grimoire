// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Responses;

namespace Grimoire.Discord.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserPermissions(Permissions.ManageMessages)]
[SlashRequireBotPermissions(Permissions.ManageChannels)]
public class LockCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("Lock", "Prevents users from being able to speak in the channel")]
    public async Task LockChannelAsync(
        InteractionContext ctx,
        [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
        [Minimum(0)]
        [Option("DurationAmount", "Select the amount of time the lock will last.")] long durationAmount,
        [ChannelTypes(ChannelType.Text, ChannelType.PublicThread, ChannelType.PrivateThread, ChannelType.Category, ChannelType.GuildForum)]
        [Option("Channel", "The Channel to lock. Current channel if not specified.")] DiscordChannel? channel = null,
        [MaximumLength(1000)]
        [Option("Reason", "The reason why the channel is getting locked")] string? reason = null)
    {
        await ctx.DeferAsync();
        channel ??= ctx.Channel;
        BaseResponse? response;

        if (channel.IsThread)
            response = await this.ThreadLockAsync(ctx, channel, reason, durationType, durationAmount);
        else if (channel.Type is ChannelType.Text
            || channel.Type is ChannelType.Category
            || channel.Type is ChannelType.GuildForum)
            response = await this.ChannelLockAsync(ctx, channel, reason, durationType, durationAmount);
        else
        {
            await ctx.EditReplyAsync(message: "Channel not of valid type.");
            return;
        }
        await ctx.EditReplyAsync(message: $"{channel.Mention} has been locked for {durationAmount} {durationType.GetName()}"); ;
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{channel.Mention} has been locked for {durationAmount} {durationType.GetName()} by {ctx.User.Mention}"
            + (string.IsNullOrWhiteSpace(reason) ? "" : $"for {reason}"));
    }

    private async Task<BaseResponse> ChannelLockAsync(InteractionContext ctx, DiscordChannel channel, string? reason, DurationType durationType, long durationAmount)
    {
        var previousSetting = ctx.Guild.GetChannel(channel.Id).PermissionOverwrites.First(x => x.Id == ctx.Guild.EveryoneRole.Id);
        var response = await this._mediator.Send(new LockChannelCommand
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

    private async Task<BaseResponse> ThreadLockAsync(InteractionContext ctx, DiscordChannel channel, string? reason, DurationType durationType, long durationAmount)
    {
        return await this._mediator.Send(new LockChannelCommand
        {
            ChannelId = channel.Id,
            ModeratorId = ctx.User.Id,
            GuildId = ctx.Guild.Id,
            Reason = reason ?? string.Empty,
            DurationType = durationType,
            DurationAmount = durationAmount
        });
    }

    [SlashCommand("Unlock", "Prevents users from being able to speak in the channel")]
    public async Task UnlockChannelAsync(
        InteractionContext ctx,
        [Option("Channel", "The Channel to unlock. Current channel if not specified.")] DiscordChannel? channel = null)
    {
        await ctx.DeferAsync();
        channel ??= ctx.Channel;
        var response = await this._mediator.Send(new UnlockChannelCommand { ChannelId = channel.Id, GuildId = ctx.Guild.Id });

        if (!channel.IsThread)
        {
            var permissions = channel.PermissionOverwrites.First(x => x.Id == ctx.Guild.EveryoneRole.Id);
            await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole,
                permissions.Allowed.RevertLockPermissions(response.PreviouslyAllowed)
                , permissions.Denied.RevertLockPermissions(response.PreviouslyDenied));
        }

        await ctx.EditReplyAsync(message: $"{channel.Mention} has been unlocked"); ;
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{channel.Mention} has been unlocked by {ctx.User.Mention}");
    }
}
