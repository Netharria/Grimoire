using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [UsedImplicitly]
    [Command("View")]
    [Description("View the current general settings for this server.")]
    public async Task ViewAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        var moderationLogText = guildSettings.ModLogChannelId is null
            ? "None"
            : ChannelExtensions.Mention(guildSettings.ModLogChannelId.Value);
        var userCommandChannelText = guildSettings.UserCommandChannelId is null
            ? "None"
            : ChannelExtensions.Mention(guildSettings.UserCommandChannelId.Value);
        await ctx.EditReplyAsync(title: "General Settings",
            message: $"**Moderation Log:** {moderationLogText}\n**User Command Channel:** {userCommandChannelText}");
    }
}

public sealed class GetGeneralSettings
{
    public sealed record Query : IRequest<Response>
    {
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(query.GuildId)
                .Select(x => new { x.ModChannelLog, x.UserCommandChannelId })
                .FirstOrDefaultAsync(cancellationToken);
            return new Response
            {
                ModLogChannel = result?.ModChannelLog, UserCommandChannel = result?.UserCommandChannelId
            };
        }
    }

    public sealed record Response
    {
        public ulong? ModLogChannel { get; init; }
        public ulong? UserCommandChannel { get; init; }
    }
}
