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
        [Parameter("PermissionRole_10")] DiscordRole? permissionRole10 = null)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.Guild!;
        var guildId = guild.GetGuildId();
        var roleIds = CollectRoleIds(permissionRole1, permissionRole2, permissionRole3, permissionRole4, permissionRole5,
            permissionRole6, permissionRole7, permissionRole8, permissionRole9, permissionRole10);

        if (ValidateRoles(restrictedUse, roleIds) is not Validation<IReadOnlyList<RoleId>>.Valid { Value: var roles })
        {
            await ctx.SendWarningResponseAsync("Command set as restricted but no roles allowed to use it.");
            return;
        }

        await SaveCommandAsync(BuildCommand(name, guildId, content, embed, embedColor, restrictedUse, roles, ctx.GetModeratorId()))
            .Match(
                _ => OnLearnSuccessAsync(ctx, guild, name),
                errors => ctx.SendErrorResponseAsync(errors[0].Message).AsTask());
    }

    private static IReadOnlyList<RoleId> CollectRoleIds(params DiscordRole?[] roles)
        => roles.OfType<DiscordRole>().Select(r => r.GetRoleId()).Distinct().ToList();

    private static Validation<IReadOnlyList<RoleId>> ValidateRoles(bool restrictedUse, IReadOnlyList<RoleId> roleIds)
        => restrictedUse && roleIds.Count == 0
            ? Validation<IReadOnlyList<RoleId>>.Fail(new Error("command.learn.no_roles",
                "Command set as restricted but no roles allowed to use it."))
            : Validation<IReadOnlyList<RoleId>>.Succeed(roleIds);

    private static CustomCommand BuildCommand(CustomCommandName name, GuildId guildId, string content, bool embed,
        CustomCommandEmbedColor? embedColor, bool restrictedUse, IReadOnlyList<RoleId> roleIds, ModeratorId? moderatorId)
    {
        var now = DateTimeOffset.UtcNow;
        return new CustomCommand
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = now,
            Content = content,
            IsEmbedded = embed,
            EmbedColor = embedColor,
            RestrictedUse = restrictedUse,
            ModeratorId = moderatorId,
            Roles = [.. roleIds.Select(roleId => new CustomCommandRole
            {
                Name = name, GuildId = guildId, CreatedAt = now, RoleId = roleId
            })]
        };
    }

    private async Task<Result<CustomCommand>> SaveCommandAsync(CustomCommand command)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.AddAsync(command);
            await dbContext.SaveChangesAsync();
            return Result<CustomCommand>.Ok(command);
        }
        catch (DbUpdateException)
        {
            return Result<CustomCommand>.Fail(new Error("command.learn.db_error",
                "Could not save that command right now due to a database error. Please try again."));
        }
    }

    private async Task OnLearnSuccessAsync(CommandContext ctx, DiscordGuild guild, CustomCommandName name)
    {
        await ctx.ReplyAsync(GrimoireColor.Green, $"Added {name} custom command.");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} asked {guild.CurrentMember!.Mention} to learn a new command: {name}",
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation
        });
    }
}
