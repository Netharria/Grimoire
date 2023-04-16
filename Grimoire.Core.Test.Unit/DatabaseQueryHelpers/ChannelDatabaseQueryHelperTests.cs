using System.Collections.Generic;
using System.Threading.Tasks;
using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Shared.SharedDtos;
using FluentAssertions;
using NUnit.Framework;

namespace Grimoire.Core.Test.Unit.DatabaseQueryHelpers
{
    [TestFixture]
    public class ChannelDatabaseQueryHelperTests
    {
        public TestDatabaseFixture DatabaseFixture { get; set; } = null!;

        
        [OneTimeSetUp]
        public void Setup() => this.DatabaseFixture = new TestDatabaseFixture();

        [Test]
        public async Task WhenChannelsAreNotInDatabase_AddThemAsync()
        {
            var context = this.DatabaseFixture.CreateContext();
            context.Database.BeginTransaction();
            var channelsToAdd = new List<ChannelDto>
            {
                new ChannelDto() { Id = 2, GuildId = TestDatabaseFixture.Guild1.Id},
                new ChannelDto() { Id = 3, GuildId = TestDatabaseFixture.Guild1.Id},
                new ChannelDto() { Id = 4, GuildId = TestDatabaseFixture.Guild1.Id},
                new ChannelDto() { Id = 5, GuildId = TestDatabaseFixture.Guild1.Id}
            };
            var result = await context.Channels.AddMissingChannelsAsync(channelsToAdd, default);

            await context.SaveChangesAsync();

            result.Should().BeTrue();
            context.Channels.Should().HaveCount(5);
        }
    }
}
