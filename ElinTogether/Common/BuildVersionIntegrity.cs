using System;

namespace ElinTogether.Common;

public class BuildVersionIntegrity : EClass
{
    public enum APIVersion
    {
        V1 = 1,
        // sleep
        V2 = 2,
        // currency
        V3 = 3,
        // refuel + toggle/charge channels
        V4 = 4,
        // branch resource channel
        V5 = 5,
        // identify + invowner effect channels
        V6 = 6,
        // Auto Act custom steps and depth-preserving task payloads
        V7 = 7,
        // Authoritative embedded weapon spell state.
        V8 = 8,
        // Atomic task tools and authoritative boss loot terrain.
        V9 = 9,
        // Host-authorized wish dialog and reply.
        V10 = 10,
        // Authoritative weather in world/time snapshots.
        V11 = 11,
        // Shared guild state and validated guild dialogue actions.
        V12 = 12,
        // Host-owned unlocked cassette collection in world snapshots.
        V13 = 13,
        // Host-validated shop investment and synchronized shop/zone results.
        V14 = 14,
        // Shared town investment transaction and authoritative town development.
        V15 = 15,
        // Host-owned shop transfers and per-player trade settlement.
        V16 = 16,
        // Personal fighter bounties with host-only eligibility during damage replay.
        V17 = 17,
        // Host-saved personal karma and actor-specific criminal checks.
        V18 = 18,
        // Shared discovered traps in world snapshots and client discovery reports.
        V19 = 19,
        // Resolved condition families including native serialized subclass state.
        V20 = 20,
        // Personal player profiles with acknowledged, session-bound checkpoints.
        V21 = 21,
        // Connected spectator/AI control handoff and acknowledged character resync.
        V22 = 22,
        // Bundled build results alongside AutoAct completion identifiers.
        V23 = 23,
        // Failed progress results must never replay native completion.
        V24 = 24,
        // Pickup attempts are replaced by inventory outcomes and product handoffs.
        V25 = 25,
        // Hunger and host-controlled companion vitals in character snapshots.
        V26 = 26,
        // Host-resolved gifts/conversations and synchronized important-item flags.
        V27 = 27,
        // Host-authored live land expansion bounds.
        V28 = 28,
        // Authoritative equipment enhancement changes and transfer reconciliation.
        V29 = 29,
        // Zone-bound item outcomes and inactive-map prediction correction.
        V30 = 30,
        V31 = 31,
        // Container settings identify their owning item, never a shared UI prefab.
        V32 = 32,
        // Host-executed TpMagicAppendix requests/results and floor outcomes.
        V33 = 33,
        // Item explosion presentation independent of consumed-card replay.
        V34 = 34,
        // Shared resident-party commands and membership results.
        V35 = 35,
        V36 = 36, // Host-authoritative hearthstone reserves.
        V37 = 37, // Client send-to-reserve and livestock timer result.
        V38 = 38, // Shared roster board commands and results.
        V39 = 39, // Shared resident roles and maid assignment.
        V40 = 40, // Authoritative chest unlock and practice relock state.
        V41 = 41, // Vanilla throw awards through owner progression; fractional raw XP.
        V42 = 42, // Shared settlement board transactions and state.
        V43 = 43, // Host-validated quest acceptance and delivery intents.
        V44 = 44, // Authoritative quest event widget text, including timer mods.
        V45 = 45, // Validated resident name and appearance edits.
        V46 = 46, // Read-only planning state and host-owned area transactions.
        V47 = 47, // Native construction transactions and final placement results.
    }

    public const APIVersion APIVersionLatest = APIVersion.V47;

    public static string GameVersion => $"{core.version.major}.{core.version.minor}.{core.version.batch}.{core.version.fix}";

    // HSteamConnection.m_UserData
    public static long VersionStringToLong()
    {
        return VersionStringToLong(ModInfo.BuildVersion, GameVersion);
    }

    public static long VersionStringToLong(string mod, string version)
    {
        var raw = $"{APIVersionLatest}|{mod}|{version}";
        var folded = BitConverter.ToInt64([..raw.GetSha256Hash()], 0);
        return ((long)APIVersionLatest << 56) | (folded & 0x00FFFFFFFFFFFFFFL);
    }

    public static bool Ok(string? mod, string? version, APIVersion api = APIVersionLatest)
    {
        return api == APIVersionLatest &&
               string.Equals(mod, ModInfo.BuildVersion, StringComparison.Ordinal) &&
               string.Equals(version, GameVersion, StringComparison.Ordinal);
    }

    public static string GtfoReason()
    {
        return $"emp_version_mismatch|{ModInfo.BuildVersion}|{GameVersion}";
    }

    public static bool GetGtfoReason(string? reason, out string mod, out string version)
    {
        mod = version = "";
        if (reason is null || !reason.StartsWith("emp_version_mismatch|", StringComparison.Ordinal)) {
            return false;
        }

        var parts = reason.Split('|');
        if (parts.Length != 3) {
            return false;
        }

        mod = parts[1];
        version = parts[2];
        return true;
    }
}
