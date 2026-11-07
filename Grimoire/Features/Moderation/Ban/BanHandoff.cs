// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Grimoire.Features.Moderation.Ban;

/// <summary>A ban <c>/Ban</c> asked Discord for, waiting for its gateway event to record it.</summary>
public sealed record PendingBan(ModeratorId Moderator, string Reason)
{
    private static readonly TimeSpan _recordTimeout = TimeSpan.FromSeconds(5);
    private readonly TaskCompletionSource<SinId> _recorded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void MarkRecorded(SinId sinId) => this._recorded.TrySetResult(sinId);

    /// <returns>The sin's id, or null if the event didn't arrive in time.</returns>
    public async Task<SinId?> WaitUntilRecordedAsync()
    {
        try
        {
            return await this._recorded.Task.WaitAsync(_recordTimeout);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}

/// <summary>
///     Discord's ban events only name the guild and the user, and every ban the bot makes looks like the bot's own in the
///     audit log. So <c>/Ban</c> and <c>/Unban</c> leave who asked here, and the events collect it. The events are the only
///     writers of ban sins, so a ban can't be recorded twice however the two race.
/// </summary>
public sealed class BanHandoff(TimeProvider timeProvider)
{
    /// <summary>An entry older than this lost its event, so it must not be pinned on a later ban.</summary>
    private static readonly TimeSpan _maxAge = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<(GuildId, UserId), (PendingBan Ban, DateTimeOffset At)> _bans = new();
    private readonly ConcurrentDictionary<(GuildId, UserId), (ModeratorId Moderator, DateTimeOffset At)> _unbans = new();

    public PendingBan ExpectBan(GuildId guildId, UserId userId, ModeratorId moderator, string reason)
    {
        var pending = new PendingBan(moderator, reason);
        this._bans[(guildId, userId)] = (pending, timeProvider.GetUtcNow());
        return pending;
    }

    public PendingBan? TakeBan(GuildId guildId, UserId userId)
        => this._bans.TryRemove((guildId, userId), out var entry) && this.IsFresh(entry.At) ? entry.Ban : null;

    public void CancelBan(GuildId guildId, UserId userId) => this._bans.TryRemove((guildId, userId), out _);

    public void ExpectUnban(GuildId guildId, UserId userId, ModeratorId moderator)
        => this._unbans[(guildId, userId)] = (moderator, timeProvider.GetUtcNow());

    public ModeratorId? TakeUnban(GuildId guildId, UserId userId)
        => this._unbans.TryRemove((guildId, userId), out var entry) && this.IsFresh(entry.At) ? entry.Moderator : null;

    public void CancelUnban(GuildId guildId, UserId userId) => this._unbans.TryRemove((guildId, userId), out _);

    private bool IsFresh(DateTimeOffset at) => timeProvider.GetUtcNow() - at < _maxAge;
}
