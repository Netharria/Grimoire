// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;

namespace Grimoire.Features.Shared.Alerts;

public sealed class AlertChannels(DiscordClient client, IConfiguration configuration)
{
    /// <summary>
    ///     <c>channelId</c> stays the urgent channel so existing deployments keep working;
    ///     <c>warningChannelId</c> is optional.
    /// </summary>
    public ChannelId? ConfiguredId(AlertSeverity severity) => ChannelId.TryParse(configuration[severity switch
    {
        AlertSeverity.Urgent => "channelId",
        AlertSeverity.Warning => "warningChannelId",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    }]);

    public Task<DiscordChannel?> GetAsync(AlertSeverity severity, CancellationToken cancellationToken = default)
        => client.GetChannelOrDefaultAsync(this.ConfiguredId(severity), cancellationToken);
}
