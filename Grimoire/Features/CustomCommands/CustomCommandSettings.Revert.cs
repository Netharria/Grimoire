// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
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
    [Command("Revert")]
    [Description("Revert a command to a previous version.")]
    public async Task Revert(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("Name")]
        [Description("The name of the command to revert.")]
        CustomCommandName name,
        [SlashAutoCompleteProvider<GetCommandVersionOptions>]
        [Parameter("Version")]
        [Description("The version to revert to.")]
        string version)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("This command can only be used in a server.");
            return;
        }

        if (!long.TryParse(version, out var unixSeconds))
        {
            await ctx.SendWarningResponseAsync("Invalid version selected. Use the autocomplete to pick a version.");
            return;
        }

        var targetCreatedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var guildId = guild.GetGuildId();

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var target = await dbContext.CustomCommands
            .AsNoTracking()
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name && x.CreatedAt == targetCreatedAt);

        if (target is null)
        {
            await ctx.SendWarningResponseAsync($"Version not found for command `{name}`.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var reverted = new CustomCommand
        {
            Name = target.Name,
            GuildId = target.GuildId,
            CreatedAt = now,
            Content = target.Content,
            HasMention = target.HasMention,
            HasMessage = target.HasMessage,
            IsEmbedded = target.IsEmbedded,
            EmbedColor = target.EmbedColor,
            RestrictedUse = target.RestrictedUse,
            ModeratorId = ctx.GetModeratorId(),
            Roles = [.. target.Roles.Select(r => new CustomCommandRole
            {
                Name = r.Name,
                GuildId = r.GuildId,
                CreatedAt = now,
                RoleId = r.RoleId
            })]
        };

        await dbContext.AddAsync(reverted);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await ctx.SendErrorResponseAsync("Could not revert the command due to a database error. Please try again.");
            return;
        }

        await ctx.ReplyAsync(GrimoireColor.Green,
            $"Reverted `{name}` to version from <t:{unixSeconds}:f>.");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} reverted command `{name}` to version from <t:{unixSeconds}:f>.",
            GuildId = guildId,
            GuildLogType = GuildLogType.Moderation
        });
    }
}
