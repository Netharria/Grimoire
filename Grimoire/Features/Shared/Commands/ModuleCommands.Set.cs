// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    [UsedImplicitly]
    [Command("Set")]
    [Description("Enable or Disable a module.")]
    public async Task SetAsync(SlashCommandContext ctx,
        [Parameter("Module")]
        [Description("The module to enable or disable.")]
        Module module,
        [Parameter("Enable")]
        [Description("Whether to enable or disable the module.")]
        bool enable)
    {
        await ctx.DeferResponseAsync();

        if(ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new EnableModule.Request
        {
            GuildId = ctx.Guild.Id, Module = module, Enable = enable
        });

        await ctx.EditReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module}");

        await this._channel.Writer.WriteAsync(new PublishToGuildLog
        {
            LogChannelId = response.LogChannelId,
            Description = $"{ctx.User.GetUsernameWithDiscriminator()} {(enable ? "Enabled" : "Disabled")} {module}",
            Color = GrimoireColor.Purple
        });
    }
}


internal sealed class EnableModule
{
    public sealed record Request : IRequest<BaseResponse>
{
    public ulong GuildId { get; init; }
    public Module Module { get; init; }
    public bool Enable { get; init; }
}

public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IRequestHandler<Request, BaseResponse>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task<BaseResponse> Handle(Request command,
        CancellationToken cancellationToken)
    {
        var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await dbContext.Guilds
            .WhereIdIs(command.GuildId)
            .GetModulesOfType(command.Module)
            .Select(module => new { Module = module, module.Guild.ModChannelLog })
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null)
            throw new AnticipatedException("Could not find the settings for this server.");

        var modChannelLog = result.ModChannelLog;
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var guildModule = result.Module ?? command.Module switch
        {
            Module.Leveling => new GuildLevelSettings { GuildId = command.GuildId },
            Module.UserLog => new GuildUserLogSettings { GuildId = command.GuildId },
            Module.Moderation => new GuildModerationSettings { GuildId = command.GuildId },
            Module.MessageLog => new GuildMessageLogSettings { GuildId = command.GuildId },
            Module.Commands => new GuildCommandsSettings { GuildId = command.GuildId },
            _ => throw new NotImplementedException()
        };
        guildModule.ModuleEnabled = command.Enable;
        if (result.Module is null)
            await dbContext.AddAsync(guildModule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BaseResponse { LogChannelId = modChannelLog };
    }
}
}
