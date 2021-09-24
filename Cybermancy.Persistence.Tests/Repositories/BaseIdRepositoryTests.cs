// -----------------------------------------------------------------------
// <copyright file="BaseIdRepositoryTests.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Persistence.Tests.Repositories
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cybermancy.Domain;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Tests for the method in <see cref="Persistence.Repositories.BaseIdRepository{T}"/>.
    /// </summary>
    public class BaseIdRepositoryTests : BaseIdRepositoryTestBase<Guild>
    {
        private Guild Guild1 { get; } = new () { Id = 1 };

        private Guild Guild2 { get; } = new () { Id = 2 };

        private List<Guild> List { get; } = new ();

        /// <summary>
        /// Runs the setup for needed for the tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            this.List.Add(this.Guild1);
            this.List.Add(this.Guild2);
            this.InstatiateMocks(this.List);
        }

        /// <summary>
        /// Checks if the repository can successfully find out if items exist in the database.
        /// </summary>
        /// <returns>The completed taks.</returns>
        [Test]
        public async Task WhenCheckingIfEntityExists_ReturnCorrectResultAsync()
        {
            Assert.IsTrue(await this.MockRepository.ExistsAsync(1), "Expected Item to exist and it did not.");
            Assert.IsFalse(await this.MockRepository.ExistsAsync(3), "Expected Item to not exist and it did.");
        }

        /// <summary>
        /// Checks if the repository can get the object out of the database.
        /// </summary>
        /// <returns>The completed taks.</returns>
        [Test]
        public async Task WhenGettingById_ReturnResultAsync()
        {
            this.MockDbSet.Setup(x => x.FindAsync(1UL)).ReturnsAsync(this.Guild1);
            var result = await this.MockRepository.GetByIdAsync(1);
            Assert.AreEqual(this.Guild1, result);
        }
    }
}