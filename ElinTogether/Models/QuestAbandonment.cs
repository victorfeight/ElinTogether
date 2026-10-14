using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class QuestAbandonDelta : ElinDelta
{
    [Key(0)] public int QuestUid { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) QuestAbandonment.Resolve(host, this);
    }
}

internal sealed class QuestAbandonment : EClass
{
    internal static bool CanRequest(Quest quest) => RandomQuestStateSnapshot.Includes(quest) &&
        quest.CanAbandon && !quest.UseInstanceZone;
    internal static void Submit(Quest quest)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return;
        if (!CanRequest(quest)) { EmpPop.Information("emp_ui_quest_client".lang()); return; }
        client.Delta.AddRemote(new QuestAbandonDelta { QuestUid = quest.uid, ZoneUid = _zone.uid });
        EmpLog.Information("Requesting quest abandonment {Quest}", quest.uid);
    }
    internal static void Resolve(ElinNetHost host, QuestAbandonDelta request)
    {
        if (SharedQuests.Actor(host, request.OriginPeer, request.ZoneUid) is not { } actor) return;
        // Native removal is also the idempotency boundary: a duplicate request
        // cannot charge failure penalties or destroy quest items twice.
        var quest = game.quests.list.Find(q => q.uid == request.QuestUid);
        if (quest == null) return;
        if (!CanRequest(quest) || QuestSkillOwner.Applies(quest) && QuestSkillOwner.Actor(quest) == null) {
            SharedQuests.Reject(host, request.OriginPeer, request.QuestUid, "Have the host resolve this quest.");
            return;
        }
        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var karma = PersonalKarma.For(actor);
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            Msg.Say("questAbandon", quest.GetTitle());
            quest.Fail();
            RandomQuestStateSnapshot.MarkDirty();
            EmpLog.Information("Abandoned shared quest {Quest} for peer {Peer}", quest.uid, request.OriginPeer);
        } finally { player.chara = previous; }
    }
}
