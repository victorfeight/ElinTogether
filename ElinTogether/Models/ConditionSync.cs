using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace ElinTogether.Models;

// Send the host's resolved condition family, not another attempt to inflict it.
// Native save contracts include reference values, duration, flags and subclass data.
internal static class ConditionSync
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(GameIOContext.Settings);

    internal static LZ4Bytes Capture(Chara owner, int id) =>
        LZ4Bytes.Create(owner.conditions.Where(c => c.id == id).ToArray());

    internal static void Apply(Chara owner, int id, LZ4Bytes state)
    {
        var incoming = state.Decompress<Condition[]>();
        if (incoming.Any(c => c is null || c.id != id || c._ints is null || c._ints.Length < 5)) return;
        var unmatched = owner.conditions.Where(c => c.id == id).ToList();
        foreach (var next in incoming) {
            var current = unmatched.FirstOrDefault(c => c.GetType() == next.GetType() &&
                c.refVal == next.refVal && c.refVal2 == next.refVal2);
            if (current is null) continue;
            unmatched.Remove(current);
        }
        // Kill only instances absent from the authoritative family. Removing a
        // speed debuff must not also remove a strength buff sharing its type ID.
        foreach (var removed in unmatched) removed.Kill();
        var available = owner.conditions.Where(c => c.id == id).ToList();
        foreach (var next in incoming) {
            var current = available.FirstOrDefault(c => c.GetType() == next.GetType() &&
                c.refVal == next.refVal && c.refVal2 == next.refVal2);
            if (current is null) {
                owner.conditions.Add(next);
                // Same restoration entry as Chara.InitStats(onDeserialize:true).
                // Do not reroll resistance/duration or run OnStart rewards again.
                next.SetOwner(owner, onDeserialize: true);
                next.PlayEffect();
                if (next is ConTransmute transform) transform.Change();
                if (next.CancelAI) owner.ai.Cancel();
                if (next.ShouldRefresh) owner.Refresh();
            } else {
                available.Remove(current);
                if (JToken.DeepEquals(JToken.FromObject(current, Serializer), JToken.FromObject(next, Serializer))) continue;
                var contract = (JsonObjectContract)Serializer.ContractResolver.ResolveContract(current.GetType());
                // Detach derived containers before replacing serialized subclass
                // fields (e.g. disease's private stat container).
                var container = current.GetElementContainer();
                container?.SetParent();
                if (current.elements != container) current.elements?.SetParent();
                foreach (var property in contract.Properties) {
                    if (!property.Ignored && property.Readable && property.Writable)
                        property.ValueProvider!.SetValue(current, property.ValueProvider.GetValue(next));
                }
                current.SetOwner(owner, onDeserialize: true);
                if (current is ConTransmute transform) transform.Change();
                if (current.ShouldRefresh) owner.Refresh();
            }
        }
        owner.SetDirtySpeed();
        EmpLog.Debug("Condition state reconciled: chara {Uid}, condition {ConditionId}, instances {Count}", owner.uid, id, incoming.Length);
    }
}
