// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Cybermancy.Extensions
{
    public class ServiceActivator
    {
        internal static IServiceProvider? _serviceProvider = null;

        public static void Configure(IServiceProvider serviceProvider)
            => _serviceProvider = serviceProvider;

        public static IServiceScope GetScope(IServiceProvider? serviceProvider = null)
        {
            var provider = serviceProvider ?? _serviceProvider;
            var scope = provider?
                .GetRequiredService<IServiceScopeFactory>()
                .CreateScope();
            if (scope == null) throw new InvalidOperationException("Could Not Create Scope");
            return scope;
        }
    }
}
