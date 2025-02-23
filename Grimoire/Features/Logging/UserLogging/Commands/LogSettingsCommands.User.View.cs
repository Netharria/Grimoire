// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands;

// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class User
    {
        [Command("View")]
        [Description("View the current settings for the User Log module.")]
        public async Task ViewAsync(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync(true);

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var response = await this._mediator.Send(new GetUserLogSettings.Query { GuildId = ctx.Guild.Id });
            if (response is null)
            {
                await ctx.EditReplyAsync(GrimoireColor.Red,
                    "User Logging settings could not be found for this server.");
                return;
            }

            var joinChannelLog =
                response.JoinChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.JoinChannelLog.Value)).Mention;
            var leaveChannelLog =
                response.LeaveChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.LeaveChannelLog.Value)).Mention;
            var usernameChannelLog =
                response.UsernameChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.UsernameChannelLog.Value)).Mention;
            var nicknameChannelLog =
                response.NicknameChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.NicknameChannelLog.Value)).Mention;
            var avatarChannelLog =
                response.AvatarChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.AvatarChannelLog.Value)).Mention;
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                         $"**Join Log:** {joinChannelLog}\n" +
                         $"**Leave Log:** {leaveChannelLog}\n" +
                         $"**Username Log:** {usernameChannelLog}\n" +
                         $"**Nickname Log:** {nicknameChannelLog}\n" +
                         $"**Avatar Log:** {avatarChannelLog}\n");
        }
    }
}

public sealed class GetUserLogSettings
{
    public sealed record Query : IRequest<Response?>
    {
        public ulong GuildId { get; init; }
    }


    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.GuildUserLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    JoinChannelLog = x.JoinChannelLogId,
                    LeaveChannelLog = x.LeaveChannelLogId,
                    UsernameChannelLog = x.UsernameChannelLogId,
                    NicknameChannelLog = x.NicknameChannelLogId,
                    AvatarChannelLog = x.AvatarChannelLogId,
                    IsLoggingEnabled = x.ModuleEnabled
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? JoinChannelLog { get; init; }
        public ulong? LeaveChannelLog { get; init; }
        public ulong? UsernameChannelLog { get; init; }
        public ulong? NicknameChannelLog { get; init; }
        public ulong? AvatarChannelLog { get; init; }
        public required bool IsLoggingEnabled { get; init; }
    }
}
