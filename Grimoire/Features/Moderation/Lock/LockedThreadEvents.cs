// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Lock;

public sealed class LockedThreadEvents(SettingsModule settingsModule)
    : IEventHandler<MessageCreatedEventArgs>
        , IEventHandler<MessageReactionAddedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        // DSharpPlus hasn't finished implementing nullable notations
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null)
            return;
        if (!args.Channel.IsThread)
            return;
        if (args.Author is not DiscordMember member)
            return;
        if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermission.ManageMessages))
            return;
        if (await this.IsEnforcedAsync(args.GetChannelId(), args.Guild.GetGuildId()))
            await args.Message.DeleteAsync("Thread is locked.");
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs args)
    {
        if (args.Guild is null)
            return;
        if (!args.Channel.IsThread)
            return;
        if (args.User is not DiscordMember member)
            return;
        if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermission.ManageMessages))
            return;
        if (await this.IsEnforcedAsync(args.Channel.GetChannelId(), args.Guild.GetGuildId()))
            await args.Message.DeleteReactionAsync(args.Emoji, args.User, "Thread is locked.");
    }

    private async Task<bool> IsEnforcedAsync(ChannelId channelId, GuildId guildId)
        => await settingsModule.IsModuleEnabled(Module.Moderation, guildId).GetOrElse(() => false)
           && await settingsModule.IsThreadLocked(channelId, guildId).GetOrElse(() => false);
}
