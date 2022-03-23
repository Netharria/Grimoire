// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Shared.SharedDtos
{
    public class UserDto
    {
        public ulong Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }
}
