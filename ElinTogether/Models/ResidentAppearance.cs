using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[Flags]
public enum CosmeticFields { None = 0, Name = 1, Portrait = 2, Skin = 4, Hair = 8, Parts = 16, All = 31 }

[MessagePackObject]
public sealed class ResidentCosmetics
{
    [Key(0)] public string? Name { get; set; }
    [Key(1)] public string? Portrait { get; set; }
    [Key(2)] public string? Skin { get; set; }
    [Key(3)] public int Hair { get; set; }
    // PCCData's native saved representation is only this map. Each entry is
    // [body set, part, color]; renderer caches and temporary editor state aren't saved.
    [Key(4)] public Dictionary<string, string[]>? Parts { get; set; }

    private static Dictionary<string, string[]>? Copy(Dictionary<string, string[]>? parts) =>
        parts?.ToDictionary(p => p.Key, p => (string[])p.Value.Clone());

    internal static ResidentCosmetics Capture(Chara c) => new() {
        Name = c.c_altName, Portrait = c.c_idPortrait, Skin = c.c_idSpriteReplacer,
        Hair = c.GetInt(105), Parts = Copy(c.pccData?.map),
    };

    internal CosmeticFields Difference(ResidentCosmetics other)
    {
        var fields = CosmeticFields.None;
        if (Name != other.Name) fields |= CosmeticFields.Name;
        if (Portrait != other.Portrait) fields |= CosmeticFields.Portrait;
        if (Skin != other.Skin) fields |= CosmeticFields.Skin;
        if (Hair != other.Hair) fields |= CosmeticFields.Hair;
        if (Parts == null ? other.Parts != null : other.Parts == null || Parts.Count != other.Parts.Count ||
            Parts.Any(p => !other.Parts.TryGetValue(p.Key, out var values) || !p.Value.SequenceEqual(values))) fields |= CosmeticFields.Parts;
        return fields;
    }

    internal bool Valid => Parts == null || Parts.ContainsKey("body") && Parts.Values.All(v => v is { Length: 3 });

    internal void Write(Chara c, CosmeticFields fields)
    {
        fields &= Capture(c).Difference(this);
        if (fields == CosmeticFields.None) return;
        if (fields.HasFlag(CosmeticFields.Name)) c.c_altName = Name;
        if (fields.HasFlag(CosmeticFields.Portrait)) c.c_idPortrait = Portrait;
        if (fields.HasFlag(CosmeticFields.Skin)) c.c_idSpriteReplacer = Skin;
        if (fields.HasFlag(CosmeticFields.Hair)) c.SetInt(105, Hair);
        if (fields.HasFlag(CosmeticFields.Parts)) c.pccData = Parts == null ? null : new PCCData { map = Copy(Parts)! };
        if ((fields & (CosmeticFields.Parts | CosmeticFields.Skin)) != 0) {
            // The same screen-list handoff used by vanilla's toggle-PCC callback.
            var visible = c.isSynced;
            if (visible) c.renderer.OnLeaveScreen();
            EClass.scene.syncList.Remove(c.renderer);
            c._CreateRenderer();
            if (visible) {
                EClass.scene.syncList.Add(c.renderer);
                c.renderer.OnEnterScreen();
            }
        }
        WidgetRoster.SetDirty();
        ReserveManagement.RefreshList();
    }
}

[MessagePackObject]
public sealed class ResidentCosmeticRequest : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int MemberUid { get; init; }
    [Key(2)] public required ResidentCosmetics Before { get; init; }
    [Key(3)] public required ResidentCosmetics After { get; init; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) ResidentAppearance.Execute(host, this); }
}

[MessagePackObject]
public sealed class ResidentCosmeticState : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int MemberUid { get; init; }
    [Key(2)] public required ResidentCosmetics Data { get; init; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || ZoneUid != _zone.uid) return;
        var c = _map.FindChara(MemberUid);
        if (c == null) { net.Delta.DeferLocal(this); return; }
        if (PartyMembership.Protected(c)) return;
        Data.Write(c, CosmeticFields.All);
    }
}

internal sealed class ResidentAppearance : EClass
{
    internal sealed record Edit(ElinNetBase Connection, int ZoneUid, Chara Target, ResidentCosmetics Before);
    internal static bool Eligible(Chara c) => c.IsAliveInCurrentZone && c.IsHomeMember() && !c.IsGuest() && !PartyMembership.Protected(c);

    internal static Edit? Begin(Chara c) => NetSession.Instance.Connection is { } connection &&
        !ElinDelta.IsApplying && Eligible(c) ? new(connection, _zone.uid, c, ResidentCosmetics.Capture(c)) : null;

    internal static void Finish(Edit? edit)
    {
        if (edit == null || NetSession.Instance.Connection != edit.Connection || !core.IsGameStarted ||
            game.isLoading || _zone.uid != edit.ZoneUid || !Eligible(edit.Target)) return;
        var after = ResidentCosmetics.Capture(edit.Target);
        if (edit.Before.Difference(after) == CosmeticFields.None) return;
        if (edit.Connection is ElinNetClient client) client.Delta.AddRemote(new ResidentCosmeticRequest {
            ZoneUid = edit.ZoneUid, MemberUid = edit.Target.uid, Before = edit.Before, After = after,
        });
        else if (edit.Connection is ElinNetHost host) Publish(host, edit.Target);
    }

    private static void Publish(ElinNetHost host, Chara c) => host.Delta.AddRemote(new ResidentCosmeticState {
        ZoneUid = _zone.uid, MemberUid = c.uid, Data = ResidentCosmetics.Capture(c),
    });

    internal static void Execute(ElinNetHost host, ResidentCosmeticRequest request)
    {
        if (request.ZoneUid != _zone.uid || !host.AcceptsPlayerInput(request.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(request.OriginPeer, out var actor) ||
            !actor.IsInActiveMap || actor.isDead || actor.IsDisabled) return;
        var c = _map.FindChara(request.MemberUid);
        if (c == null || PartyMembership.Protected(c)) return;
        void Reject(string reason) {
            EmpLog.Warning("Resident cosmetic edit rejected: peer {Peer}, member {Member}, reason {Reason}", request.OriginPeer, c.uid, reason);
            host.SendDeltaTo(request.OriginPeer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
        }
        try {
            if (!Eligible(c) || !request.Before.Valid || !request.After.Valid) { Reject("This resident can no longer be edited here."); return; }
            var fields = request.Before.Difference(request.After);
            if ((fields & ~CosmeticFields.Name) != 0 && !c.sourceCard.idActor.IsEmpty()) {
                Reject("This resident uses a fixed appearance."); return;
            }
            var current = ResidentCosmetics.Capture(c);
            // Different fields can be edited independently; a stale edit of the
            // same field must not overwrite another player's newer choice.
            if ((current.Difference(request.Before) & fields) != 0 && (current.Difference(request.After) & fields) != 0) {
                Reject("This resident changed while you were editing. Reopen the editor to try again."); return;
            }
            request.After.Write(c, fields);
            EmpLog.Information("Resident cosmetic edit applied: peer {Peer}, member {Member}, fields {Fields}", request.OriginPeer, c.uid, fields);
        } finally {
            Publish(host, c); // Also repairs a rejected client's local preview.
        }
    }
}
