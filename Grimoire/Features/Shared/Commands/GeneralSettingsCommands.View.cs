using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Shared.Commands;
internal sealed partial class GeneralSettingsCommands
{
    [SlashCommand("View", "View the current general settings.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            await ctx.DeferAsync(true);
            var response = await this._mediator.Send(new GetGeneralSettings.Query { GuildId = ctx.Guild.Id });
            var moderationLogText = response.ModLogChannel is null
                ? "None"
                : ChannelExtensions.Mention(response.ModLogChannel.Value);
            var userCommandChannelText = response.UserCommandChannel is null
                ? "None"
                : ChannelExtensions.Mention(response.UserCommandChannel.Value);
            await ctx.EditReplyAsync(title: "General Settings",
                message: $"**Moderation Log:** {moderationLogText}\n**User Command Channel:** {userCommandChannelText}");
        }
}

public sealed class GetGeneralSettings
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
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
