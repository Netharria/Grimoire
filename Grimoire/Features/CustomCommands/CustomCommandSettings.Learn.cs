// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    //todo: fix this when variadic arguments are fixed
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Learn")]
    [Description("Learn a new command or update an existing one")]
    public async Task Learn(
        CommandContext ctx,
        [Parameter("Name")]
        [Description("The name that the command will be called. This is used to activate the command.")]
        CustomCommandName name,
        [MinMaxLength(maxLength: 2000)]
        [Parameter("Content")]
        [Description("The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Parameter("Embed")] [Description("Put the message in an embed")]
        bool embed = false,
        [Parameter("EmbedColor")] [Description("Hexadecimal color of the embed")]
        CustomCommandEmbedColor? embedColor = null,
        [Parameter("RestrictedUse")] [Description("Only explicitly allowed roles can use this command")]
        bool restrictedUse = false,
        [Parameter("PermissionRole_1")] DiscordRole? permissionRole1 = null,
        [Parameter("PermissionRole_2")] DiscordRole? permissionRole2 = null,
        [Parameter("PermissionRole_3")] DiscordRole? permissionRole3 = null,
        [Parameter("PermissionRole_4")] DiscordRole? permissionRole4 = null,
        [Parameter("PermissionRole_5")] DiscordRole? permissionRole5 = null,
        [Parameter("PermissionRole_6")] DiscordRole? permissionRole6 = null,
        [Parameter("PermissionRole_7")] DiscordRole? permissionRole7 = null,
        [Parameter("PermissionRole_8")] DiscordRole? permissionRole8 = null,
        [Parameter("PermissionRole_9")] DiscordRole? permissionRole9 = null,
        [Parameter("PermissionRole_10")] DiscordRole? permissionRole10 = null
    )
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("This command can only be used in a server.");
            return;
        }

        var guildId = guild.GetGuildId();
        var hasMention = content.Contains("%mention", StringComparison.OrdinalIgnoreCase);
        var hasMessage = content.Contains("%message", StringComparison.OrdinalIgnoreCase);

        var roleIds = new[]
            {
                permissionRole1, permissionRole2, permissionRole3, permissionRole4, permissionRole5,
                permissionRole6, permissionRole7, permissionRole8, permissionRole9, permissionRole10
            }
            .OfType<DiscordRole>()
            .Select(r => r.GetRoleId())
            .Distinct()
            .ToList();

        if (restrictedUse && roleIds.Count == 0)
        {
            await ctx.SendWarningResponseAsync("Command set as restricted but no roles allowed to use it.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var command = new CustomCommand
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = now,
            Content = content,
            HasMention = hasMention,
            HasMessage = hasMessage,
            IsEmbedded = embed,
            EmbedColor = embedColor,
            RestrictedUse = restrictedUse,
            ModeratorId = ctx.GetModeratorId(),
            Roles =
            [
                .. roleIds.Select(roleId =>
                    new CustomCommandRole { Name = name, GuildId = guildId, CreatedAt = now, RoleId = roleId })
            ]
        };

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        await dbContext.AddAsync(command);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await ctx.SendErrorResponseAsync(
                "Could not save that command right now due to a database error. Please try again.");
            return;
        }

        await ctx.ReplyAsync(GrimoireColor.Green, $"Added {name} custom command.");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            Color = GrimoireColor.Purple,
            Description =
                $"{ctx.User.Mention} asked {ctx.Guild.CurrentMember.Mention} to learn a new command: {name}",
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation
        });
    }
}
