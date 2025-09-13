// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Leveling.Settings;



public sealed partial class LevelSettingsCommandGroup
{
    public enum LevelSettings
    {
        [ChoiceDisplayName("Timeout between xp gains in minutes")]TextTime,
        [ChoiceDisplayName("Base - linear xp per level modifier")]Base,
        [ChoiceDisplayName("Modifier - exponential xp per level modifier")]Modifier,
        [ChoiceDisplayName("Amount per xp gain.")]Amount,
        [ChoiceDisplayName("Log Channel")]LogChannel
    }

    [Command("Set")]
    [Description("Set a leveling setting.")]
    public async Task SetAsync(
        CommandContext ctx,
        [Parameter("Setting")]
        [Description("The setting to change.")]
        LevelSettings levelSettings,
        [MinMaxValue(1, int.MaxValue)]
        [Parameter("Value")]
        [Description("The value to change the setting to.")]
        int value)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        await this._mediator.Send(new SetLevelSettings.Request
        {
            GuildId = ctx.Guild.Id, LevelSettings = levelSettings, Value = value.ToString()
        });

        await ctx.EditReplyAsync(message: $"Updated {levelSettings} level setting to {value}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = $"{ctx.User.Mention} updated {levelSettings} level setting to {value}"
        });
    }

    [Command("LogSet")]
    [Description("Set the leveling log channel.")]
    public async Task LogSetAsync(
        CommandContext ctx,
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel")]
        ChannelOption option,
        [Parameter("Channel")]
        [Description("The channel to change the log to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
                throw new AnticipatedException(
                    $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }

        await this._mediator.Send(new SetLevelSettings.Request
        {
            GuildId = ctx.Guild.Id,
            LevelSettings = LevelSettings.LogChannel,
            Value = channel is null ? "0" : channel.Id.ToString()
        });
        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? "Disabled the level log."
            : $"Updated the level log to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the level log."
                : $"{ctx.User.Mention} updated the level log to {channel?.Mention}."
        });
    }
}

public sealed class SetLevelSettings
{
    public sealed record Request : IRequest
    {
        public required ulong GuildId { get; init; }
        public required LevelSettingsCommandGroup.LevelSettings LevelSettings { get; init; }
        public required string Value { get; init; }
    }


    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var levelSettings = await dbContext.GuildLevelSettings
                .Where(x => x.GuildId == command.GuildId)
                .FirstOrDefaultAsync(cancellationToken);
            if (levelSettings == null) throw new AnticipatedException("Could not find guild level settings.");
            switch (command.LevelSettings)
            {
                case LevelSettingsCommandGroup.LevelSettings.TextTime:
                    if (!uint.TryParse(command.Value, out var textTime))
                        throw new AnticipatedException("Please give a valid number for TextTime.");
                    levelSettings.TextTime = TimeSpan.FromMinutes(textTime);
                    break;
                case LevelSettingsCommandGroup.LevelSettings.Base:
                    if (!int.TryParse(command.Value, out var baseXp))
                        throw new AnticipatedException("Please give a valid number for base XP.");
                    levelSettings.Base = baseXp;
                    break;
                case LevelSettingsCommandGroup.LevelSettings.Modifier:
                    if (!int.TryParse(command.Value, out var modifier))
                        throw new AnticipatedException("Please give a valid number for Modifier.");
                    levelSettings.Modifier = modifier;
                    break;
                case LevelSettingsCommandGroup.LevelSettings.Amount:
                    if (!int.TryParse(command.Value, out var amount))
                        throw new AnticipatedException("Please give a valid number for Amount.");
                    levelSettings.Amount = amount;
                    break;
                case LevelSettingsCommandGroup.LevelSettings.LogChannel:
                    if (!ulong.TryParse(command.Value, out var value))
                        throw new AnticipatedException("Please give a valid channel for Log Channel.");
                    levelSettings.LevelChannelLogId = value == 0 ? null : value;
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
