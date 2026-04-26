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

        if (!long.TryParse(version, out var unixSeconds))
        {
            await ctx.SendWarningResponseAsync("Invalid version selected. Use the autocomplete to pick a version.");
            return;
        }

        var guild = ctx.Guild!;
        await FetchVersionAsync(guild.GetGuildId(), name, DateTimeOffset.FromUnixTimeSeconds(unixSeconds))
            .BindAsync(target => SaveRevertedCommandAsync(target, ctx.GetModeratorId()))
            .Match(
                _ => OnRevertSuccessAsync(ctx, guild, name, unixSeconds),
                errors => ctx.SendWarningResponseAsync(errors[0].Message).AsTask());
    }

    private async Task<Result<CustomCommand>> FetchVersionAsync(GuildId guildId, CustomCommandName name, DateTimeOffset targetCreatedAt)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var target = await dbContext.CustomCommands
            .AsNoTracking()
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name && x.CreatedAt == targetCreatedAt);
        return target is null
            ? Result<CustomCommand>.Fail(new Error("command.revert.not_found", $"Version not found for command `{name}`."))
            : Result<CustomCommand>.Ok(target);
    }

    private async Task<Result<CustomCommand>> SaveRevertedCommandAsync(CustomCommand target, ModeratorId? moderatorId)
    {
        var now = DateTimeOffset.UtcNow;
        var reverted = new CustomCommand
        {
            Name = target.Name,
            GuildId = target.GuildId,
            CreatedAt = now,
            Content = target.Content,
            IsEmbedded = target.IsEmbedded,
            EmbedColor = target.EmbedColor,
            RestrictedUse = target.RestrictedUse,
            ModeratorId = moderatorId,
            Roles = [.. target.Roles.Select(r => new CustomCommandRole
            {
                Name = r.Name, GuildId = r.GuildId, CreatedAt = now, RoleId = r.RoleId
            })]
        };
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.AddAsync(reverted);
            await dbContext.SaveChangesAsync();
            return Result<CustomCommand>.Ok(reverted);
        }
        catch (DbUpdateException)
        {
            return Result<CustomCommand>.Fail(new Error("command.revert.db_error",
                "Could not revert the command due to a database error. Please try again."));
        }
    }

    private async Task OnRevertSuccessAsync(CommandContext ctx, DiscordGuild guild, CustomCommandName name, long unixSeconds)
    {
        await ctx.ReplyAsync(GrimoireColor.Green, $"Reverted `{name}` to version from <t:{unixSeconds}:f>.");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} reverted command `{name}` to version from <t:{unixSeconds}:f>.",
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation
        });
    }
}
