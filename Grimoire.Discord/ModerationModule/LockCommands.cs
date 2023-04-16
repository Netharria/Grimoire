// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Responses;
using Grimoire.Core.Features.Moderation.Commands.LockCommands.LockChannel;
using Grimoire.Core.Features.Moderation.Commands.LockCommands.UnlockChannelCommand;

namespace Grimoire.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageMessages)]
    public class LockCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public LockCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Lock", "Prevents users from being able to speak in the channel")]
        public async Task LockChannelAsync(
            InteractionContext ctx,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
            [Minimum(0)]
            [Option("DurationAmount", "Select the amount of time the logging will last.")] long durationAmount,
            [ChannelTypes(ChannelType.Text, ChannelType.PublicThread, ChannelType.PrivateThread, ChannelType.Category, ChannelType.Forum)]
            [Option("Channel", "The Channel to lock. Current channel if not specified.")] DiscordChannel? channel = null,
            [Option("Reason", "The reason why the channel is getting locked")] string? reason = null)
        {
            channel ??= ctx.Channel;
            BaseResponse? response = null;

            if (channel.IsThread)
                response = await this.ThreadLockAsync(ctx, channel, reason, durationType, durationAmount);
            else if (channel.Type is ChannelType.Text
                || channel.Type is ChannelType.Category
                || channel.Type is ChannelType.Forum)
                response = await this.ChannelLockAsync(ctx, channel, reason, durationType, durationAmount);
            else
            {
                await ctx.ReplyAsync(message: "Channel not of valid type.");
                return;
            }
            await ctx.ReplyAsync(message: $"{channel.Mention} has been locked for {durationAmount} {durationType.GetName()}", ephemeral: false); ;
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{channel.Mention} has been locked for {durationAmount} {durationType.GetName()} by {ctx.User.Mention}");
        }

        private async Task<BaseResponse> ChannelLockAsync(InteractionContext ctx, DiscordChannel channel, string? reason, DurationType durationType, long durationAmount)
        {
            var previousSetting = channel.PermissionOverwrites.First(x => x.Id == ctx.Guild.EveryoneRole.Id);

            await channel.ModifyAsync(editModel => editModel.PermissionOverwrites = channel.PermissionOverwrites.ToAsyncEnumerable()
                .SelectAwait(async x => {
                    if (x.Type == OverwriteType.Role)
                        return await new DiscordOverwriteBuilder(await x.GetRoleAsync()).FromAsync(x);
                    return await new DiscordOverwriteBuilder(await x.GetMemberAsync()).FromAsync(x);
                })
                .Select(x =>
                {
                    if (x.Target.Id == ctx.Guild.EveryoneRole.Id)
                    {
                        x.Denied.SetLockPermissions();
                    }
                    return x;
                }).ToEnumerable());

            return await _mediator.Send(new LockChannelCommand{
                ChannelId = channel.Id,
                PreviouslyAllowed = previousSetting.Allowed.GetLockPermissions().ToLong(),
                PreviouslyDenied = previousSetting.Denied.GetLockPermissions().ToLong(),
                ModeratorId = ctx.User.Id,
                GuildId = ctx.Guild.Id,
                Reason = reason ?? string.Empty,
                DurationType = durationType,
                DurationAmount = durationAmount
            });
        }

        private async Task<BaseResponse> ThreadLockAsync(InteractionContext ctx, DiscordChannel channel, string? reason, DurationType durationType, long durationAmount)
        {
            return await _mediator.Send(new LockChannelCommand
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
            channel ??= ctx.Channel;
            var response = await _mediator.Send(new UnlockChannelCommand { ChannelId = channel.Id, GuildId = ctx.Guild.Id });

            if(!channel.IsThread)
                await channel.ModifyAsync(editModel => editModel.PermissionOverwrites = channel.PermissionOverwrites.ToAsyncEnumerable()
                .SelectAwait(async x => {
                    if (x.Type == OverwriteType.Role)
                        return await new DiscordOverwriteBuilder(await x.GetRoleAsync()).FromAsync(x);
                    return await new DiscordOverwriteBuilder(await x.GetMemberAsync()).FromAsync(x);
                    })
                .Select(x =>
                {
                    if(x.Target.Id == ctx.Guild.EveryoneRole.Id)
                    {
                        x.Allowed.RevertLockPermissions(response.PreviouslyAllowed);
                        x.Denied.RevertLockPermissions(response.PreviouslyDenied);
                    }
                    return x;
                }).ToEnumerable());

            await ctx.ReplyAsync(message: $"{channel.Mention} has been unlocked", ephemeral: false); ;
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{channel.Mention} has been unlocked by {ctx.User.Mention}");
        }
    }
}
