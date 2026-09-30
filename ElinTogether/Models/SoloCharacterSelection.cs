using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ElinTogether.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElinTogether.Models;

internal sealed class SoloCharacterSelection : EClass
{
    internal const string OriginalHostKey = "emp_solo_original_host";
    private sealed record Selection(string World, bool Cloud, int From, int To);
    private static Selection? _pending;
    internal static string BackupRoot => Path.Combine(CorePath.PathBackup, "ElinTogether-characters");
    internal static bool IsSoloCharacter => pc is not null && pc.GetInt(OriginalHostKey) is var uid && uid > 0 && uid != pc.uid;

    internal static IEnumerable<Chara> Candidates() => game.cards.globalCharas.Values
        .Where(c => c == pc || c.GetBool("remote_chara") || c.GetInt(OriginalHostKey) > 0)
        .OrderBy(c => c.uid);

    internal static string? CannotSelect(Chara target)
    {
        if (_pending is not null) return "A character switch is already loading.";
        if (!core.IsGameStarted || game.isLoading) return "Load the host's save first.";
        if (NetSession.Instance.Connection is not null) return "Disconnect multiplayer before switching characters.";
        if (target == pc || !Candidates().Contains(target)) return "Choose another saved player character.";
        if (pc.isDead || target.isDead) return "Both characters must be alive before switching.";
        if (pc.host is not null || pc.ride is not null || pc.parasite is not null ||
            target.host is not null || target.ride is not null || target.parasite is not null)
            return "Dismount both characters before switching.";
        if (pc.party is null || pc.currentZone is null || pc.homeZone is null || target.faction != pc.faction)
            return "These characters do not have a compatible world and party.";
        if (OwnerProgressionAwards.Read(target).Count != 0)
            return "This character has pending multiplayer rewards. Rejoin multiplayer to finish synchronizing them first.";
        return null;
    }

    internal static void CaptureCurrent()
    {
        if (NetSession.Instance.IsClient || FaithTransactions.Actor is not null || pc is null) return;
        SoloPlayerProfile.Capture(pc, player);
        PersonalFaith.CaptureLocal();
        PersonalKarma.Initialize(pc, player.karma);
        pc.SetInt(PersonalKarma.ValueKey, player.karma);
    }

    internal static string Export(params Chara[] characters)
    {
        if (!core.IsGameStarted || NetSession.Instance.Connection is not null)
            throw new InvalidOperationException("Load the save offline before exporting characters.");
        CaptureCurrent();
        var folder = Path.Combine(BackupRoot, $"{Game.id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        foreach (var actor in characters.Distinct()) {
            var document = new JObject {
                ["format"] = "ElinTogether character backup", ["version"] = 1,
                ["world"] = Game.id, ["createdUtc"] = DateTime.UtcNow.ToString("O"),
                ["uid"] = actor.uid, ["name"] = actor.NameSimple,
                ["character"] = JToken.Parse(LZ4Bytes.Create(actor).DecompressToString()),
                ["personalProfile"] = Parse(actor.GetStr(SoloPlayerProfile.SaveKey)),
                ["faith"] = Parse(actor.GetStr(PersonalFaith.SaveKey)),
            };
            var path = Path.Combine(folder, $"character-{actor.uid}.json");
            File.WriteAllText(path + ".tmp", document.ToString(Formatting.Indented));
            File.Move(path + ".tmp", path);
        }
        EmpLog.Information("Character JSON backup saved to {Folder}", folder);
        return folder;

        static JToken Parse(string? json) => string.IsNullOrEmpty(json) ? JValue.CreateNull() : JToken.Parse(json);
    }

