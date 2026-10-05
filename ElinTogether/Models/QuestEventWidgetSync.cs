using System.Linq;
using ElinTogether.Net;

namespace ElinTogether.Models;

// Presentation only: never install host quest events into the client's live
// event manager. Their lifecycle includes spawning and ending instance quests.
internal sealed class QuestEventWidgetSync : EClass
{
    private static int _zoneUid;
    private static string? _text;

    internal static void Reset() { _zoneUid = 0; _text = null; }

    internal static string Capture() => string.Concat(_zone.events.list
        .OfType<ZoneEventQuest>().Where(e => e.quest != null).Select(e => e.TextWidgetDate));

    internal static void Apply(int zoneUid, string? text)
    {
        if (!NetSession.Instance.IsClient || zoneUid != _zone.uid || text == null) return;
        _zoneUid = zoneUid;
        _text = text;
    }

    private static bool HasHostText => NetSession.Instance.IsClient && _zoneUid == _zone.uid && _text != null;

    internal static string ZoneText(Zone zone) => zone.TextWidgetDate +
        (HasHostText && zone.uid == _zoneUid ? _text : "");

    internal static string EventText(ZoneEvent zoneEvent)
    {
        if (zoneEvent is ZoneEventQuest questEvent &&
            (HasHostText || questEvent.quest == null)) return "";
        return zoneEvent.TextWidgetDate;
    }
}
