// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers
{
    public static class MessageDatabaseQueryHelpers
    {
        public static IQueryable<Message> WhereLoggingIsEnabled(this IQueryable<Message> messages)
            => messages.Where(x => x.Guild.LogSettings.ModuleEnabled);
    }
}
