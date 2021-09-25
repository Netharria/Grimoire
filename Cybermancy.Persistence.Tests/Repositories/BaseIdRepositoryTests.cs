// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using Moq;
using NUnit.Framework;

namespace Cybermancy.Persistence.Tests.Repositories
{
    public class BaseIdRepositoryTests : BaseIdRepositoryTestBase<Guild>
    {
        private readonly Guild _guild1 = new () { Id = 1 };
        private readonly Guild _guild2 =  new () { Id = 2 };
        private readonly List<Guild> _list = new ();

        [SetUp]
        public void SetUp()
        {
            this._list.Add(this._guild1);
            this._list.Add(this._guild2);
            this.InstatiateMocks(this._list);
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
            this.MockDbSet.Setup(x => x.FindAsync(1UL)).ReturnsAsync(this._guild1);
            var result = await this.MockRepository.GetByIdAsync(1);
            Assert.AreEqual(this._guild1, result);
        }
    }
}
