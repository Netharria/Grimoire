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
        SlashCommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("name")]
        [Description("The name of the command to revert.")]
        CustomCommandName name,
        [SlashAutoCompleteProvider<GetCommandVersionOptions>]
        [Parameter("version")]
        [Description("The version to revert to.")]
        string version)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        await ParseVersion(version)
            .ToResult()
            .BindAsync(ticks => FetchVersionAsync(guild.GetGuildId(), name, new DateTimeOffset(ticks, TimeSpan.Zero))
                .Map(target => (Target: target, Ticks: ticks)))
            .Match(
                async found =>
                {
                    await this.SaveCommandAsync(found.Target.AsNewVersion(ctx.GetModeratorId(), DateTimeOffset.UtcNow));
                    await OnRevertSuccessAsync(ctx, guild, name, found.Ticks);
                },
                error => ctx.SendWarningResponseAsync(error.Message).AsTask());
    }

    private static Validation<long> ParseVersion(string version)
        => long.TryParse(version, out var ticks)
            ? Validation<long>.Succeed(ticks)
            : Validation<long>.Fail(new Error("command.revert.invalid-version",
                "Invalid version selected. Use the autocomplete to pick a version."));

    private async Task<Result<CustomCommand>> FetchVersionAsync(GuildId guildId, CustomCommandName name,
        DateTimeOffset targetCreatedAt)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var target = await dbContext.CustomCommands
            .AsNoTracking()
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name && x.CreatedAt == targetCreatedAt);
        return target is null
            ? Result<CustomCommand>.Fail(new Error("command.revert.not_found",
                $"Version not found for command `{name}`."))
            : Result<CustomCommand>.Ok(target);
    }

    private async Task OnRevertSuccessAsync(CommandContext ctx, DiscordGuild guild, CustomCommandName name,
        long ticks)
    {
        var unixSeconds = new DateTimeOffset(ticks, TimeSpan.Zero).ToUnixTimeSeconds();
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
