// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Lock;

public sealed class LockedTreadEvents(SettingsModule settingsModule)
    : IEventHandler<MessageCreatedEventArgs>
        , IEventHandler<MessageReactionAddedEventArgs>
{
    private readonly SettingsModule _settingsModule = settingsModule;

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
        if (await this._settingsModule.IsThreadLocked(args.GetChannelId(), args.Guild.GetGuildId())
                .GetOrElse(() => false))
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
        if (await this._settingsModule.IsThreadLocked(args.Channel.GetChannelId(), args.Guild.GetGuildId())
                .GetOrElse(() => false))
            await args.Message.DeleteReactionAsync(args.Emoji, args.User, "Thread is locked.");
    }
}
