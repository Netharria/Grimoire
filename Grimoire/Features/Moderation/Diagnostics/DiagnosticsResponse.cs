// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Moderation.Diagnostics;

public static class DiagnosticsResponse
{
    /// <summary>The summary embed with the full report attached as a text file.</summary>
    public static DiscordMessageBuilder Build(DiscordGuild guild, RoleId? muteRoleId, DiagnosticsIgnores ignores)
    {
        var snapshot = DiagnosticsSnapshotBuilder.Build(guild, muteRoleId);
        var result = ModerationDiagnostics.Analyze(snapshot, ignores);

        return new DiscordMessageBuilder()
            .AddEmbed(DiagnosticsReportFormatter.BuildEmbed(snapshot, result))
            .AddFile("moderation-diagnostics.txt",
                new MemoryStream(Encoding.UTF8.GetBytes(DiagnosticsReportFormatter.BuildText(snapshot, result))));
    }
}
