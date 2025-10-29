using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Migrations
{
    /// <inheritdoc />
    public partial class LeaderboardView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW leaderboard_view AS
                SELECT
                    ""GuildId"",
                    ""UserId"",
                    SUM(""Xp"") as ""TotalXp"",
                    ROW_NUMBER() OVER (PARTITION BY ""GuildId"" ORDER BY SUM(""Xp"") DESC) as ""Rank""
                FROM ""XpHistory""
                GROUP BY ""GuildId"", ""UserId"";

                CREATE UNIQUE INDEX idx_leaderboard_view_guild_user
                ON leaderboard_view (""GuildId"", ""UserId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS leaderboard_view;");
        }
    }
}
