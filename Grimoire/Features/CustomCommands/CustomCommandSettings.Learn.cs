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
        [Parameter("restricted_use")]
        [Description("Only the permission roles may use this command. Otherwise, the permission roles may not use it.")]
        bool restrictedUse = false,
        [VariadicArgument(10)]
        [Parameter("permission_role")]
        [Description("A role the command is limited to, or blocked for without restricted_use.")]
        DiscordRole[]? permissionRoles = null)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var guildId = guild.GetGuildId();
        var roleIds = CollectRoleIds(permissionRoles ?? []);

        await CustomCommandName.Create(name)
            .Bind(validName => NotABuiltInCommand(ctx.Extension, validName))
            .Bind(validName => ParseEmbedColor(embed, embedColor)
                .Bind(color => SplitRoleIds(restrictedUse, roleIds)
                    .Bind(roles => CustomCommandContent.Create(content)
                        .Map(CustomCommand (validContent) => embed
                            ? EmbedCustomCommand.Create(validName, guildId, validContent, color, roles.Allow,
                                roles.Deny, ctx.GetModeratorId(), DateTimeOffset.UtcNow)
                            : TextCustomCommand.Create(validName, guildId, validContent, roles.Allow, roles.Deny,
                                ctx.GetModeratorId(), DateTimeOffset.UtcNow)))))
            .ToResult()
            .Match(
                async command =>
                {
                    var outcome = await this.SaveCommandAsync(command);
                    await this.OnLearnSuccessAsync(ctx, guild, command.Name, outcome);
                },
                error => ctx.SendErrorResponseAsync(error.Message).AsTask());
    }

    /// <summary>
    ///     <c>!name</c> runs the bot's own command of that name as a text command, so a custom command can't take it
    ///     (KI-85).
    /// </summary>
    private static Validation<CustomCommandName> NotABuiltInCommand(CommandsExtension extension, CustomCommandName name)
        => extension.IsBuiltInCommandName(name.Value)
            ? Validation<CustomCommandName>.Fail(new Error("command.learn.built-in-name",
                $"`{name}` is the name of one of Grimoire's own commands, so a custom command can't use it."))
            : Validation<CustomCommandName>.Succeed(name);

    /// <summary>A colour only means something on an embed, so one given without it is refused rather than dropped.</summary>
    private static Validation<CustomCommandEmbedColor?> ParseEmbedColor(bool embed, string? embedColor)
        => embedColor switch
        {
            null => Validation<CustomCommandEmbedColor?>.Succeed(null),
            _ when !embed => Validation<CustomCommandEmbedColor?>.Fail(new Error("command.learn.color-without-embed",
                "An embed color only applies to an embed. Set embed to true, or leave out the color.")),
            _ => CustomCommandEmbedColor.Create(embedColor).Map(color => (CustomCommandEmbedColor?)color)
        };

    private static IReadOnlyList<RoleId> CollectRoleIds(IEnumerable<DiscordRole> roles)
        => roles.Select(r => r.GetRoleId()).Distinct().ToList();

    /// <summary>
    ///     A restricted command may be used only by its roles, so it needs one. Roles on an unrestricted command are the
    ///     ones that may not use it, as on <c>main</c>.
    /// </summary>
    private static Validation<(IReadOnlyList<RoleId> Allow, IReadOnlyList<RoleId> Deny)> SplitRoleIds(
        bool restrictedUse, IReadOnlyList<RoleId> roleIds)
        => restrictedUse switch
        {
            true when roleIds.Count == 0 => Validation<(IReadOnlyList<RoleId>, IReadOnlyList<RoleId>)>.Fail(new Error(
                "command.learn.restricted-no-roles",
                "A restricted command must have at least one permission role.")),
            true => Validation<(IReadOnlyList<RoleId>, IReadOnlyList<RoleId>)>.Succeed((roleIds, [])),
            false => Validation<(IReadOnlyList<RoleId>, IReadOnlyList<RoleId>)>.Succeed(([], roleIds))
        };

    private async Task OnLearnSuccessAsync(CommandContext ctx, DiscordGuild guild, CustomCommandName name,
        SaveOutcome outcome)
    {
        await ctx.ReplyAsync(GrimoireColor.Green, outcome switch
        {
            SaveOutcome.Updated => $"Updated {name} custom command.",
            _ => $"Added {name} custom command."
        });
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            Color = GrimoireColor.Purple,
            Description = outcome switch
            {
                SaveOutcome.Updated => $"{ctx.User.Mention} asked {guild.CurrentMember.Mention} to update the command: {name}",
                _ => $"{ctx.User.Mention} asked {guild.CurrentMember.Mention} to learn a new command: {name}"
            },
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation
        });
    }
}
