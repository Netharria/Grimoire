// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Moderation.Commands.MuteCommands.SetMuteRole;
using Cybermancy.Core.Features.Moderation.Queries.GetMuteRole;

namespace Cybermancy.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    [SlashCommandGroup("MuteAdmin", "Manages the Mute role.")]
    public class MuteAdminCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public MuteAdminCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Set", "Sets the role that is used for muting users.")]
        public async Task SetMuteRoleAsync(
            InteractionContext ctx,
            [Option("Role", "The role to use for muting")] DiscordRole role)
        {
            await this._mediator.Send(new SetMuteRoleCommand
            {
                Role = role.Id,
                GuildId = ctx.Guild.Id
            });

            await ctx.ReplyAsync(message: $"Will now use role {role.Name} for muting users.", ephemeral: false);
        }

        [SlashCommand("Create", "Creates a new role to be use for muting users and set permissions in all channels.")]
        public async Task CreateMuteRoleAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var role = await ctx.Guild.CreateRoleAsync("Muted");

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Role {role.Mention} is created. Now Saving role to {ctx.Client.CurrentUser.Mention} configuration."));

            await this._mediator.Send(new SetMuteRoleCommand
            {
                Role = role.Id,
                GuildId = ctx.Guild.Id
            });

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Role {role.Mention} is saved in {ctx.Client.CurrentUser.Mention} configuration. Now setting role permissions"));
            try
            {
                await SetMuteRolePermissionsAsync(ctx.Guild, role);
            }
            catch (Exception)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Error occured when setting role permissions for {role.Mention}."));
                throw;
            }
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Succussfully created role {role.Mention} and set permissions for channels"));
        }

        [SlashCommand("Refresh", "Refreshes the permissions of the currently configured mute role.")]
        public async Task RefreshMuteRoleAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var response = await this._mediator.Send(new GetMuteRoleQuery
            {
                GuildId = ctx.Guild.Id
            });

            if (!ctx.Guild.Roles.TryGetValue(response.RoleId, out var role))
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "Could not find configured mute role.");
                return;
            }
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Refreshing permissions for {role.Mention} role."));
            try
            {
                await SetMuteRolePermissionsAsync(ctx.Guild, role);
            }
            catch (Exception)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Error occured when setting role permissions for {role.Mention}."));
                throw;
            }
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Succussfully refreshed permissions for {role.Mention} role."));
        }

        private static async Task SetMuteRolePermissionsAsync(DiscordGuild guild, DiscordRole role)
        {
            foreach (var (_, channel) in guild.Channels)
            {
                if (channel.Type == ChannelType.Text
                    || channel.Type == ChannelType.Category
                    || channel.Type == ChannelType.PrivateThread
                    || channel.Type == ChannelType.PublicThread)
                {
                    await channel.ModifyAsync(editModel => editModel.PermissionOverwrites = channel.PermissionOverwrites.ToAsyncEnumerable()
                    .SelectAwait(async x => {
                        if (x.Type == OverwriteType.Role)
                            return await new DiscordOverwriteBuilder(await x.GetRoleAsync()).FromAsync(x);
                        return await new DiscordOverwriteBuilder(await x.GetMemberAsync()).FromAsync(x);
                    })
                    .Select(x =>
                    {
                        if (x.Target.Id == role.Id)
                        {
                            x.Denied.SetLockPermissions();
                        }
                        return x;
                    }).ToEnumerable());
                }
                else if (channel.Type == ChannelType.Voice)
                {
                    await channel.ModifyAsync(editModel => editModel.PermissionOverwrites = channel.PermissionOverwrites.ToAsyncEnumerable()
                    .SelectAwait(async x => {
                        if (x.Type == OverwriteType.Role)
                            return await new DiscordOverwriteBuilder(await x.GetRoleAsync()).FromAsync(x);
                        return await new DiscordOverwriteBuilder(await x.GetMemberAsync()).FromAsync(x);
                    })
                    .Select(x =>
                    {
                        if (x.Target.Id == role.Id)
                        {
                            x.Deny(Permissions.Speak);
                        }
                        return x;
                    }).ToEnumerable());
                }
            }
        }
    }
}
