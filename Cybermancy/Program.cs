// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy
{
    using System.IO;
    using Cybermancy.Core;
    using Cybermancy.Persistence;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The startup class for Cybermancy.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Starts Cybermancy.
        /// </summary>
        /// <param name="args">This is an empty string array.</param>
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            host.Run();
        }

        /// <summary>
        /// Initializes the hosted service for discord.
        /// </summary>
        /// <param name="args">Standard arguments from program startup.</param>
        /// <returns>The Configured Host.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(x =>
                {
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();
                    x.AddConfiguration(configuration);
                })
                .ConfigureLogging((context, x) =>
                {
                    x.AddConsole();
                    x.AddConfiguration(context.Configuration);
                })
                .ConfigureServices((context, services) =>
                {
                    services
                        .AddPersistenceServices(context.Configuration)
                        .AddCoreServices(context.Configuration);
                })
                .UseConsoleLifetime();
        }
    }
}