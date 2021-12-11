// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using Cybermancy.Domain;
using MediatR;

namespace Cybermancy.Core.Features.Leveling.Commands.UpdateIgnoreStateForXpGain
{
    public class UpdateIgnoreStateForXpGainCommandHandler : IRequestHandler<UpdateIgnoreStateForXpGainCommand, UpdateIgnoreStateForXpGainResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateIgnoreStateForXpGainCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<UpdateIgnoreStateForXpGainResponse> Handle(UpdateIgnoreStateForXpGainCommand request, CancellationToken cancellationToken)
        {
            var userIds = request.Users.Select(x => x.Id).ToArray();
            GuildUser[]? allUsersToIgnore = null;
            Role[]? allRolesToIgnore = null;
            Channel[]? allChannelsToIgnore = null;
            var newIgnoredItems = new StringBuilder();

            if (request.Users.Any())
                allUsersToIgnore = this._cybermancyDbContext.GuildUsers
                    .Where(x => x.GuildId == request.GuildId)
                    .Where(x => userIds.Contains(x.UserId))
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();
            if (request.RoleIds.Any())
                allRolesToIgnore = this._cybermancyDbContext.Roles
                    .Where(x => request.RoleIds.Contains(x.Id))
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();

            if (request.ChannelIds.Any())
                allChannelsToIgnore = this._cybermancyDbContext.Channels
                    .Where(x => request.ChannelIds.Contains(x.Id))
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();


            if (allUsersToIgnore is not null)
                this._cybermancyDbContext.GuildUsers.UpdateRange(allUsersToIgnore);

            if (allRolesToIgnore is not null)
                this._cybermancyDbContext.Roles.UpdateRange(allRolesToIgnore);

            if (allChannelsToIgnore is not null)
                this._cybermancyDbContext.Channels.UpdateRange(allChannelsToIgnore);

            if (allUsersToIgnore is not null && request.Users.Count() != allUsersToIgnore.Length)
            {
                var usersNotUpdated = allUsersToIgnore.Select(x => x.Id).Except(userIds).ToArray();
                var existingUsersToBeAddedToGuild = this._cybermancyDbContext.Users
                    .Where(x => usersNotUpdated.Contains(x.Id))
                    .ToArray()
                    .Select(x =>
                    {
                        newIgnoredItems.Append(x.Mention()).Append(' ');
                        x.GuildMembers.Add(new GuildUser
                        {
                            GuildId = request.GuildId,
                            IsXpIgnored = request.ShouldIgnore
                        });
                        return x;
                    });
                if (existingUsersToBeAddedToGuild.Any())
                    this._cybermancyDbContext.Users.UpdateRange(existingUsersToBeAddedToGuild);

                var nonRegisteredUsers = existingUsersToBeAddedToGuild.Select(y => y.Id).Except(usersNotUpdated).ToArray();
                var newUserToBeAdded = request.Users
                    .Where(x => nonRegisteredUsers.Contains(x.Id))
                    .Select(x =>
                    {
                        var user = new User
                        {
                            Id = x.Id,
                            UserName = x.UserName,
                            DisplayName = x.DisplayName,
                            AvatarUrl = x.AvatarUrl,

                        };
                        user.GuildMembers.Add(new GuildUser
                        {
                            GuildId = request.GuildId,
                            IsXpIgnored = request.ShouldIgnore
                        });
                        newIgnoredItems.Append(user.Mention()).Append(' ');
                        return user;
                    }).ToArray();

                if (newUserToBeAdded.Any())
                    this._cybermancyDbContext.Users.UpdateRange(newUserToBeAdded);

            }
            await _cybermancyDbContext.SaveChangesAsync();


            var notFoundInDatabase = new StringBuilder();
            if (allRolesToIgnore is not null && request.ChannelIds.Length != allRolesToIgnore.Length)
            {
                var rolesNotUpdated = allRolesToIgnore.Select(x => x.Id).Except(request.RoleIds).ToArray();
                foreach (var role in rolesNotUpdated)
                    notFoundInDatabase.Append(role).Append(' ');
            }

            if (allChannelsToIgnore is not null && request.ChannelIds.Length != allChannelsToIgnore.Length)
            {
                var channelsNotUpdated = allChannelsToIgnore.Select(x => x.Id).Except(request.ChannelIds).ToArray();
                foreach (var channel in channelsNotUpdated)
                    notFoundInDatabase.Append(channel).Append(' ');
            }

            var couldNotMatch = new StringBuilder();
            if (request.InvalidIds.Any())
                foreach (var id in request.InvalidIds)
                    couldNotMatch.Append(id).Append(' ');

            var finalString = new StringBuilder();
            if (notFoundInDatabase.Length > 0) finalString.Append(notFoundInDatabase).Append("were not found in database. ");
            if (couldNotMatch.Length > 0) finalString.Append("Could not match ").Append(couldNotMatch).Append("with a role, channel or user. ");
            if (newIgnoredItems.Length > 0) finalString.Append(newIgnoredItems).Append(request.ShouldIgnore ? " are now ignored for xp gain." : " are now being watched for xp gain.");

            return new UpdateIgnoreStateForXpGainResponse { Success = true, Message = finalString.ToString() };
        }


    }
}
