// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Domain.Documents;

/// <summary>
///     Which modules are on (C13): one document for all six, so the check every gated command and event makes reads
///     one small document, and a disabled module's own document is never loaded. A missing flag is off. General has
///     no flag: it is always on.
/// </summary>
public sealed record ModuleStates : ISettingsDocument<ModuleStates>
{
    public bool Leveling { get; init; }
    public bool UserLog { get; init; }
    public bool Moderation { get; init; }
    public bool MessageLog { get; init; }
    public bool Commands { get; init; }
    public bool AntiSpam { get; init; }

    public static SettingsDocument Kind => SettingsDocument.Modules;
    public static ModuleStates Empty { get; } = new();

    public bool IsEnabled(Module module)
        => module switch
        {
            Module.General => true,
            Module.Leveling => this.Leveling,
            Module.UserLog => this.UserLog,
            Module.Moderation => this.Moderation,
            Module.MessageLog => this.MessageLog,
            Module.Commands => this.Commands,
            Module.AntiSpam => this.AntiSpam,
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, null)
        };

    public Fin<ModuleStates> With(Module module, bool enabled)
        => module switch
        {
            Module.General => new Invalid("Cannot disable the general module."),
            Module.Leveling => this with { Leveling = enabled },
            Module.UserLog => this with { UserLog = enabled },
            Module.Moderation => this with { Moderation = enabled },
            Module.MessageLog => this with { MessageLog = enabled },
            Module.Commands => this with { Commands = enabled },
            Module.AntiSpam => this with { AntiSpam = enabled },
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, null)
        };
}

/// <summary>The settings that belong to no module: the moderation log and the user command channel.</summary>
public sealed record GeneralConfig : ILogChannelDocument<GeneralConfig>
{
    public ChannelId? ModerationLogChannel { get; init; }
    public ChannelId? UserCommandChannel { get; init; }

    public static SettingsDocument Kind => SettingsDocument.General;
    public static GeneralConfig Empty { get; } = new();

    public ChannelId? LogChannel(GuildLogType type)
        => type is GuildLogType.Moderation
            ? this.ModerationLogChannel
            : throw SettingsDocumentExtensions.NotIn(type, Kind);

    public GeneralConfig WithLogChannel(GuildLogType type, ChannelId? channel)
        => type is GuildLogType.Moderation
            ? this with { ModerationLogChannel = channel }
            : throw SettingsDocumentExtensions.NotIn(type, Kind);
}

public sealed record LevelingConfig : ILogChannelDocument<LevelingConfig>
{
    public XpGainAmount? XpGainAmount { get; init; }
    public LevelScalingBase? LevelScalingBase { get; init; }
    public LevelScalingModifier? LevelScalingModifier { get; init; }

    [JsonPropertyName("XpTimeoutSeconds")]
    public XpTimeoutPeriod? XpTimeout { get; init; }

    public ChannelId? LogChannel { get; init; }

    public static SettingsDocument Kind => SettingsDocument.Leveling;
    public static LevelingConfig Empty { get; } = new();

    /// <summary>The settings with each missing one filled in with its default.</summary>
    public LevelingSettingEntry Resolve()
        => new(
            this.XpTimeout ?? XpTimeoutPeriod.Default,
            this.LevelScalingModifier ?? Values.LevelScalingModifier.Default,
            this.LevelScalingBase ?? Values.LevelScalingBase.Default,
            this.XpGainAmount ?? Values.XpGainAmount.Default);

    ChannelId? ILogChannelDocument<LevelingConfig>.LogChannel(GuildLogType type)
        => type is GuildLogType.Leveling ? this.LogChannel : throw SettingsDocumentExtensions.NotIn(type, Kind);

    public LevelingConfig WithLogChannel(GuildLogType type, ChannelId? channel)
        => type is GuildLogType.Leveling
            ? this with { LogChannel = channel }
            : throw SettingsDocumentExtensions.NotIn(type, Kind);
}

