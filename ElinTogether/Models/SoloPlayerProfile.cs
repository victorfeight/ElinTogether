using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElinTogether.Models;

// Shared by MP checkpoints, MP joins and offline character selection.
// Only local-player data belongs here. Never serialize/replace Player wholesale:
// its zone, agent, quests, recipes, collections and finances belong to this world.
internal static class SoloPlayerProfile
{
    internal const string SaveKey = "emp_solo_player_v1";
    internal const int MaxJsonLength = 1024 * 1024;
    internal sealed class State
    {
        public int Version = 1;
        // Sticky provenance: a later checkpoint cannot reconstruct pre-upgrade history.
        public bool LegacyHistoryMissing;
        public Dictionary<string, string> Fields = [];
        public Dictionary<int, Window.SaveData>? CarriedWindows;
    }

    private static readonly string[] Names = [
        nameof(Player.totalFeat), nameof(Player.expKnowledge), nameof(Player.knowledges),
        nameof(Player.stats), nameof(Player.nums), nameof(Player.pref),
        nameof(Player.hotbars), nameof(Player.hotbarPage), nameof(Player.favAbility),
        nameof(Player.layerAbilityConfig), nameof(Player.priorityActions), nameof(Player.lastRecipes),
        nameof(Player.knownCraft), nameof(Player.knownSongs), nameof(Player.sketches),
        nameof(Player.title), nameof(Player.memo), nameof(Player.memo2),
        nameof(Player.trackedCategories), nameof(Player.trackedCards), nameof(Player.trackedElements),
        nameof(Player.partySetups), nameof(Player.returnInfo), nameof(Player.safeTravel),
        nameof(Player.fished), nameof(Player.fishArtifact), nameof(Player.dailyGacha),
        nameof(Player.wellWished), nameof(Player.staminaRecovery),
    ];
    private static readonly FieldInfo[] Fields = Names.Select(name => typeof(Player).GetField(name)
        ?? throw new MissingFieldException(typeof(Player).FullName, name)).ToArray();

    internal static void Capture(Chara actor, Player player)
    {
        if (actor != player.chara) throw new InvalidOperationException("Only the active player's profile can be captured.");
        var state = new State { LegacyHistoryMissing = actor.GetStr(SaveKey) is { Length: > 0 } && HasMissingHistory(actor) };
        foreach (var field in Fields) state.Fields[field.Name] =
            JsonConvert.SerializeObject(field.Name == nameof(Player.returnInfo) && Net.NetSession.Instance.Connection != null
                ? null : field.GetValue(player), field.FieldType, GameIOContext.Settings);
        state.CarriedWindows = PersonalContainerWindows.Capture(actor);
        var json = JsonConvert.SerializeObject(state);
        if (json.Length > MaxJsonLength) throw new InvalidOperationException("Player profile exceeds the supported size.");
        actor.SetStr(SaveKey, json);
        PersonalStaminaRecovery.Store(actor, player.staminaRecovery);
    }

    internal static bool HasMissingHistory(Chara actor)
    {
        if (actor.GetStr(SaveKey) is not { Length: > 0 } json) return true;
        if (Read(json).LegacyHistoryMissing) return true;
        // The first solo-selector build lacked provenance. Its guest defaults
        // must not be mistaken for recovered MP history after this upgrade.
        var origin = actor.GetInt(SoloCharacterSelection.OriginalHostKey);
        return JObject.Parse(json)[nameof(State.LegacyHistoryMissing)] is null &&
            (origin > 0 ? origin != actor.uid : actor.GetBool("remote_chara"));
    }

    private static State Read(string json)
    {
        if (json.Length > MaxJsonLength) throw new InvalidOperationException("Player profile exceeds the supported size.");
        var state = JsonConvert.DeserializeObject<State>(json)
            ?? throw new InvalidOperationException("The saved player profile is empty.");
        if (state.Version != 1 || state.Fields is null)
            throw new InvalidOperationException("Unsupported saved player profile version.");
        if (state.CarriedWindows is not null && state.CarriedWindows.Values.Any(data =>
                data is null || data.ints is null || data.ints.Length < 20 || data.cats is null))
            throw new InvalidOperationException("Invalid carried container layout.");
        return state;
    }

    internal static void Initialize(Chara actor, bool legacy)
    {
        var defaults = new Player { chara = actor };
        defaults.hotbars.OnCreateGame();
        // Seed metadata first so Capture preserves the correct provenance.
        actor.SetStr(SaveKey, JsonConvert.SerializeObject(new State { LegacyHistoryMissing = legacy }));
        Capture(actor, defaults);
    }

