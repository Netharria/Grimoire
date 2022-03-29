// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Domain;
using MediatR;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain
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
            Member[]? allUsersToIgnore = null;
            Role[]? allRolesToIgnore = null;
            Channel[]? allChannelsToIgnore = null;
            var newIgnoredItems = new StringBuilder();

            if (request.Users.Any())
                allUsersToIgnore = this._cybermancyDbContext.Members
                    .WhereMembersHaveIds(userIds, request.GuildId)
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();
            if (request.RoleIds.Any())
                allRolesToIgnore = this._cybermancyDbContext.Roles
                    .WhereIdsAre(request.RoleIds)
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();

            if (request.ChannelIds.Any())
                allChannelsToIgnore = this._cybermancyDbContext.Channels
                    .WhereIdsAre(request.ChannelIds)
                    .UpdateIgnoredStatus(request.ShouldIgnore, newIgnoredItems)
                    .ToArray();


            if (allUsersToIgnore is not null)
                this._cybermancyDbContext.Members.UpdateRange(allUsersToIgnore);

            if (allRolesToIgnore is not null)
                this._cybermancyDbContext.Roles.UpdateRange(allRolesToIgnore);

            if (allChannelsToIgnore is not null)
                this._cybermancyDbContext.Channels.UpdateRange(allChannelsToIgnore);

            if (allUsersToIgnore is not null && request.Users.Count() != allUsersToIgnore.Length)
            {
                var usersNotUpdated = allUsersToIgnore.Select(x => x.UserId).Except(userIds).ToArray();
                var existingUsersToBeAddedToGuild = this._cybermancyDbContext.Users
                    .WhereIdsAre(usersNotUpdated)
                    .ToArray()
                    .Select(x =>
                    {
                        newIgnoredItems.Append(x.Mention()).Append(' ');
                        x.MemberProfiles.Add(new Member
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
                            UsernameHistories = new List<UsernameHistory> {
                                new UsernameHistory {
                                    Username = x.UserName,
                                    UserId = x.Id
                                }
                            }
                        };
                        var member = new Member
                        {
                            GuildId = request.GuildId,
                            IsXpIgnored = request.ShouldIgnore,
                            UserId = x.Id
                        };
                        if(!string.IsNullOrWhiteSpace(x.Nickname))
                            member.NicknamesHistory.Add(
                                new NicknameHistory
                                {
                                    Nickname = x.Nickname,
                                    GuildId = request.GuildId,
                                    UserId = x.Id
                                });
                        user.MemberProfiles.Add(member);
                        newIgnoredItems.Append(user.Mention()).Append(' ');
                        return user;
                    }).ToArray();

                if (newUserToBeAdded.Any())
                    this._cybermancyDbContext.Users.UpdateRange(newUserToBeAdded);
            }
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);


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
