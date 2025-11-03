// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.UserLogging;

public sealed class GuildMemberRemovedEvent(GuildLog guildLog, SettingsModule settingsModule)
    : IEventHandler<GuildMemberRemovedEventArgs>
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberRemovedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.GetGuildId()))
            return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Left")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Joined", Formatter.Timestamp(args.Member.JoinedAt), true)
            .WithColor(GrimoireColor.Red)
            .WithThumbnail(args.Member.GetGuildAvatarUrl(MediaFormat.Auto))
            .WithFooter($"Total Members: {args.Guild.MemberCount}")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField($"Roles[{args.Member.Roles.Count()}]",
                args.Member.Roles.Any()
                    ? string.Join(' ', args.Member.Roles.Where(x => x.Id != args.Guild.Id).Select(x => x.Mention))
                    : "None");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.UserLeft, Embed = embed
        });
    }
}
