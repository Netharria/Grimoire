// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire;
using Microsoft.Extensions.Hosting;

AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", false);

// CreateDefaultBuilder loads appsettings.json, then appsettings.{Environment}.json, then (in Development) user
// secrets, then environment variables, then the command line. Later sources win, so an environment variable such as
// TOKEN or CONNECTIONSTRINGS__GRIMOIRE overrides the shipped file. Don't add the file again after them (SH-1).
await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) => services.AddGrimoire(context.Configuration))
    .RunConsoleAsync();
