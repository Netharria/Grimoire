// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Features.Shared.Attributes;

internal sealed class RequireModuleEnabledAttribute(Module module) : ContextCheckAttribute
{
    public readonly Module Module = module;
}

internal sealed class RequireModuleEnabledCheck : IContextCheck<RequireModuleEnabledAttribute>
{
    public async ValueTask<string?> ExecuteCheckAsync(RequireModuleEnabledAttribute attribute, CommandContext context)
    {
        if (context.Guild is null)
            return "This command can only be used in a server.";
        var settingsModule = context.ServiceProvider.GetRequiredService<SettingsModule>();
        var result = await settingsModule.IsModuleEnabled(attribute.Module, context.Guild.Id);
        return !result ? "This module is disabled in this server." : null;
    }
}
