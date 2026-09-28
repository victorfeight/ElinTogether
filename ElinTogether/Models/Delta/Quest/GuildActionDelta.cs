using System;
using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class GuildActionDelta : ElinDelta
{
    public enum Operation : byte { Trial, Join, MageLetter, Promote }
    [Key(0)] public Guid RequestId { get; set; } = Guid.NewGuid();
    [Key(1)] public string GuildId { get; set; } = "";
    [Key(2)] public RemoteCard? Npc { get; init; }
    [Key(3)] public Operation Action { get; init; }
    [Key(4)] public int ExpectedPhase { get; init; }
    [Key(5)] public int ExpectedRank { get; init; }
    private static readonly HashSet<Guid> Completed = [];
    internal static void Reset() => Completed.Clear();

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host || RequestId == Guid.Empty ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) ||
            actor.isDead || !actor.IsInActiveMap || Npc?.Find() is not Chara npc ||
            npc.isDead || !npc.IsInActiveMap || actor.Dist(npc) > 3 ||
            GuildStateSnapshot.Find(GuildId) is not { } guild) return;
        var suffix = guild == Guild.Fighter ? "fighter" : guild == Guild.Mage ? "mage" :
            guild == Guild.Thief ? "thief" : "merchant";
        var isDoorman = npc.id == "guild_doorman_" + suffix && npc.trait is TraitGuildDoorman;
        var isMerchantMaster = guild == Guild.Merchant && npc.id == "guild_master_merchant";
        var isClerk = npc.id == "guild_clerk_" + suffix;
        if ((Action == Operation.Promote ? !isClerk : !(isDoorman || isMerchantMaster)) ||
            !Completed.Add(RequestId)) return;

        var quest = guild.Quest;
        if ((quest?.phase ?? -1) != ExpectedPhase || guild.relation.rank != ExpectedRank) {
            Reply(host, "Shared guild progress changed. Talk to the guild representative again.");
            return;
        }
        using var simulation = Simulate();
        switch (Action) {
            case Operation.Trial when quest is null && !guild.IsMember:
                if (isMerchantMaster) game.quests.Start("guild_merchant", npc, false);
                else ((TraitGuildDoorman)npc.trait).GiveTrial();
                break;
            case Operation.Join when !guild.IsMember && quest?.phase == QuestGuild.CompletedTrial:
                Join(guild, npc, quest);
                break;
            case Operation.MageLetter when guild == Guild.Mage && !guild.IsMember && quest?.phase == QuestGuild.Started:
                if (actor.things.Find("letter_trial") is not { } letter || letter.Num <= 0) {
                    Reply(host, "You need the trial letter in your inventory.");
                    return;
                }
                letter.ModNum(-1);
                quest.NextPhase();
                // The vanilla doorman script immediately invokes guild_join after
                // accepting the letter. Keep that one host transaction here too.
                Join(guild, npc, quest);
                break;
            case Operation.Promote when guild.IsMember && guild.relation.rank < guild.relation.MaxRank &&
                                         guild.relation.exp >= guild.relation.ExpToNext:
                guild.relation.Promote();
                if (guild.IsCurrentZone) guild.RefreshDevelopment();
                break;
            default:
                Reply(host, "The guild requirements are not met on the host.");
                return;
        }
        GuildStateSnapshot.MarkDirty();
        Reply(host, "Shared guild progress updated: " + guild.Name + ".");
        EmpLog.Information("Guild action {RequestId}: peer {Peer}, guild {Guild}, action {Action}, phase {Phase}, rank {Rank}",
            RequestId, OriginPeer, GuildId, Action, guild.Quest?.phase ?? -1, guild.relation.rank);
    }

    private static void Join(Guild guild, Chara npc, Quest quest)
    {
        if (guild != Guild.Merchant) ((TraitGuildDoorman)npc.trait).OnJoinGuild();
        guild.relation.type = FactionRelation.RelationType.Member;
        quest.ChangePhase(QuestGuild.Joined);
    }

    private void Reply(ElinNetHost host, string text) => host.SendDeltaTo(OriginPeer,
        new MsgSayDelta { Text = text, R = 1, G = 1, B = 1, A = 1 });
}
