using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElinTogether.Models;

// Recovery evidence only: never apply an old connection's profile to a new
// world/session automatically. The host may have progressed since the capture.
internal static class PlayerProfileRecovery
{
    internal static string Save(string backupRoot, ulong lobby, PlayerProfileCheckpoint checkpoint)
    {
        if (checkpoint.OwnerUid <= 0 || checkpoint.Session == Guid.Empty || checkpoint.Revision <= 0)
            throw new InvalidOperationException("Invalid personal profile checkpoint.");
        var profile = JObject.Parse(checkpoint.Json);
        var directory = Path.Combine(backupRoot, "ElinTogether-profiles");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{lobby}-{checkpoint.OwnerUid}-{checkpoint.Session:N}-{checkpoint.Revision}-{Guid.NewGuid():N}.json");
        var document = new JObject {
            ["format"] = "ElinTogether unacknowledged personal profile", ["version"] = 1,
            ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["lobby"] = lobby.ToString(),
            ["uid"] = checkpoint.OwnerUid, ["session"] = checkpoint.Session.ToString(),
            ["revision"] = checkpoint.Revision, ["personalProfile"] = profile,
        };
        File.WriteAllText(path + ".tmp", document.ToString(Formatting.Indented));
        File.Move(path + ".tmp", path);
        return path;
    }
}
