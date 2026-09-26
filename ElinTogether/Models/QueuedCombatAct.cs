using System;
using System.Collections.Generic;

namespace ElinTogether.Models;

// DynamicAIAct.Run unconditionally yields DoWait(0), consuming a separate tick.
// An already-targeted instant action must execute on its first scheduled tick.
internal sealed class QueuedCombatAct(string text, Func<bool> perform, Func<bool>? canStart = null) : AIAct
{
    public override bool CancelWhenDamaged => false;
    public override string GetText(string str = "") => text;

    // Called before Chara.Tick: a failed preflight must not tick conditions or spend time.
    internal bool CanStart() => canStart?.Invoke() ?? true;

    public override IEnumerable<Status> Run()
    {
        yield return perform() ? Success() : Cancel();
    }
}
