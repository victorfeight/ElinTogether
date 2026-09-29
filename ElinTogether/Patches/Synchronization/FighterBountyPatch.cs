using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Only damage replay suppresses payouts; normal tooltip eligibility stays native.
[HarmonyPatch(typeof(GuildFighter), nameof(GuildFighter.HasBounty))]
internal static class FighterBountyReplayPatch
{
    internal static bool ReplayingClientDamage { get; set; }

    [HarmonyPrefix]
    internal static bool Before(ref bool __result)
    {
        if (!ReplayingClientDamage || NetSession.Instance.Connection is not ElinNetClient) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Card), nameof(Card.DamageHP), typeof(long), typeof(int), typeof(int),
    typeof(AttackSource), typeof(Card), typeof(bool), typeof(Thing), typeof(Chara), typeof(int))]
internal static class FighterBountyPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var say = AccessTools.Method(typeof(Msg), nameof(Msg.Say), [typeof(string), typeof(Card), typeof(string), typeof(string), typeof(string)]);
        var getPc = AccessTools.PropertyGetter(typeof(EClass), nameof(EClass.pc));
        var currency = AccessTools.Method(typeof(Card), nameof(Card.ModCurrency), [typeof(int), typeof(string)]);
        var bounty = AccessTools.Method(typeof(GuildFighter), nameof(GuildFighter.HasBounty));
        var markers = code.Select((c, i) => (c, i)).Where(x => x.c.opcode == OpCodes.Ldstr && Equals(x.c.operand, "bounty")).Select(x => x.i).ToArray();
        var anchor = markers.Length == 1 ? markers[0] : -1;
        var message = anchor < 0 ? -1 : code.FindIndex(anchor, c => c.Calls(say));
        var recipient = message < 0 ? -1 : code.FindIndex(message, c => c.Calls(getPc));
        var payment = recipient < 0 ? -1 : code.FindIndex(recipient, c => c.Calls(currency));
        if (anchor < 0 || message < 0 || message - anchor > 20 || recipient != message + 2 ||
            payment < 0 || payment - recipient > 5 || !code.Take(anchor).Any(c => c.Calls(bounty))) {
            EmpLog.Warning("Fighter bounty IL did not match; retaining vanilla payout rather than changing unrelated currency calls");
            return code;
        }
        var result = new List<CodeInstruction>();
        for (var i = 0; i < code.Count; i++) {
            if (i == message) {
                result.Add(new CodeInstruction(OpCodes.Ldarg_S, (byte)5).MoveLabelsFrom(code[i]).MoveBlocksFrom(code[i]));
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FighterBountyPatch), nameof(Say))));
            } else if (i == recipient) {
                result.Add(new CodeInstruction(OpCodes.Ldarg_S, (byte)5).MoveLabelsFrom(code[i]).MoveBlocksFrom(code[i]));
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FighterBountyPatch), nameof(Recipient))));
            } else result.Add(code[i]);
        }
        return result;
    }

    internal static Chara Recipient(Card? origin) => ActorOwnership.PlayerFor(origin) ?? EClass.pc;

    internal static string Say(string id, Card victim, string? amount, string? ref2, string? ref3, Card? origin)
    {
        var previous = PersonalMsgSayPatch.BountyReceiver;
        try {
            PersonalMsgSayPatch.BountyReceiver = Recipient(origin);
            return Msg.Say(id, victim, amount, ref2, ref3);
        } finally { PersonalMsgSayPatch.BountyReceiver = previous; }
    }
}
