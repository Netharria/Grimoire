// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Runtime.InteropServices.JavaScript;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    //todo: fix this when variadic arguments are fixed
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Learn")]
    [Description("Learn a new command or update an existing one")]
    public async Task Learn(
        CommandContext ctx,
        [MinMaxLength(0, 24)]
        [Parameter("Name")]
        [Description("The name that the command will be called. This is used to activate the command.")]
        CustomCommandName name,
        [MinMaxLength(maxLength: 2000)]
        [Parameter("Content")]
        [Description("The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Parameter("Embed")] [Description("Put the message in an embed")]
        bool embed = false,
        [MinMaxLength(maxLength: 6)] [Parameter("EmbedColor")] [Description("Hexadecimal color of the embed")]
        CustomCommandEmbedColor? embedColor = null
        // ,
        // [Parameter("RestrictedUse")]
        // [Description("Only explicitly allowed roles can use this command")]
        // bool restrictedUse = false,
        // [Parameter("PermissionRoles")]
        // [Description("Deny roles the ability to use this command or allow roles if command is restricted use")]
        // [VariadicArgument(10)]
        // IReadOnlyList<DiscordRole>? allowedRoles = null
    )
    {
        IReadOnlyList<DiscordRole> allowedRoles = [];
        const bool restrictedUse = false;

        await (
            from _1 in ctx.DeferResponse()
            from _2 in guard(restrictedUse && allowedRoles.Count == 0, Error.New("Command set as restricted but no roles allowed to use it."))
            from guild in Optional(ctx.Guild).ToEff(Error.New("This command can only be used in a server."))
            from _3 in SaveCommand(name, guild.GetGuildId(), content, embed, embedColor, restrictedUse,
                allowedRoles.Select(x => x.GetRoleId()).ToArray())
            from _4 in ctx.EditReply(GrimoireColor.Green, $"Learned new command: {name}")
            from _5 in this._guildLog.SendLogMessage(
                new GuildLogMessage
                {
                    GuildId = guild.GetGuildId(),
                    GuildLogType = GuildLogType.Moderation,
                    Description =
                        $"{ctx.User.Mention} asked {guild.CurrentMember.Mention} to learn a new command: {name}",
                    Color = GrimoireColor.Purple
                })
            select _5
            )
            .Run()
            .Match(
                Succ: result => result.AsTask(),
                Fail: err => ctx.SendErrorResponseAsync(err.Message)
            );
    }

    private Eff<Unit> SaveCommand(
        CustomCommandName commandName,
        GuildId guildId,
        string content,
        bool isEmbedded,
        CustomCommandEmbedColor? embedColor,
        bool restrictedUse,
        ICollection<RoleId> permissionRoles,
        CancellationToken cancellationToken = default) =>
        this._dbContextFactory.StartTransaction(
            (dbContext, token) =>
                from _1 in liftEff(() => dbContext.CustomCommands
                    .Where(x => x.Name == commandName && x.GuildId == guildId)
                    .ExecuteDeleteAsync(token))
                from _2 in liftEff(() => dbContext.AddAsync(
                    new CustomCommand
                    {
                        Name = commandName,
                        GuildId = guildId,
                        Content = content,
                        HasMention = content.Contains("%mention", StringComparison.OrdinalIgnoreCase),
                        HasMessage = content.Contains("%message", StringComparison.OrdinalIgnoreCase),
                        IsEmbedded = isEmbedded,
                        EmbedColor = embedColor,
                        RestrictedUse = restrictedUse,
                        CustomCommandRoles = permissionRoles.Select(x =>
                                new CustomCommandRole
                                    {
                                        CustomCommandName = commandName, GuildId = guildId, RoleId = x
                                    })
                            .ToList()
                    }, token))
                from _3 in liftEff(() => dbContext.SaveChangesAsync(token))
                select unit, cancellationToken
        );


}