    internal static void Select(Chara target)
    {
        var reason = CannotSelect(target);
        if (reason is not null) throw new InvalidOperationException(reason);
        _ = SoloPlayerProfile.Prepare(target);
        var selection = new Selection(Game.id, game.isCloud, pc.uid, target.uid);
        if (!game.Save(silent: true)) throw new IOException("The world could not be saved. No character was switched.");
        Export(pc, target);
        var index = new GameIndex().Create(game);
        index.id = Game.id;
        index.cloud = game.isCloud;
        GameIO.MakeBackup(index);
        _pending = selection;
        try {
            EmpLog.Information("Solo character selection requested: {PreviousUid} -> {TargetUid}", selection.From, selection.To);
            Game.Load(selection.World, selection.Cloud);
            if (_pending is not null || player.uidChara != selection.To || pc.uid != selection.To)
                throw new InvalidOperationException("The selected character was not activated. Restoring the original saved world.");
        } catch {
            _pending = null;
            // The saved world still contains the original active character.
            // Reload it through vanilla instead of leaving a half-loaded game.
            Game.Load(selection.World, selection.Cloud);
            throw;
        } finally { _pending = null; }
    }

    // Runs before vanilla OnGameInstantiated binds Player.chara and builds UI.
    // No UID reassignment, inventory cloning, new-character setup or map editing.
    internal static void ApplyPending()
    {
        var selection = _pending;
        if (selection is null) return;
        _pending = null;
        if (Game.id != selection.World || game.isCloud != selection.Cloud || player.uidChara != selection.From)
            throw new InvalidOperationException("The loaded save changed before character selection.");
        var previous = game.cards.globalCharas.Find(selection.From)
            ?? throw new InvalidOperationException("The original character is missing from this save.");
        var target = game.cards.globalCharas.Find(selection.To)
            ?? throw new InvalidOperationException("The selected character is missing from this save.");
        _ = SoloPlayerProfile.Prepare(target);
        var party = previous.party ?? throw new InvalidOperationException("The saved party is missing.");
        var origin = previous.GetInt(OriginalHostKey, previous.uid);
        previous.SetInt(OriginalHostKey, origin);
        target.SetInt(OriginalHostKey, origin);
        player.chara = previous;
        // This is save hydration, before world._OnLoad assigns branch owners.
        // Live AddMemeber/RemoveMember refresh branch policies, work and UI and
        // cannot run here. Update persisted membership and invalidate its cache;
        // vanilla world/player loading then initializes the resulting party.
        if (target.party is { } oldParty && oldParty != party) {
            oldParty.uidMembers.RemoveAll(uid => uid == target.uid);
            oldParty._members = null;
        }
        party.uidMembers.RemoveAll(uid => uid == previous.uid || uid == target.uid);
        party.uidMembers.Add(target.uid);
        party._members = null;
        target.party = party;
        target.isSale = false;
        target.SetBool(18, false); // Same membership flag cleared by vanilla AddMemeber.
        party.SetLeader(target);
        previous.party = null;
        previous.c_wasInPcParty = false;
        target.homeZone ??= previous.homeZone;
        target.currentZone = previous.currentZone;
        target.pos.Set(previous.pos);
        target.global.transition = new ZoneTransition {
            state = ZoneTransition.EnterState.Exact, x = previous.pos.x, z = previous.pos.z,
        };
        previous.SetBool(CINT.IsPC, false);
        previous.SetBool("remote_chara", true);
        target.SetBool(CINT.IsPC, true);
        target.SetBool("remote_chara", false);
        player.uidChara = target.uid;
        player.chara = target;
        player.zone = target.currentZone;
        SoloPlayerProfile.RestoreFor(target, player);
        PersonalKarma.Initialize(target, 30);
        PersonalKarma.Join(target);
        // Faith is restored after ReligionManager.OnLoad initializes its lookup.
        var faith = PersonalFaith.Read(target);
        faith.AccountedDay = world.date.GetRawDay();
        PersonalFaith.Save(target, faith);
        EmpLog.Information("Solo character selected: {PreviousUid} -> {TargetUid}", previous.uid, target.uid);
    }
}
