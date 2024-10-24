////This file is part of the Grimoire Project.

//// Copyright (c) Netharia 2021-Present.

//// All rights reserved.
//// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using DSharpPlus.Entities;
//using DSharpPlus.EventArgs;
//using DSharpPlus;
//using Grimoire.Domain;
//using Grimoire.Features.CustomCommands;
//using Grimoire.Features.Leveling.Events;
//using Grimoire.Features.Leveling.Settings;
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using NSubstitute;
//using Xunit;
//using System.Threading;

//namespace Grimoire.Test.Unit.Features.Leveling.Events;

//[Collection("Test collection")]
//public class GainUserXpTestsIntegration : IAsyncLifetime
//{
//    private readonly GrimoireDbContext _dbContext;
//    private readonly Func<Task> _resetDatabase;
//    private readonly IHost _host;
//    private readonly IMediator _mediator;

//    public GainUserXpTestsIntegration(GrimoireCoreFactory factory)
//    {
//        _host = Host.CreateDefaultBuilder()
//            .ConfigureServices((context, services)
//                => services.AddDbContext<GrimoireDbContext>(options =>
//                    options.UseNpgsql(factory.ConnectionString))
//                .AddMediatR(options => options.RegisterServicesFromAssemblyContaining<AddCustomCommand.Handler>())).Build();

//        _dbContext = _host.Services.GetRequiredService<GrimoireDbContext>();
//        _mediator = _host.Services.GetRequiredService<IMediator>();
//        _resetDatabase = factory.ResetDatabase;
//    }

//    public async Task InitializeAsync()
//    {
//        await _host.StartAsync();
//        var guild = new Guild
//        {
//            Id = 1,
//            LevelSettings = new GuildLevelSettings
//            {
//                ModuleEnabled = true
//            }
//        };

//        var user = new User
//        {
//            Id = 1,
//        };

//        var member = new Member
//        {
//            UserId = 1,
//            GuildId = 1,
//            XpHistory = []
//        };

//        await _dbContext.Guilds.AddAsync(guild);
//        await _dbContext.Users.AddAsync(user);
//        await _dbContext.Members.AddAsync(member);
//        await _dbContext.SaveChangesAsync();
//    }

//    public async Task DisposeAsync()
//    {
//        await this._resetDatabase();
//        await _host.WaitForShutdownAsync();
//    }

//}
