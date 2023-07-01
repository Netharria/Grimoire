// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands.SetMuteRole;
using Grimoire.Core.Features.Moderation.Queries.GetAllActiveMutes;
using Grimoire.Core.Features.Moderation.Queries.GetMuteRole;
using Serilog;

namespace Grimoire.Discord.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
[SlashRequireBotPermissions(Permissions.ManageRoles)]
[SlashCommandGroup("Mutes", "Manages the mute role settings.")]
public partial class MuteAdminCommands : ApplicationCommandModule
{
    private readonly IMediator _mediator;
    private readonly ILogger _logger;

    public MuteAdminCommands(IMediator mediator, ILogger logger)
    {
        this._mediator = mediator;
        this._logger = logger;
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

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: $"Role {role.Mention} is created. Now Saving role to {ctx.Client.CurrentUser.Mention} configuration.");

        var response = await this._mediator.Send(new SetMuteRoleCommand
        {
            Role = role.Id,
            GuildId = ctx.Guild.Id
        });

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Role {role.Mention} is saved in {ctx.Client.CurrentUser.Mention} configuration. Now setting role permissions");
        var result = await this.SetMuteRolePermissionsAsync(ctx.Guild, role)
                .Where(x => !x.WasSuccessful)
                .ToArrayAsync();

        if (!result.Any())
        {
            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Successfully created role {role.Mention} and set permissions for channels");
        }
        else
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, $"Successfully created role {role.Mention} but, " +
                $"was not able to set permissions for the following channels. {string.Join(' ', result.Select(x => x.Channel.Mention))}");
        }
        
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
            await ctx.EditReplyAsync(GrimoireColor.Yellow, message: "Could not find configured mute role.");
            return;
        }
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Refreshing permissions for {role.Mention} role.");
        var result = await this.SetMuteRolePermissionsAsync(ctx.Guild, role)
                .Where(x => !x.WasSuccessful)
                .ToArrayAsync();

        if(!result.Any())
        {
            await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Succussfully refreshed permissions for {role.Mention} role.");
        }
        else
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, $"Was not able to set permissions for the following channels. " +
                $"{string.Join(' ', result.Select(x => x.Channel.Mention))}");
        }
        
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.Member.Mention} asked {ctx.Guild.CurrentMember} to refresh the permissions of mute role {role.Mention}");
    }

    private async IAsyncEnumerable<OverwriteChannelResult> SetMuteRolePermissionsAsync(DiscordGuild guild, DiscordRole role)
    {
        foreach (var (_, channel) in guild.Channels)
        {
            if (channel.Type == ChannelType.Text
                || channel.Type == ChannelType.Category
                || channel.Type == ChannelType.GuildForum)
            {
                yield return await this.OverwriteChannelAsync(channel, role);

            }
            else if (channel.Type == ChannelType.Voice)
            {
                yield return await this.OverwriteVoiceChannelAsync(channel, role);
            }
        }
    }

    private async Task<OverwriteChannelResult> OverwriteChannelAsync(DiscordChannel channel, DiscordRole role)
    {
        var permissions = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == role.Id);
        foreach (var tryAttempt in Enumerable.Range(1, 3))
        {
            try
            {
                if (permissions is not null)
                    await channel.AddOverwriteAsync(role,
                            permissions.Allowed.RevokeLockPermissions(),
                            permissions.Denied.SetLockPermissions());
                else
                    await channel.AddOverwriteAsync(role,
                            deny: PermissionValues.LockPermissions);
                return new OverwriteChannelResult
                {
                    WasSuccessful = true,
                    Channel = channel,
                };
            }
            catch (DiscordException ex) when (ex is BadRequestException or ServerErrorException)
            {
                if(tryAttempt < 3)
                {
                    this._logger.Warning("Exception was thrown while trying to overwrite channel mute permissions. " +
                        "Trying again. Attempt: ({attempt}) Exception Message : ({message})", tryAttempt, ex.Message);
                    await Task.Delay(1 * tryAttempt);
                }
                else
                {
                    this._logger.Error(ex, "Was not able to successfully overwrite channel mute permissions after ({attempt})" +
                        "Exception Message : ({message})", tryAttempt, ex.Message);
                }
            }
            catch (Exception ex)
            {
                this._logger.Error(ex, "A non transient exception was thrown while trying to overwrite channel mute permissions." +
                        "Exception Message : ({message})", tryAttempt, ex.Message);
                break;
            }

        }
        return new OverwriteChannelResult
        {
            WasSuccessful = false,
            Channel = channel,
        };
    }

    private async Task<OverwriteChannelResult> OverwriteVoiceChannelAsync(DiscordChannel channel, DiscordRole role)
    {
        var permissions = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == role.Id);

        foreach (var tryAttempt in Enumerable.Range(1, 3))
        {
            try
            {
                if (permissions is not null)
                    await channel.AddOverwriteAsync(role,
                            permissions.Allowed.RevokeVoiceLockPermissions(),
                            permissions.Denied.SetVoiceLockPermissions());
                else
                    await channel.AddOverwriteAsync(role,
                            deny: PermissionValues.VoiceLockPermissions);
                return new OverwriteChannelResult
                {
                    WasSuccessful = true,
                    Channel = channel,
                };
            }
            catch (DiscordException ex) when (ex is BadRequestException or ServerErrorException)
            {
                if (tryAttempt < 3)
                {
                    this._logger.Warning("Exception was thrown while trying to overwrite channel mute permissions. " +
                        "Trying again. Attempt: ({attempt}) Exception Message : ({message})", tryAttempt, ex.Message);
                    await Task.Delay(1 * tryAttempt);
                }
                else
                {
                    this._logger.Error(ex, "Was not able to successfully overwrite channel mute permissions after ({attempt})" +
                        "Exception Message : ({message})", tryAttempt, ex.Message);
                }
            }
            catch (Exception ex)
            {
                this._logger.Error(ex, "A non transient exception was thrown while trying to  overwrite channel mute permissions." +
                        "Exception Message : ({message})", tryAttempt, ex.Message);
                break;
            }

        }
        return new OverwriteChannelResult
        {
            WasSuccessful = false,
            Channel = channel,
        };
    }
}

