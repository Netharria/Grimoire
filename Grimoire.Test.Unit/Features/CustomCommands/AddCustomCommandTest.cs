//This file is part of the Grimoire Project.

// Copyright (c) Netharia 2021-Present.

// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Grimoire.Features.CustomCommands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Grimoire.Test.Unit.Features.CustomCommands;

[Collection("Test collection")]
public class AddCustomCommandTest : IAsyncLifetime
{
    private readonly DbContext _dbContext;
    private readonly Func<Task> _resetDatabase;
    private readonly IHost _host;

    public AddCustomCommandTest(GrimoireCoreFactory factory)
    {
        this._host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services)
                => services.AddDbContext<GrimoireDbContext>(options =>
                    options.UseNpgsql(factory.ConnectionString))
                .AddMediatR(options => options.RegisterServicesFromAssemblyContaining<AddCustomCommand.Handler>())).Build();

        this._dbContext = this._host.Services.GetRequiredService<GrimoireDbContext>();
        this._resetDatabase = factory.ResetDatabase;
    }

    public async Task InitializeAsync()
    {
        await this._host.StartAsync();
        return;
        //await this._dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await this._resetDatabase();
        await this._host.StopAsync();
    }

    //[Fact]
    //public async Task GivenThereIsASpaceInTheCommandName_WhenLearnCommandIsCalled_ThenShouldReplyWithErrorResponse()
    //{

    //    var mediator = _host.Services.GetRequiredService<IMediator>();

    //    var CUT = new CustomCommandSettings(mediator);

    //    await CUT.Learn(context, "something else", "");

        
    //}

}
