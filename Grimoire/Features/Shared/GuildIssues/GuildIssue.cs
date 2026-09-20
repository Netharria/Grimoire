// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.GuildIssues;

/// <summary>A problem with the server's setup that only its moderators can fix, with the suggested fix.</summary>
/// <param name="Code">Stable identifier used to report each problem at most once a day per guild.</param>
public sealed record GuildIssue(GuildId GuildId, string Code, string Summary, string SuggestedFix);
