using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybermancy.Persistence.Tests.Repositories
{
    [TestFixture]
    public class BaseRepositoryTests : BaseRepositoryTestBase<GuildLevelSettings>
    {
        private Guild Guild1 { get; set; } = new Guild { Id = 1 };
        private Guild Guild2 { get; set; } = new Guild { Id = 2 };
        private GuildLevelSettings Settings1 { get; set; }
        private GuildLevelSettings Settings2 { get; set; }
        private List<GuildLevelSettings> List { get; } = new List<GuildLevelSettings>();

        [SetUp]
        public void SetUp()
        {
            Settings1 = new GuildLevelSettings { Guild = Guild1 };
            List.Add(Settings1);
            Settings2 = new GuildLevelSettings { Guild = Guild2 };
            List.Add(Settings2);
            base.InstatiateMocks(List);
        }

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
