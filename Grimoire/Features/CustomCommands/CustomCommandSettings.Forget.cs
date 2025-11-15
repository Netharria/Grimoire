// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Forget")]
    [Description("Forget a command that you have saved.")]
    public Task Forget(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
        [Parameter("Name")]
        [Description("The name of the command to forget.")]
        CustomCommandName name) =>
        (
            from guild in Optional(ctx.Guild).ToEff(Error.New("This command can only be used in a server."))
            from _1 in ctx.DeferResponse()
            from _2 in this._dbContextFactory.StartTransaction(
                (dbContext, cancellationToken) =>
                            dbContext.CustomCommands
                            .Where(x => x.Name == name && x.GuildId == guild.GetGuildId())
                            .ExecuteDeleteAsync(cancellationToken)
                            .ToUnit())
            from _3 in ctx.EditReply(GrimoireColor.Green, $"Forgot command: {name}")
            from _4 in this._guildLog.SendLogMessage(new GuildLogMessage
                {
                    GuildId = guild.GetGuildId(),
                    GuildLogType = GuildLogType.Moderation,
                    Description = $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command: {name}",
                    Color = GrimoireColor.Purple
                })
            select unit)
        .Run()
        .Match(
            success => success.AsTask(),
            error => ctx.SendErrorResponseAsync(error.Message));

    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Forget")]
    [Description("Forget a command that you have saved.")]
    public Task Forget2(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
        [Parameter("Name")]
        [Description("The name of the command to forget.")]
        CustomCommandName name) =>
        (
            from guild in OptionT.lift<Eff, DiscordGuild>(Optional(ctx.Guild))
            from _1 in ctx.DeferResponse()
            from _2 in this._dbContextFactory.StartTransaction(
                (dbContext, cancellationToken) =>
                    dbContext.CustomCommands
                        .Where(x => x.Name == name && x.GuildId == guild.GetGuildId())
                        .ExecuteDeleteAsync(cancellationToken)
                        .ToUnit())
            from _3 in ctx.EditReply(GrimoireColor.Green, $"Forgot command: {name}")
            from _4 in this._guildLog.SendLogMessage(new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Description = $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command: {name}",
                Color = GrimoireColor.Purple
            })
            select unit)
        .Run()
        .Run()
        .Match(
            success => success.AsTask(),
            error => ctx.SendErrorResponseAsync(error.Message));
}
