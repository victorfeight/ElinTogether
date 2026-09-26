using System;
using System.Collections.Generic;

namespace ElinTogether.Models;

// DynamicAIAct.Run unconditionally yields DoWait(0), consuming a separate tick.
// An already-targeted instant action must execute on its first scheduled tick.
internal sealed class QueuedCombatAct(string text, Func<bool> perform) : AIAct
{
    public override bool CancelWhenDamaged => false;
    public override string GetText(string str = "") => text;

    public override IEnumerable<Status> Run()
    {
        perform();
        yield return Success();
    }
}