public sealed record MessageLogConfig : ILogChannelDocument<MessageLogConfig>
{
    public ChannelId? DeleteLogChannel { get; init; }
    public ChannelId? BulkDeleteLogChannel { get; init; }
    public ChannelId? EditLogChannel { get; init; }

    public static SettingsDocument Kind => SettingsDocument.MessageLog;
    public static MessageLogConfig Empty { get; } = new();

    public ChannelId? LogChannel(GuildLogType type)
        => type switch
        {
            GuildLogType.MessageDeleted => this.DeleteLogChannel,
            GuildLogType.BulkMessageDeleted => this.BulkDeleteLogChannel,
            GuildLogType.MessageEdited => this.EditLogChannel,
            _ => throw SettingsDocumentExtensions.NotIn(type, Kind)
        };

    public MessageLogConfig WithLogChannel(GuildLogType type, ChannelId? channel)
        => type switch
        {
            GuildLogType.MessageDeleted => this with { DeleteLogChannel = channel },
            GuildLogType.BulkMessageDeleted => this with { BulkDeleteLogChannel = channel },
            GuildLogType.MessageEdited => this with { EditLogChannel = channel },
            _ => throw SettingsDocumentExtensions.NotIn(type, Kind)
        };
}

public sealed record ModerationConfig : ILogChannelDocument<ModerationConfig>
{
    public ChannelId? PublicModerationLogChannel { get; init; }

    [JsonPropertyName("AutoPardonAfterSeconds")]
    public TimeSpan? AutoPardonAfter { get; init; }

    public RoleId? MuteRole { get; init; }

    public static SettingsDocument Kind => SettingsDocument.Moderation;
    public static ModerationConfig Empty { get; } = new();

    public ChannelId? LogChannel(GuildLogType type)
        => type is GuildLogType.PublicModeration
            ? this.PublicModerationLogChannel
            : throw SettingsDocumentExtensions.NotIn(type, Kind);

    public ModerationConfig WithLogChannel(GuildLogType type, ChannelId? channel)
        => type is GuildLogType.PublicModeration
            ? this with { PublicModerationLogChannel = channel }
            : throw SettingsDocumentExtensions.NotIn(type, Kind);
}

public sealed record UserLogConfig : ILogChannelDocument<UserLogConfig>
{
    public ChannelId? JoinLogChannel { get; init; }
    public ChannelId? LeaveLogChannel { get; init; }
    public ChannelId? UsernameLogChannel { get; init; }
    public ChannelId? NicknameLogChannel { get; init; }
    public ChannelId? AvatarLogChannel { get; init; }

    public static SettingsDocument Kind => SettingsDocument.UserLog;
    public static UserLogConfig Empty { get; } = new();

    public ChannelId? LogChannel(GuildLogType type)
        => type switch
        {
            GuildLogType.UserJoined => this.JoinLogChannel,
            GuildLogType.UserLeft => this.LeaveLogChannel,
            GuildLogType.UsernameUpdated => this.UsernameLogChannel,
            GuildLogType.NicknameUpdated => this.NicknameLogChannel,
            GuildLogType.AvatarUpdated => this.AvatarLogChannel,
            _ => throw SettingsDocumentExtensions.NotIn(type, Kind)
        };

    public UserLogConfig WithLogChannel(GuildLogType type, ChannelId? channel)
        => type switch
        {
            GuildLogType.UserJoined => this with { JoinLogChannel = channel },
            GuildLogType.UserLeft => this with { LeaveLogChannel = channel },
            GuildLogType.UsernameUpdated => this with { UsernameLogChannel = channel },
            GuildLogType.NicknameUpdated => this with { NicknameLogChannel = channel },
            GuildLogType.AvatarUpdated => this with { AvatarLogChannel = channel },
            _ => throw SettingsDocumentExtensions.NotIn(type, Kind)
        };
}
