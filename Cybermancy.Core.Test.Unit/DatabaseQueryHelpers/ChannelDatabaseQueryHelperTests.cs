using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using NUnit.Framework;

namespace Cybermancy.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class ChannelDatabaseQueryHelperTests
    {
        [Test]
        public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
        {
            var context = TestCybermancyDbContextFactory.Create();
            var channelsToAdd = new List<ChannelDto>
            {
                new ChannelDto() { Id = 2, GuildId = TestCybermancyDbContextFactory.Guild1.Id},
                new ChannelDto() { Id = 3, GuildId = TestCybermancyDbContextFactory.Guild1.Id},
                new ChannelDto() { Id = 4, GuildId = TestCybermancyDbContextFactory.Guild1.Id},
                new ChannelDto() { Id = 5, GuildId = TestCybermancyDbContextFactory.Guild1.Id}
            };
            var result = await context.Channels.AddMissingChannelsAsync(channelsToAdd, default);

            await context.SaveChangesAsync();

            result.Should().BeTrue();
            context.Channels.Should().HaveCount(4);
        }
    }
}
