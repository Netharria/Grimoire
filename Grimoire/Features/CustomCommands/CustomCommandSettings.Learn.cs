// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Text.RegularExpressions;
using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels.GuildLog;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();

    //todo: fix this when variadic arguments are fixed
    [UsedImplicitly]
    [Command("Learn")]
    [Description("Learn a new command or update an existing one")]
    public async Task Learn(
        CommandContext ctx,
        [MinMaxLength(0, 24)]
        [Parameter("Name")]
        [Description("The name that the command will be called. This is used to activate the command.")]
        string name,
        [MinMaxLength(maxLength: 2000)]
        [Parameter("Content")]
        [Description("The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Parameter("Embed")] [Description("Put the message in an embed")]
        bool embed = false,
        [MinMaxLength(maxLength: 6)] [Parameter("EmbedColor")] [Description("Hexadecimal color of the embed")]
        string? embedColor = null
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

        if (name.Contains(' '))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "No spaces are allowed in command name.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(embedColor) && !ValidHexColor().IsMatch(embedColor))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Embed Color provided but is not a valid hex color code.");
            return;
        }

        if (ctx.Guild is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "This command can only be used in a server.");
            return;
        }

        allowedRoles = Array.Empty<DiscordRole>();

        if (restrictedUse && allowedRoles.Count != 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Command set as restricted but no roles allowed to use it.");
            return;
        }

        await SaveCommand(name, ctx.Guild.Id, content, embed, embedColor, restrictedUse,
            allowedRoles.Select(x => x.Id).ToArray());

        await ctx.EditReplyAsync(GrimoireColor.Green, $"Learned new command: {name}");
        await this._guildLog.SendLogMessageAsync(
            new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description =
                    $"{ctx.User.Mention} asked {ctx.Guild.CurrentMember.Mention} to learn a new command: {name}",
                Color = GrimoireColor.Purple
            });
    }

    private async Task SaveCommand(string commandName, ulong guildId, string content, bool isEmbedded,
        string? embedColor, bool restrictedUse, ICollection<ulong> permissionRoles,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Roles.AddMissingRolesAsync(permissionRoles, guildId, cancellationToken);

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
