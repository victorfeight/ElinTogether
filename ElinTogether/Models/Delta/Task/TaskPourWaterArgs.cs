using MessagePack;
using HarmonyLib;

namespace ElinTogether.Models;

[MessagePackObject]
public class TaskPourWaterArgs : TaskArgsBase
{
    [Key(0)]
    public required Position Pos { get; init; }

    [Key(1)]
    public required RemoteCard PotOwner { get; init; }

    // Preserve Auto Act's depth-limited subtype so progress IDs and count agree.
    [Key(2)] public int? AutoActDepth { get; init; }

    public static TaskPourWaterArgs Create(TaskPourWater task)
    {
        return new() {
            Pos = task.pos,
            PotOwner = task.pot.owner,
            AutoActDepth = task.GetType().FullName == "AutoActMod.Actions.AutoActPourWater+SubActPourWater"
                ? (int?)AccessTools.Field(task.GetType(), "targetCount").GetValue(task) : null,
        };
    }

    public override AIAct CreateSubAct()
    {
        TaskPourWater task;
        if (AutoActDepth is { } depth) {
            var type = AccessTools.TypeByName("AutoActMod.Actions.AutoActPourWater+SubActPourWater");
            if (type is null) throw new System.InvalidOperationException("Auto Act pouring type missing");
            task = (TaskPourWater)System.Activator.CreateInstance(type)!;
            AccessTools.Field(type, "targetCount").SetValue(task, depth);
        } else {
            task = new TaskPourWater();
        }
        task.pos = Pos;
        task.pot = PotOwner.Find()?.trait as TraitToolWaterPot;
        return task;
    }
}