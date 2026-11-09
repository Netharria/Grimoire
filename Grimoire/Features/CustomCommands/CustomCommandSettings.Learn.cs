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
        SlashCommandContext ctx,
        // A string, not the converter, so the user hears what's wrong with it. The converter only says it failed.
        [MinMaxLength(1, 24)]
        [Parameter("name")]
        [Description("The name that the command will be called. This is used to activate the command.")]
        string name,
        [MinMaxLength(maxLength: 2000)]
        [Parameter("content")]
        [Description("The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Parameter("embed")] [Description("Put the message in an embed")]
        bool embed = false,
        [Parameter("embed_color")]
        [Description("Hexadecimal color of the embed, such as ff0000. Only used when embed is true.")]
        string? embedColor = null,
        [Parameter("restricted_use")] [Description("Restrict this command to specific roles.")]
        bool restrictedUse = false,
        [Parameter("permission_role_1")] DiscordRole? permissionRole1 = null,
        [Parameter("permission_role_2")] DiscordRole? permissionRole2 = null,
        [Parameter("permission_role_3")] DiscordRole? permissionRole3 = null,
        [Parameter("permission_role_4")] DiscordRole? permissionRole4 = null,
        [Parameter("permission_role_5")] DiscordRole? permissionRole5 = null,
        [Parameter("permission_role_6")] DiscordRole? permissionRole6 = null,
        [Parameter("permission_role_7")] DiscordRole? permissionRole7 = null,
        [Parameter("permission_role_8")] DiscordRole? permissionRole8 = null,
        [Parameter("permission_role_9")] DiscordRole? permissionRole9 = null,
        [Parameter("permission_role_10")] DiscordRole? permissionRole10 = null)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var guildId = guild.GetGuildId();
        var roleIds = CollectRoleIds(permissionRole1, permissionRole2, permissionRole3, permissionRole4,
            permissionRole5,
            permissionRole6, permissionRole7, permissionRole8, permissionRole9, permissionRole10);

        await CustomCommandName.Create(name)
            .Bind(validName => ParseEmbedColor(embed, embedColor)
                .Bind(color => AllowRoleIds(restrictedUse, roleIds)
                    .Bind(allowRoleIds => CustomCommandContent.Create(content)
                        .Map(CustomCommand (validContent) => embed
                            ? EmbedCustomCommand.Create(validName, guildId, validContent, color, allowRoleIds,
                                ctx.GetModeratorId(), DateTimeOffset.UtcNow)
                            : TextCustomCommand.Create(validName, guildId, validContent, allowRoleIds,
                                ctx.GetModeratorId(), DateTimeOffset.UtcNow)))))
            .ToResult()
            .Match(
                async command =>
                {
                    await this.SaveCommandAsync(command);
                    await OnLearnSuccessAsync(ctx, guild, command.Name);
                },
                error => ctx.SendErrorResponseAsync(error.Message).AsTask());
    }

    /// <summary>A colour only means something on an embed, so one given without it is refused rather than dropped.</summary>
    private static Validation<CustomCommandEmbedColor?> ParseEmbedColor(bool embed, string? embedColor)
        => embedColor switch
        {
            null => Validation<CustomCommandEmbedColor?>.Succeed(null),
            _ when !embed => Validation<CustomCommandEmbedColor?>.Fail(new Error("command.learn.color-without-embed",
                "An embed color only applies to an embed. Set embed to true, or leave out the color.")),
            _ => CustomCommandEmbedColor.Create(embedColor).Map(color => (CustomCommandEmbedColor?)color)
        };

    private static IReadOnlyList<RoleId> CollectRoleIds(params DiscordRole?[] roles)
        => roles.OfType<DiscordRole>().Select(r => r.GetRoleId()).Distinct().ToList();

    private static Validation<IReadOnlyList<RoleId>> AllowRoleIds(bool restrictedUse, IReadOnlyList<RoleId> roleIds)
        => restrictedUse switch
        {
            false => Validation<IReadOnlyList<RoleId>>.Succeed([]),
            true when roleIds.Count == 0 => Validation<IReadOnlyList<RoleId>>.Fail(new Error(
                "command.learn.restricted-no-roles",
                "A restricted command must have at least one permission role.")),
            _ => Validation<IReadOnlyList<RoleId>>.Succeed(roleIds)
        };

    private async Task OnLearnSuccessAsync(CommandContext ctx, DiscordGuild guild, CustomCommandName name)
    {
        await ctx.ReplyAsync(GrimoireColor.Green, $"Added {name} custom command.");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} asked {guild.CurrentMember.Mention} to learn a new command: {name}",
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation
        });
    }
}
