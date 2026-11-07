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

internal sealed class GuildMemberAddedEvent(
    GuildLog guildLog,
    SettingsModule settingsModule,
    IInviteService inviteService)
    : IEventHandler<GuildMemberAddedEventArgs>
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly IInviteService _inviteService = inviteService;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.GetGuildId()).GetOrElse(() => false))
            return;
        // Without Manage Server the invites can't be listed, so the join is logged with no invite.
        var invites = await args.Guild.GetGuildInvitesAsync();
        var inviteUsed = invites is null ? null : this._inviteService.CalculateInviteUsed(invites);
        string? inviteUsedText;
        if (inviteUsed is not null)
            inviteUsedText =
                $"{inviteUsed.Url} ({inviteUsed.Uses} uses)\n**Created By:** {inviteUsed.Inviter?.ToString() ?? "Unknown"}";
        else if (!string.IsNullOrWhiteSpace(args.Guild.VanityUrlCode))
            inviteUsedText = "Vanity Invite";
        else
            inviteUsedText = "Unknown Invite";

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Joined")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Invite Used", inviteUsedText)
            .WithColor(args.Member.CreationTimestamp > DateTimeOffset.UtcNow.AddDays(-7)
                ? GrimoireColor.Yellow
                : GrimoireColor.Green)
            .WithThumbnail(args.Member.GetGuildAvatarUrl(MediaFormat.Auto))
            .WithFooter($"Total Members: {args.Guild.MemberCount}")
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (args.Member.CreationTimestamp > DateTimeOffset.UtcNow.AddDays(-7))
            embed.AddField("New Account", $"Created {Formatter.Timestamp(args.Member.CreationTimestamp)}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.UserJoined, Embed = embed
        });
    }
}
