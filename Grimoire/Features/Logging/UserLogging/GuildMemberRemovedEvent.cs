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
    public async Task HandleEventAsync(DiscordClient sender, GuildMemberRemovedEventArgs args)
    {
        if (!await settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.GetGuildId()).GetOrElse(() => false))
            return;

        var roles = args.Member.Roles.Where(x => x.Id != args.Guild.Id).Select(x => x.Mention).ToArray();
        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Left")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Joined", Formatter.Timestamp(args.Member.JoinedAt), true)
            .WithColor(GrimoireColor.Red)
            .WithThumbnail(args.Member.GetGuildAvatarUrl(MediaFormat.Auto))
            .WithFooter($"Total Members: {args.Guild.MemberCount}")
            .WithTimestamp(DateTimeOffset.UtcNow)
            // About 45 roles pass a field's 1,024 characters, so they're split across fields.
            .AddMessageTextToFields($"Roles[{roles.Length}]", roles.Length > 0 ? string.Join(' ', roles) : "None");
        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.UserLeft, Embed = embed
        });
    }
}
