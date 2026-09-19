// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Alerts;

public interface IAlertSender
{
    /// <summary>Queues an alert. Never blocks or throws; alerts are dropped if the queue is full.</summary>
    void Send(Alert alert);
}
