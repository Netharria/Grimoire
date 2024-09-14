// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Features.Moderation.Queries;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
[SlashRequireBotPermissions(Permissions.ManageRoles)]
[SlashCommandGroup("Mutes", "Manages the mute role settings.")]
public sealed partial class MuteAdminCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("View", "View the current configured mute role and any active mutes.")]
    public async Task ViewMutesAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);
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
        if (users is not null && users.Length != 0)
            embed.AddField("Muted Users", string.Join(" ", users.Select(x => x.Mention)));
        else
            embed.AddField("Muted Users", "None");


        await ctx.EditReplyAsync(embed: embed);
    }

    [SlashCommand("Set", "Sets the role that is used for muting users.")]
    public async Task SetMuteRoleAsync(
        InteractionContext ctx,
        [Option("Role", "The role to use for muting")] DiscordRole role)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new SetMuteRoleCommand
        {
            Role = role.Id,
            GuildId = ctx.Guild.Id
        });

        await ctx.EditReplyAsync(message: $"Will now use role {role.Mention} for muting users.");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.Member.Mention} updated the mute role to {role.Mention}");
    }

    [SlashCommand("Create", "Creates a new role to be use for muting users and set permissions in all channels.")]
    public async Task CreateMuteRoleAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync();
        var role = await ctx.Guild.CreateRoleAsync("Muted");

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, message: $"Role {role.Mention} is created. Now Saving role to {ctx.Client.CurrentUser.Mention} configuration.");

        var response = await this._mediator.Send(new SetMuteRoleCommand
        {
            Role = role.Id,
            GuildId = ctx.Guild.Id
        });

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Role {role.Mention} is saved in {ctx.Client.CurrentUser.Mention} configuration. Now setting role permissions");
        var result = await SetMuteRolePermissionsAsync(ctx.Guild, role)
                .Where(x => !x.WasSuccessful)
                .ToArrayAsync();

        if (result.Length == 0)
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
        await ctx.DeferAsync();

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
        var result = await SetMuteRolePermissionsAsync(ctx.Guild, role)
                .Where(x => !x.WasSuccessful)
                .ToArrayAsync();

        if (result.Length == 0)
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

    private static async IAsyncEnumerable<OverwriteChannelResult> SetMuteRolePermissionsAsync(DiscordGuild guild, DiscordRole role)
    {
        foreach (var (_, channel) in guild.Channels)
        {
            if (channel.Type == ChannelType.Text
                || channel.Type == ChannelType.Category
                || channel.Type == ChannelType.GuildForum)
            {
                yield return await OverwriteChannelAsync(channel, role);

            }
            else if (channel.Type == ChannelType.Voice)
            {
                yield return await OverwriteVoiceChannelAsync(channel, role);
            }
        }
    }

    private static async Task<OverwriteChannelResult> OverwriteChannelAsync(DiscordChannel channel, DiscordRole role)
    {
        var permissions = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == role.Id);
        return await DiscordRetryPolicy.RetryDiscordCall(async () =>
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
        }, new OverwriteChannelResult
        {
            WasSuccessful = false,
            Channel = channel,
        });
    }

    [LoggerMessage(LogLevel.Warning, "Exception was thrown while trying to overwrite channel mute permissions. " +
                        "Trying again. Attempt: ({attempt}) Exception Message : ({message})")]
    public static partial void LogRetryAttempt(ILogger logger, int attempt, string message);

    [LoggerMessage(LogLevel.Error, "Was not able to successfully overwrite channel mute permissions after ({attempt})" +
                        "Exception Message : ({message})")]
    public static partial void LogRetriesExhaustedError(ILogger logger, DiscordException ex, int attempt, string message);

    [LoggerMessage(LogLevel.Error, "A non transient exception was thrown while trying to overwrite channel mute permissions." +
                        "Exception Message : ({message})")]
    public static partial void LogNonTransientError(ILogger logger, Exception ex, string message);

    private static async Task<OverwriteChannelResult> OverwriteVoiceChannelAsync(DiscordChannel channel, DiscordRole role)
    {
        var permissions = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == role.Id);

        return await DiscordRetryPolicy.RetryDiscordCall(async () =>
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
        }, new OverwriteChannelResult
        {
            WasSuccessful = false,
            Channel = channel,
        });
    }
}

