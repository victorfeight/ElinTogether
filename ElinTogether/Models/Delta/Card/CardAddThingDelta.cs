using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CardAddThingDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Thing { get; init; }

    [Key(1)]
    public required RemoteCard Parent { get; init; }

    [Key(2)]
    public required bool TryStack { get; init; }

    [Key(3)]
    public required int DestInvX { get; init; }

    [Key(4)]
    public required int DestInvY { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Thing.Find() is not Thing { isDestroyed: false } thing) {
            EmpLog.Warning("Dropping {DeltaType} from peer {PeerIndex}, uid {Uid} cannot be resolved here",
                nameof(CardAddThingDelta), OriginPeer, Thing.Uid);
            return;
        }

        if (Parent.Find() is not { isDestroyed: false } parent) {
            EmpLog.Warning("Dropping {DeltaType} from peer {PeerIndex}, parent uid {Uid} cannot be resolved here",
                nameof(CardAddThingDelta), OriginPeer, Parent.Uid);
            return;
        }

        if (RejectForeignOwner(net, OriginPeer, Thing, thing)) return;

        if (net.IsHost) {
            net.Delta.AddRemote(this);
        }

        var before = QuickTransferTrace.Item(thing);
        if (thing.parent != parent) {
            var added = parent.AddThing(thing, TryStack, DestInvX, DestInvY);
            if (added == thing) {
                if (DestInvX >= 0) {
                    added.invX = DestInvX;
                }

                if (DestInvY >= 0) {
                    added.invY = DestInvY;
                    if (DestInvY == 1) {
                        WidgetCurrentTool.dirty = true;
                    }
                }
            }

            EmpLog.Debug("Add thing {Uid} into parent {ParentUid}", thing.uid, parent.uid);
        }

        EmpLog.Debug("PickupTrace transfer-result: origin {OriginPeer}, item {ItemUid}, requestedParent {ParentUid}, before {Before}, after {After}, matchesParent {MatchesParent}, consumed {Consumed}",
            OriginPeer, Thing.Uid, Parent.Uid, before, QuickTransferTrace.Item(thing),
            thing.parent == parent, thing.isDestroyed);
    }

    // All pickup outcomes must honor a host-side ownership change, including
    // stacking and a late ground result from an overflowing client.
    internal static bool RejectForeignOwner(ElinNetBase net, int originPeer, RemoteCard remote, Card card)
    {
        if (net is not ElinNetHost host || originPeer == 0 || card is not Thing thing ||
            thing.GetRootCard() is not Chara { IsPlayer: true } holder ||
            holder == host.ActiveRemoteCharas.GetValueOrDefault(originPeer)) return false;

        EmpLog.Warning("Refusing inventory outcome from peer {PeerIndex}, uid {Uid} is held by player {HolderUid}",
            originPeer, thing.uid, holder.uid);
        Rebind(net, remote, thing);
        return true;
    }

    internal static void Rebind(ElinNetBase net, RemoteCard remote, Thing thing)
    {
        if (thing.parent is Card parent) {
            net.Delta.AddRemote(new CardAddThingDelta {
                Thing = remote, Parent = parent, TryStack = false,
                DestInvX = thing.invX, DestInvY = thing.invY,
            });
        } else if (thing.parent is Zone zone) {
            net.Delta.AddRemote(new ZoneAddCardDelta {
                Card = remote, ZoneUid = zone.uid, Pos = thing.pos,
            });
        }
    }

    protected override bool OnRefresh()
    {
        if (Thing.Find() is not Thing { isDestroyed: false } thing) {
            return false;
        }

        if (NetSession.Instance.IsHost) {
            Thing.Data = LZ4Bytes.Create(thing);
            Thing.Num = thing.Num;
        }

        return true;
    }
}
