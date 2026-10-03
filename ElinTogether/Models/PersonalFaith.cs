using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using Newtonsoft.Json;

namespace ElinTogether.Models;

// Character-owned progression; altars, offerings and spawned gifts stay world
// objects. The host's live Religion instances are a view of its current player.
internal static class PersonalFaith
{
    internal const string SaveKey = "emp_personal_faith_v1";
    internal sealed class God
    {
        public int Gifts, Mood, Relation;
    }
    internal sealed class State
    {
        public int Version = 1;
        public long Revision;
        public int Days;
        // Vanilla GetRawDay is midnight expressed in minutes, not a day index.
        // Keep these timestamps unchanged for existing saves/prayer equality.
        public int PrayerDay = -1;
        public int AccountedDay;
        public Dictionary<string, God> Gods = [];
    }

    internal static State Read(Chara actor) => actor.GetStr(SaveKey) is { Length: > 0 } json
        ? JsonConvert.DeserializeObject<State>(json)! : new State {
            Days = actor.c_daysWithGod, AccountedDay = EClass.world.date.GetRawDay(),
        };
    internal static void Save(Chara actor, State state)
    {
        state.Revision++;
        actor.SetStr(SaveKey, JsonConvert.SerializeObject(state));
    }

    internal static bool Receive(Chara actor, string json)
    {
        if (string.IsNullOrEmpty(json)) return false;
        var incoming = JsonConvert.DeserializeObject<State>(json);
        if (incoming is null || incoming.Revision < Read(actor).Revision) return false;
        actor.SetStr(SaveKey, json);
        return true;
    }

    internal static void CaptureLocal()
    {
        if (NetSession.Instance.Connection is ElinNetClient || FaithTransactions.Actor is not null) return;
        var state = Read(EClass.pc);
        Capture(EClass.pc, state);
        Save(EClass.pc, state);
    }

    internal static void Capture(Chara actor, State state)
    {
        state.Days = actor.c_daysWithGod;
        state.AccountedDay = EClass.world.date.GetRawDay();
        if (EClass.player.prayed) state.PrayerDay = state.AccountedDay;
        state.Gods = EClass.game.religions.list.ToDictionary(g => g.id,
            g => new God { Gifts = g.giftRank, Mood = g.mood, Relation = g.relation });
    }

    internal static void Restore(Chara actor, State state)
    {
        actor.c_daysWithGod = state.Days;
        EClass.player.prayed = state.PrayerDay == EClass.world.date.GetRawDay();
        foreach (var god in EClass.game.religions.list) {
            var value = state.Gods.GetValueOrDefault(god.id);
            god.giftRank = value?.Gifts ?? 0;
            god.mood = value?.Mood ?? 0;
            god.relation = value?.Relation ?? god.source.relation;
        }
    }

    internal static void BeginSession(Chara actor)
    {
        var state = Read(actor);
        // Dormant characters do not earn worship days while disconnected.
        state.AccountedDay = EClass.world.date.GetRawDay();
        Save(actor, state);
    }

    internal static void AdvanceActive(Chara actor)
    {
        var state = Read(actor);
        var today = EClass.world.date.GetRawDay();
        if (today <= state.AccountedDay) return;
        state.Days += ElapsedDays(today, state.AccountedDay);
        state.AccountedDay = today;
        actor.c_daysWithGod = state.Days;
        Save(actor, state);
        Publish(actor);
    }

    internal static void AdvanceHour(Chara actor)
    {
        var state = Read(actor);
        var god = actor.faith;
        if (!state.Gods.TryGetValue(god.id, out var personal))
            state.Gods[god.id] = personal = new God { Relation = god.source.relation };
        var previous = god.mood;
        try {
            god.mood = personal.Mood;
            god.OnChangeHour();
            personal.Mood = god.mood;
        } finally { god.mood = previous; }
        Save(actor, state);
        Publish(actor);
    }

    private static void Publish(Chara actor)
    {
        if (NetSession.Instance.Connection is ElinNetHost host) host.Delta.AddRemote(new FaithResultDelta {
            Owner = actor, FaithId = actor.faith.id, PersonalState = actor.GetStr(SaveKey) ?? "",
        });
    }

    internal static void RestoreDay()
    {
        if (EClass.pc.GetStr(SaveKey) is not { Length: > 0 }) return;
        var state = Read(EClass.pc);
        var today = EClass.world.date.GetRawDay();
        // A result can precede the client's time-advance packet. Reconcile after
        // vanilla's daily callback instead of incrementing the received day twice.
        EClass.pc.c_daysWithGod = state.Days + ElapsedDays(today, state.AccountedDay);
        EClass.player.prayed = state.PrayerDay == today;
        EClass.pc.RefreshFaithElement();
    }

    internal static void RestoreJoinedPlayer()
    {
        if (EClass.pc.GetStr(SaveKey) is not { Length: > 0 }) return;
        Restore(EClass.pc, Read(EClass.pc));
        EClass.pc.RefreshFaithElement();
    }

    private static int ElapsedDays(int today, int accountedDay) =>
        Math.Max(0, (today - accountedDay) / Date.DayToken);
}
