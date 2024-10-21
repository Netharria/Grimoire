// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Settings;
public partial class LogSettingsCommands
{
    public partial class User
    {
        [SlashCommand("View", "View the current settings for the User Log module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            await ctx.DeferAsync(true);
            var response = await this._mediator.Send(new GetUserLogSettings.Query{ GuildId = ctx.Guild.Id });
            var JoinChannelLog =
                response.JoinChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.JoinChannelLog.Value)).Mention;
            var LeaveChannelLog  =
                response.LeaveChannelLog  is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.LeaveChannelLog.Value)).Mention;
            var UsernameChannelLog =
                response.UsernameChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.UsernameChannelLog.Value)).Mention;
            var NicknameChannelLog =
                response.NicknameChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.NicknameChannelLog.Value)).Mention;
            var AvatarChannelLog =
                response.AvatarChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.AvatarChannelLog.Value)).Mention;
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                $"**Join Log:** {JoinChannelLog}\n" +
                $"**Leave Log:** {LeaveChannelLog}\n" +
                $"**Username Log:** {UsernameChannelLog}\n" +
                $"**Nickname Log:** {NicknameChannelLog}\n" +
                $"**Avatar Log:** {AvatarChannelLog}\n");
        }
    }
}

public sealed class GetUserLogSettings
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }


    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            return await this._grimoireDbContext.GuildUserLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    JoinChannelLog = x.JoinChannelLogId,
                    LeaveChannelLog = x.LeaveChannelLogId,
                    UsernameChannelLog = x.UsernameChannelLogId,
                    NicknameChannelLog = x.NicknameChannelLogId,
                    AvatarChannelLog = x.AvatarChannelLogId,
                    IsLoggingEnabled = x.ModuleEnabled
                }).FirstAsync(cancellationToken: cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? JoinChannelLog { get; init; }
        public ulong? LeaveChannelLog { get; init; }
        public ulong? UsernameChannelLog { get; init; }
        public ulong? NicknameChannelLog { get; init; }
        public ulong? AvatarChannelLog { get; init; }
        public bool IsLoggingEnabled { get; init; }
    }
}
