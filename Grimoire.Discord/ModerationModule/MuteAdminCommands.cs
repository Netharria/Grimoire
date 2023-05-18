// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands.SetMuteRole;
using Grimoire.Core.Features.Moderation.Queries.GetAllActiveMutes;
using Grimoire.Core.Features.Moderation.Queries.GetMuteRole;

namespace Grimoire.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    [SlashCommandGroup("Mutes", "Manages the mute role settings.")]
    public class MuteAdminCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public MuteAdminCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("View", "View the current configured mute role and any active mutes.")]
        public async Task ViewMutesAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetAllActiveMutesQuery{ GuildId = ctx.Guild.Id });

            DiscordRole? role = null;
            if (response.MuteRole is not null)
            {
                role = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole.Value);
            }
            var users = ctx.Guild.Members.Where(x => response.MutedUsers.Contains(x.Key))
                .Select(x => x.Value).ToArray();
            var embed = new DiscordEmbedBuilder();

            if (role is not null)
                embed.AddField("Mute Role", role.Mention);
            else
                embed.AddField("Mute Role", "None");
            if (users is not null && users.Any())
                embed.AddField("Muted Users", string.Join(" ", users.Select(x => x.Mention)));
            else
                embed.AddField("Muted Users", "None");


            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(embed));
        }

        [SlashCommand("Set", "Sets the role that is used for muting users.")]
        public async Task SetMuteRoleAsync(
            InteractionContext ctx,
            [Option("Role", "The role to use for muting")] DiscordRole role)
        {
            var response = await this._mediator.Send(new SetMuteRoleCommand
            {
                Role = role.Id,
                GuildId = ctx.Guild.Id
            });

            await ctx.ReplyAsync(message: $"Will now use role {role.Mention} for muting users.", ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.Member.Mention} updated the mute role to {role.Mention}");
        }

        [SlashCommand("Create", "Creates a new role to be use for muting users and set permissions in all channels.")]
        public async Task CreateMuteRoleAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var role = await ctx.Guild.CreateRoleAsync("Muted");

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Role {role.Mention} is created. Now Saving role to {ctx.Client.CurrentUser.Mention} configuration."));

            var response = await this._mediator.Send(new SetMuteRoleCommand
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
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Successfully created role {role.Mention} and set permissions for channels"));
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{ctx.Member.Mention} asked {ctx.Guild.CurrentMember} to create {role.Mention} to use as a mute role.");
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
                await ctx.ReplyAsync(GrimoireColor.Yellow, message: "Could not find configured mute role.");
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
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{ctx.Member.Mention} asked {ctx.Guild.CurrentMember} to refresh the permissions of mute role {role.Mention}");
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
                    .SelectAwait(async x =>
                    {
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
                    .SelectAwait(async x =>
                    {
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
