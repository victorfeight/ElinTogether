using System;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

public enum PlayerControlMode { Human, Companion, Resuming }

[MessagePackObject]
public sealed class PlayerControlRequest
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public bool TakeBreak { get; set; }
}

[MessagePackObject]
public sealed class PlayerControlReply
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public PlayerControlMode Mode { get; set; }
    [Key(2)] public string? Error { get; set; }
}

[MessagePackObject]
public sealed class PlayerControlReady
{
    [Key(0)] public Guid Id { get; set; }
}

internal static class PlayerControl
{
    internal static bool LocalInputBlocked => NetSession.Instance.Connection switch {
        ElinNetClient client => client.ControlInputBlocked,
        ElinNetHost host => host.States.TryGetValue(0, out var state) && state.Control != PlayerControlMode.Human,
        _ => false,
    };

    internal static bool WatchingOwnCharacter => NetSession.Instance.Connection is ElinNetClient client &&
        client.ControlMode == PlayerControlMode.Companion;

}
