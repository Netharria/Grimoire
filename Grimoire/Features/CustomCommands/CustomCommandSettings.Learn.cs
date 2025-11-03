// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Text.RegularExpressions;
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
        [MinMaxLength(0, 24)]
        [Parameter("Name")]
        [Description("The name that the command will be called. This is used to activate the command.")]
        CustomCommandName name,
        [MinMaxLength(maxLength: 2000)]
        [Parameter("Content")]
        [Description("The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Parameter("Embed")] [Description("Put the message in an embed")]
        bool embed = false,
        [MinMaxLength(maxLength: 6)] [Parameter("EmbedColor")] [Description("Hexadecimal color of the embed")]
        CustomCommandEmbedColor? embedColor = null
        // ,
        // [Parameter("RestrictedUse")]
        // [Description("Only explicitly allowed roles can use this command")]
        // bool restrictedUse = false,
        // [Parameter("PermissionRoles")]
        // [Description("Deny roles the ability to use this command or allow roles if command is restricted use")]
        // [VariadicArgument(10)]
        // IReadOnlyList<DiscordRole>? allowedRoles = null
    )
    {
        IReadOnlyList<DiscordRole> allowedRoles;
        const bool restrictedUse = false;
        await ctx.DeferResponseAsync();


        var guild = ctx.Guild!;

        allowedRoles = [];

        if (restrictedUse && allowedRoles.Count != 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Command set as restricted but no roles allowed to use it.");
            return;
        }

        await SaveCommand(name, guild.GetGuildId(), content, embed, embedColor, restrictedUse,
            allowedRoles.Select(x => x.GetRoleId()).ToArray());

        await ctx.EditReplyAsync(GrimoireColor.Green, $"Learned new command: {name}");
        await this._guildLog.SendLogMessageAsync(
            new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Description =
                    $"{ctx.User.Mention} asked {guild.CurrentMember.Mention} to learn a new command: {name}",
                Color = GrimoireColor.Purple
            });
    }

    private async Task SaveCommand(CustomCommandName commandName, GuildId guildId, string content, bool isEmbedded,
        CustomCommandEmbedColor? embedColor, bool restrictedUse, ICollection<RoleId> permissionRoles,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        var result = await dbContext.CustomCommands
            .Include(x => x.CustomCommandRoles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Name == commandName && x.GuildId == guildId,
                cancellationToken);
        var commandRoles = permissionRoles.Select(x =>
            new CustomCommandRole { CustomCommandName = commandName, GuildId = guildId, RoleId = x }).ToList();
        if (result is null)
        {
            result = new CustomCommand
            {
                Name = commandName,
                GuildId = guildId,
                Content = content,
                HasMention = content.Contains("%mention", StringComparison.OrdinalIgnoreCase),
                HasMessage = content.Contains("%message", StringComparison.OrdinalIgnoreCase),
                IsEmbedded = isEmbedded,
                EmbedColor = embedColor,
                RestrictedUse = restrictedUse,
                CustomCommandRoles = commandRoles
            };
            await dbContext.AddAsync(result, cancellationToken);
        }
        else
        {
            result.Name = commandName;
            result.GuildId = guildId;
            result.Content = content;
            result.HasMention = content.Contains("%mention", StringComparison.OrdinalIgnoreCase);
            result.HasMessage = content.Contains("%message", StringComparison.OrdinalIgnoreCase);
            result.IsEmbedded = isEmbedded;
            result.EmbedColor = embedColor;
            result.RestrictedUse = restrictedUse;
            result.CustomCommandRoles.Clear();
            foreach (var role in commandRoles)
                result.CustomCommandRoles.Add(role);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
