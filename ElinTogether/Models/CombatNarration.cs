using System;
using System.Collections.Generic;
using ElinTogether.Net;
using MessagePack;
using UnityEngine;

namespace ElinTogether.Models;

// Presentation only. Native damage, task replay and random combat outcomes remain unchanged.
internal static class CombatNarration
{
    internal sealed class Scope
    {
        internal Scope? Previous;
        internal readonly List<CombatText> Text = [];
    }

    internal static Scope? Current { get; private set; }
    internal static bool Rendering { get; private set; }

    internal static Scope? Begin()
    {
        if (Rendering || NetSession.Instance.Connection is null) return null;
        var scope = new Scope { Previous = Current };
        Current = scope;
        return scope;
    }

    internal static void End(Scope? scope)
    {
        if (scope is null) return;
        Current = scope.Previous;
        if (scope.Previous is { } outer) outer.Text.AddRange(scope.Text);
        else if (scope.Text.Count > 0 && NetSession.Instance.Connection is ElinNetHost host)
            host.Delta.AddRemote(new CombatNarrationDelta { Text = scope.Text });
    }

    internal static void Display(List<CombatText> text)
    {
        var previous = Rendering;
        Rendering = true;
        try {
            // One completed operation is a display unit, independent of packet/frame timing.
            Msg.NewLine();
            foreach (var fragment in text) fragment.Display();
            Msg.NewLine();
        } finally { Rendering = previous; }
    }
}

[MessagePackObject]
public sealed class CombatNarrationDelta : ElinDelta
{
    [Key(0)] public required List<CombatText> Text { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // Clients never author a combat outcome or relay a received narration.
        if (net.IsClient) CombatNarration.Display(Text);
    }
}

[MessagePackObject]
public sealed class CombatText
{
    public enum Format : byte { Raw, Strings, Cards, CardStrings, CardNumber, Number }

    [Key(0)] public string Template { get; set; } = "";
    [Key(1)] public Format Kind { get; init; }
    [Key(2)] public RemoteCard? First { get; init; }
    [Key(3)] public RemoteCard? Second { get; init; }
    [Key(4)] public string?[] Refs { get; set; } = [];
    [Key(5)] public int Number { get; init; }
    [Key(6)] public float R { get; set; }
    [Key(7)] public float G { get; set; }
    [Key(8)] public float B { get; set; }
    [Key(9)] public float A { get; set; }
    [Key(10)] public string Fallback { get; set; } = "";
    [Key(11)] public bool NewLine { get; init; }
    [Key(12)] public RemoteCard? Speaker { get; init; }

    internal string Resolve()
    {
        var first = First?.Find();
        var second = Second?.Find();
        // A dead/removed target can have left the cache before presentation. Never
        // regenerate gameplay or dereference a missing actor just to show text.
        if (First is not null && first is null || Second is not null && second is null) return Fallback;
        string? Ref(int i) => i < Refs.Length ? Refs[i] : null;
        switch (Kind) {
            case Format.Raw: return Template;
            case Format.Strings:
                Msg.thirdPerson1.Set(Ref(0)); Msg.thirdPerson2.Set(Ref(1));
                return GameLang.Parse(Template, Msg.IsThirdPerson(Ref(0)), Ref(0), Ref(1), Ref(2), Ref(3));
            case Format.Cards:
                Msg.thirdPerson1.Set(first); Msg.thirdPerson2.Set(second);
                return GameLang.Parse(Template, Msg.IsThirdPerson(first), Msg.GetName(first), Msg.GetName(second), Ref(0), Ref(1));
            case Format.CardStrings:
                Msg.thirdPerson1.Set(first); Msg.thirdPerson2.Set(Ref(0));
                return GameLang.Parse(Template, Msg.IsThirdPerson(first), Msg.GetName(first), Ref(0), Ref(1), Ref(2));
            case Format.CardNumber:
                Msg.thirdPerson1.Set(first); Msg.thirdPerson2.Set(Ref(0));
                return GameLang.Parse(Template, Msg.IsThirdPerson(Number), Msg.GetName(first), Number.ToString(), Ref(0));
            case Format.Number:
                return GameLang.Parse(Template, Msg.IsThirdPerson(Number), Ref(0), Ref(1));
            default: throw new ArgumentOutOfRangeException();
        }
    }

    internal void Display()
    {
        if (NewLine) { Msg.NewLine(); return; }
        // Match Card.ShouldShowMsg's audience test, but do not discard a completed
        // death message just because its speaker died before the packet arrived.
        if (Speaker?.Find() is { } speaker && !speaker.IsPC && speaker.parent != EClass.pc && !speaker.isSynced) return;
        var text = Resolve();
        if (string.IsNullOrEmpty(text)) return;
        Msg.SetColor(new Color(R, G, B, A));
        // Already selected and parsed: do not treat the result as another language key.
        Msg.SayRaw(text);
    }
}
