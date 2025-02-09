// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    [SlashCommand("Set", "Enable or Disable a module")]
    public async Task SetAsync(InteractionContext ctx,
        [Option("Module", "The module to enable or disable")]
        Module module,
        [Option("Enable", "Whether to enable or disable the module")]
        bool enable)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new EnableModule.Request
        {
            GuildId = ctx.Guild.Id, Module = module, Enable = enable
        });
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message:
            $"{ctx.Member.GetUsernameWithDiscriminator()} {(enable ? "Enabled" : "Disabled")} {module.GetName()}");
        await ctx.EditReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module.GetName()}");
    }
}


internal sealed class EnableModule
{
    public sealed record Request : IRequest<Response>
{
    public ulong GuildId { get; init; }
    public Module Module { get; init; }
    public bool Enable { get; init; }
}

public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IRequestHandler<Request, Response>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task<Response> Handle(Request command,
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
        return new Response { ModerationLog = modChannelLog };
    }
}

public sealed record Response : BaseResponse
{
    public ulong? ModerationLog { get; init; }
}
}
