using System.Linq;
using System.Collections.Generic;
using ElinTogether.Net;

namespace ElinTogether.Models;

// Presentation only: never install host quest events into the client's live
// event manager. Their lifecycle includes spawning and ending instance quests.
internal sealed class QuestEventWidgetSync : EClass
{
    private static int _zoneUid;
    private static string? _text;
    private static Dictionary<int, string>? _progress;

    internal static void Reset() { _zoneUid = 0; _text = null; _progress = null; }

    internal static string Capture() => string.Concat(_zone.events.list
        .OfType<ZoneEventQuest>().Where(e => e.quest != null).Select(e => e.TextWidgetDate));

    internal static Dictionary<int, string> CaptureProgress()
    {
        var result = new Dictionary<int, string>();
        foreach (var evt in _zone.events.list.OfType<ZoneEventQuest>()) {
            if (evt.quest is { UseInstanceZone: true } quest) result[quest.uid] = quest.GetTextProgress();
        }
        return result;
    }

    internal static void Apply(int zoneUid, string? text, Dictionary<int, string>? progress = null)
    {
        if (!NetSession.Instance.IsClient || zoneUid != _zone.uid || text == null) return;
        _zoneUid = zoneUid;
        _text = text;
        _progress = progress;
    }

    internal static string ProgressText(Quest quest) => HasHostText && _progress is not null
        ? _progress.GetValueOrDefault(quest.uid, "") : "";

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
