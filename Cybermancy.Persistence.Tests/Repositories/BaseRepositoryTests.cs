using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Domain;
using Moq;
using NUnit.Framework;

namespace Cybermancy.Persistence.Tests.Repositories
{
    [TestFixture]
    public class BaseRepositoryTests : BaseRepositoryTestBase<GuildLevelSettings>
    {
        [SetUp]
        public void SetUp()
        {
            Settings1 = new GuildLevelSettings {Guild = Guild1};
            List.Add(Settings1);
            Settings2 = new GuildLevelSettings {Guild = Guild2};
            List.Add(Settings2);
            InstatiateMocks(List);
        }

        private Guild Guild1 { get; } = new() {Id = 1};
        private Guild Guild2 { get; } = new() {Id = 2};
        private GuildLevelSettings Settings1 { get; set; }
        private GuildLevelSettings Settings2 { get; set; }
        private List<GuildLevelSettings> List { get; } = new();

        [Test]
        public async Task GetAllAsync_ReturnsAllEntities()
        {
            var result = await MockRepository.GetAllAsync();
            Assert.IsTrue(result.Any(x => x.Guild.Id == 1), "First Guild Setting Not Found");
            Assert.IsTrue(result.Any(x => x.Guild.Id == 2), "Second Guild Setting Not Found");
        }

        [Test]
        public async Task AddAsync_SavesEntity()
        {
            await MockRepository.AddAsync(Settings1);
            MockDbContext.Verify(x => x.Set<GuildLevelSettings>(), Times.Once);
            MockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task DeleteAsync_DeletesEntity()
        {
            await MockRepository.DeleteAsync(Settings1);
            MockDbContext.Verify(x => x.Set<GuildLevelSettings>(), Times.Once);
            MockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }
    }
}