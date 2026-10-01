using System;
using MessagePack;

namespace ElinTogether.Models;

/// <summary>
///     Net packet: Host -> Client
/// </summary>
[MessagePackObject]
public class SaveDataProbe
{
    [Key(0)]
    public required LZ4Bytes Game { get; init; }

    [Key(1)]
    public required int RemoteCharaUid { get; init; }

    [Key(2)] public Guid ProfileSession { get; init; }
    [Key(3)] public Guid ControlResume { get; init; }

    public Game MakeGameSave()
    {
        return Game.Decompress<Game>();
    }

    public static SaveDataProbe Create(int uid, Guid profileSession, Guid controlResume = default)
    {
        return new() {
            Game = LZ4Bytes.Create(EClass.game),
            RemoteCharaUid = uid,
            ProfileSession = profileSession,
            ControlResume = controlResume,
        };
    }
}
