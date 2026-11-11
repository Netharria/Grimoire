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

        await ParseVersion(name, version)
            .ToResult()
            .BindAsync(createdAt => this.FetchVersionAsync(guild.GetGuildId(), name, createdAt))
            .Match(
                async target =>
                {
                    await this.SaveCommandAsync(target.AsNewVersion(ctx.GetModeratorId(), DateTimeOffset.UtcNow));
                    await this.OnRevertSuccessAsync(ctx, guild, name, target.CreatedAt);
                },
                error => ctx.SendWarningResponseAsync(error.Message).AsTask());
    }

    /// <summary>
    ///     A version is the ticks of its <c>CreatedAt</c>. A hand-typed number outside <see cref="DateTimeOffset" />'s
    ///     range can't be any version, so it's not found rather than a crash (KI-83).
    /// </summary>
    private static Validation<DateTimeOffset> ParseVersion(CustomCommandName name, string version)
        => long.TryParse(version, out var ticks) switch
        {
            false => Validation<DateTimeOffset>.Fail(new Error("command.revert.invalid-version",
                "Invalid version selected. Use the autocomplete to pick a version.")),
            true when ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks =>
                Validation<DateTimeOffset>.Fail(VersionNotFound(name)),
            true => Validation<DateTimeOffset>.Succeed(new DateTimeOffset(ticks, TimeSpan.Zero))
        };

    private static Error VersionNotFound(CustomCommandName name)
        => new("command.revert.not_found", $"Version not found for command `{name}`.");

    private async Task<Result<CustomCommand>> FetchVersionAsync(GuildId guildId, CustomCommandName name,
        DateTimeOffset targetCreatedAt)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var target = await dbContext.CustomCommands
            .AsNoTracking()
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name && x.CreatedAt == targetCreatedAt);
        return target is null
            ? Result<CustomCommand>.Fail(VersionNotFound(name))
            : Result<CustomCommand>.Ok(target);
    }

    private async Task OnRevertSuccessAsync(CommandContext ctx, DiscordGuild guild, CustomCommandName name,
        DateTimeOffset createdAt)
    {
        var unixSeconds = createdAt.ToUnixTimeSeconds();
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