    internal static void RestoreFor(Chara actor, Player player)
    {
        if (actor != player.chara) throw new InvalidOperationException("Profile owner is not the active player.");
        if (actor.GetStr(SaveKey) is not { Length: > 0 }) Initialize(actor, legacy: true);
        Restore(player, Prepare(actor));
        PersonalContainerWindows.Restore(actor, Read(actor.GetStr(SaveKey)!).CarriedWindows);
    }

    internal static void Accept(Chara actor, string json)
    {
        // Validate every field before touching the saved record; never apply a
        // remote Player profile to the host's live Player singleton.
        var prepared = PrepareJson(json);
        var state = Read(json);
        state.LegacyHistoryMissing |= HasMissingHistory(actor);
        actor.SetStr(SaveKey, JsonConvert.SerializeObject(state));
        PersonalStaminaRecovery.Store(actor, (int)prepared[RecoveryField]!);
    }

    // Deserialize every field before changing the active player. A broken or
    // unsupported profile must fail without leaving half of a switch applied.
    internal static Dictionary<FieldInfo, object?> Prepare(Chara actor)
    {
        if (actor.GetStr(SaveKey) is not { Length: > 0 } json) return [];
        var prepared = PrepareJson(json);
        // Native AI may have spent this credit since the last human checkpoint.
        prepared[RecoveryField] = PersonalStaminaRecovery.Saved(actor, (int)prepared[RecoveryField]!);
        return prepared;
    }

    private static FieldInfo RecoveryField => Fields.Single(f => f.Name == nameof(Player.staminaRecovery));

    private static Dictionary<FieldInfo, object?> PrepareJson(string json)
    {
        var state = Read(json);
        // Pre-23.352 profiles lack only this new scalar. Keep rejecting missing
        // historical fields and unknown world fields; never copy host recovery.
        if (!state.Fields.ContainsKey(nameof(Player.staminaRecovery)))
            state.Fields.Add(nameof(Player.staminaRecovery), "0");
        if (state.Fields.Count != Fields.Length || state.Fields.Keys.Any(name => !Names.Contains(name)))
            throw new InvalidOperationException("Unexpected player profile fields.");
        var result = new Dictionary<FieldInfo, object?>();
        var defaults = new Player();
        foreach (var field in Fields) {
            if (!state.Fields.TryGetValue(field.Name, out var value))
                throw new InvalidOperationException($"The saved profile is missing {field.Name}.");
            ValidateTypes(value);
            var parsed = JsonConvert.DeserializeObject(value, field.FieldType, GameIOContext.Settings);
            if (field == RecoveryField && (parsed is not int recovery || recovery < 0))
                throw new InvalidOperationException("Invalid stamina recovery credit.");
            if (parsed is null && field.GetValue(defaults) is not null)
                throw new InvalidOperationException($"The saved profile has an empty {field.Name}.");
            result.Add(field, parsed);
        }
        return result;
    }

    private static void ValidateTypes(string json)
    {
        // Native hotbars and numeric logs are polymorphic. Permit their game
        // types without permitting a peer to instantiate arbitrary CLR types.
        if (JToken.Parse(json) is not JContainer root) return;
        foreach (var property in root.Descendants().OfType<JProperty>().Where(p => p.Name == "$type")) {
            var parts = ((string?)property.Value ?? "").Split(',');
            var assembly = typeof(Player).Assembly;
            var type = assembly.GetType(parts[0].Trim(), throwOnError: false);
            if (parts.Length < 2 || parts[1].Trim() != assembly.GetName().Name || type is null ||
                !(typeof(HotItem).IsAssignableFrom(type) || typeof(NumLog).IsAssignableFrom(type) ||
                  Fields.Any(f => f.FieldType == type)))
                throw new InvalidOperationException("Unsupported type in personal player profile.");
        }
    }

    internal static void Restore(Player player, Dictionary<FieldInfo, object?> prepared)
    {
        if (prepared.Count == 0) {
            // Legacy MP saves contain the character but never saved this Player
            // profile. Start missing counters fresh; don't impersonate the host.
            var defaults = new Player();
            foreach (var field in Fields) field.SetValue(player, field.GetValue(defaults));
            player.hotbars.OnCreateGame();
        } else {
            foreach (var pair in prepared) pair.Key.SetValue(player,
                pair.Key.Name == nameof(Player.returnInfo) && Net.NetSession.Instance.Connection != null ? null : pair.Value);
        }
        player.RefreshDomain();
        player.currentHotItem = new HotItemNoItem();
        player.eqBait = null;
        player.target = null;
    }
}
