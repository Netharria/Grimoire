// -----------------------------------------------------------------------
// <copyright file="BaseRepositoryTestBase.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence.Tests.Repositories
{
    using System.Collections.Generic;
    using System.Linq;
    using Cybermancy.Persistence.Repositories;
    using Microsoft.EntityFrameworkCore;
    using MockQueryable.Moq;
    using Moq;

    /// <summary>
    /// The Test base for the repository for all objects from <see cref="Domain"/>.
    /// </summary>
    /// <typeparam name="T">Any Object from <see cref="Domain"/>.</typeparam>
    public class BaseRepositoryTestBase<T>
        where T : class
    {
        /// <summary>
        /// Gets the repository of type <see cref="Domain"/>.
        /// </summary>
        protected BaseRepository<T> MockRepository { get; private set; }

        /// <summary>
        /// Gets the mocked <see cref="DbContext"/>.
        /// </summary>
        protected Mock<CybermancyDbContext> MockDbContext { get; private set; }

        /// <summary>
        /// Gets the mocked <see cref="DbSet{TEntity}"/>.
        /// </summary>
        protected Mock<DbSet<T>> MockDbSet { get; private set; }

        /// <summary>
        /// Prepares the mocked items for testing.
        /// </summary>
        /// <param name="list">A list of objects that should be found in the <see cref="MockDbSet"/>.</param>
        public void InstatiateMocks(List<T> list)
        {
            this.MockDbSet = list.AsQueryable().BuildMockDbSet();
            var options = new DbContextOptions<CybermancyDbContext>();
            this.MockDbContext = new Mock<CybermancyDbContext>(options);
            this.MockDbContext.Setup(x => x.Set<T>()).Returns(this.MockDbSet.Object);

            this.MockRepository = new BaseRepository<T>(this.MockDbContext.Object);
        }
    }
}