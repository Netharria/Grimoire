// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{

    [SlashCommand("Set", "Set a leveling setting.")]
    public async Task SetAsync(
        InteractionContext ctx,
        [Choice("Timeout between xp gains in minutes", 0)]
        [Choice("Base - linear xp per level modifier", 1)]
        [Choice("Modifier - exponential xp per level modifier", 2)]
        [Choice("Amount per xp gain.", 3)]
        [Option("Setting", "The Setting to change.")] long levelSettings,
        [Maximum(int.MaxValue)]
        [Minimum(1)]
        [Option("Value", "The value to change the setting to.")] long value)
    {
        await ctx.DeferAsync();
        var levelSetting = (LevelSettings)levelSettings;
        var response = await this._mediator.Send(new SetLevelSettings.Request
        {
            GuildId = ctx.Guild.Id,
            LevelSettings = levelSetting,
            Value = value.ToString()
        });

        await ctx.EditReplyAsync(message: $"Updated {levelSetting.GetName()} level setting to {value}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.Member.Mention} updated {levelSetting.GetName()} level setting to {value}");
    }

    [SlashCommand("LogSet", "Set the leveling log channel.")]
    public async Task LogSetAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to change the log to.")] DiscordChannel? channel = null)
    {
        await ctx.DeferAsync();
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }
        var response = await this._mediator.Send(new SetLevelSettings.Request
        {
            GuildId = ctx.Guild.Id,
            LevelSettings = LevelSettings.LogChannel,
            Value = channel is null ? "0" : channel.Id.ToString()
        });
        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: $"Disabled the level log.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the level log.");
            return;
        }
        await ctx.EditReplyAsync(message: $"Updated the level log to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.User.Mention} updated the level log to {channel?.Mention}.");
    }
}

public enum LevelSettings
{
    TextTime,
    Base,
    Modifier,
    Amount,
    LogChannel,
}
public sealed class SetLevelSettings
{
    public sealed record Request : ICommand<BaseResponse>
    {
        public required ulong GuildId { get; init; }
        public required LevelSettings LevelSettings { get; init; }
        public required string Value { get; init; }
    }


    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            var levelSettings = await this._grimoireDbContext.GuildLevelSettings
            .Where(x => x.GuildId == command.GuildId)
            .Select(x => new
            {
                LevelSettings = x,
                x.Guild.ModChannelLog
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (levelSettings == null) throw new AnticipatedException("Could not find guild level settings.");
            switch (command.LevelSettings)
            {
                case LevelSettings.TextTime:
                    if (!uint.TryParse(command.Value, out var textTime))
                        throw new AnticipatedException("Please give a valid number for TextTime.");
                    levelSettings.LevelSettings.TextTime = TimeSpan.FromMinutes(textTime);
                    break;
                case LevelSettings.Base:
                    if (!int.TryParse(command.Value, out var baseXp))
                        throw new AnticipatedException("Please give a valid number for base XP.");
                    levelSettings.LevelSettings.Base = baseXp;
                    break;
                case LevelSettings.Modifier:
                    if (!int.TryParse(command.Value, out var modifier))
                        throw new AnticipatedException("Please give a valid number for Modifier.");
                    levelSettings.LevelSettings.Modifier = modifier;
                    break;
                case LevelSettings.Amount:
                    if (!int.TryParse(command.Value, out var amount))
                        throw new AnticipatedException("Please give a valid number for Amount.");
                    levelSettings.LevelSettings.Amount = amount;
                    break;
                case LevelSettings.LogChannel:
                    if (!ulong.TryParse(command.Value, out var value))
                        throw new AnticipatedException("Please give a valid channel for Log Channel.");
                    levelSettings.LevelSettings.LevelChannelLogId = value == 0 ? null : value;
                    break;
            }

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                LogChannelId = levelSettings.ModChannelLog
            };
        }
    }
}

