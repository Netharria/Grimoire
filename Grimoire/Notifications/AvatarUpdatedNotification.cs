// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Notifications;

public sealed record AvatarUpdatedNotification : INotification
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string BeforeAvatar { get; init; } = string.Empty;
    public string AfterAvatar { get; init; } = string.Empty;
}
