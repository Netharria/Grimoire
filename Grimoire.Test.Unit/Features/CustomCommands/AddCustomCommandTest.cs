//// This file is part of the Grimoire Project.
////
//// Copyright (c) Netharia 2021-Present.
////
//// All rights reserved.
//// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

//using System;
//using System.Threading.Tasks;
//using DSharpPlus.Entities;
//using DSharpPlus.SlashCommands;
//using FakeItEasy;
//using Grimoire.Extensions;
//using Grimoire.Features.CustomCommands;
//using Grimoire.Structs;
//using Mediator;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Xunit;

//namespace Grimoire.Test.Unit.Features.CustomCommands;

//[Collection("Test collection")]
//public class AddCustomCommandTest(GrimoireCoreFactory factory) : IAsyncLifetime
//{
//    private readonly GrimoireCoreFactory _factory = factory;
//    private readonly Func<Task> _resetDatabase = factory.ResetDatabase;

//    public async Task InitializeAsync()
//    {
//        //await this._dbContext.SaveChangesAsync();
//    }

//    public Task DisposeAsync() => this._resetDatabase();

//    [Fact]
//    public async Task GivenThereIsASpaceInTheCommandName_WhenLearnCommandIsCalled_ThenShouldReplyWithErrorResponse()
//    {
//        using var webApplication = new HostBuilder<Program>(_factory);

//        var host = webApplication.Services.GetRequiredService<IHost>();
//        await host.StartAsync();
//        var scope = webApplication.Server.Services.CreateScope();
//        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


//        var CUT = new AddCustomCommand.Command(mediator);
//        var context = A.Fake<InteractionContext>();
//        A.CallTo(() => context.DeferAsync(A<bool>.That.Equals(false)))
//            .Returns(Task.CompletedTask);
//        A.CallTo(() => context.EditReplyAsync(A<DiscordColor>.Ignored,
//            A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<DiscordEmbed>.Ignored, A<DateTime>.Ignored)).Returns(Task.CompletedTask);

//        await CUT.Learn(context, "something else", "");

//        await host.StopAsync();
//    }

//}
