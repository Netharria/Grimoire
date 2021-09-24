// -----------------------------------------------------------------------
// <copyright file="BaseRepositoryTests.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence.Tests.Repositories
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cybermancy.Domain;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Tests for the method in <see cref="Persistence.Repositories.BaseRepository{T}"/>.
    /// </summary>
    [TestFixture]
    public class BaseRepositoryTests : BaseRepositoryTestBase<GuildLevelSettings>
    {
        private Guild Guild1 { get; } = new () { Id = 1 };

        private Guild Guild2 { get; } = new () { Id = 2 };

        private GuildLevelSettings Settings1 { get; set; }

        private GuildLevelSettings Settings2 { get; set; }

        private List<GuildLevelSettings> List { get; } = new ();

        /// <summary>
        /// Runs the setup for needed for the tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            this.Settings1 = new GuildLevelSettings { Guild = this.Guild1 };
            this.List.Add(this.Settings1);
            this.Settings2 = new GuildLevelSettings { Guild = this.Guild2 };
            this.List.Add(this.Settings2);
            this.InstatiateMocks(this.List);
        }

        /// <summary>
        /// Checks if the repository can get all items of type out of database.
        /// </summary>
        /// <returns>The completed task.</returns>
        [Test]
        public async Task GetAllAsync_ReturnsAllEntitiesAsync()
        {
            var result = await this.MockRepository.GetAllAsync();
            Assert.IsTrue(result.Any(x => x.Guild.Id == 1), "First Guild Setting Not Found");
            Assert.IsTrue(result.Any(x => x.Guild.Id == 2), "Second Guild Setting Not Found");
        }

        /// <summary>
        /// Checks if the repository can add item to database.
        /// </summary>
        /// <returns>The completed task.</returns>
        [Test]
        public async Task AddAsync_SavesEntityAsync()
        {
            await this.MockRepository.AddAsync(this.Settings1);
            this.MockDbContext.Verify(x => x.Set<GuildLevelSettings>(), Times.Once);
            this.MockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        /// <summary>
        /// Checks if the repository can delete item from database.
        /// </summary>
        /// <returns>The completed task.</returns>
        [Test]
        public async Task DeleteAsync_DeletesEntityAsync()
        {
            await this.MockRepository.DeleteAsync(this.Settings1);
            this.MockDbContext.Verify(x => x.Set<GuildLevelSettings>(), Times.Once);
            this.MockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }
    }
}