using System;
using System.Linq;

namespace ElinTogether.Models;

// Native designation processing owns hit tests, forced-instant rules, payment,
// resource drops and queued work. The client sends selections, never results.
internal sealed class TerrainManagement : EClass
{
    private sealed class RemoteMine : AM_Mine
    {
        internal bool Roof;
        public override bool IsRoofEditMode(Card? c = null) => Roof;
    }
    internal static void Execute(Chara actor, DesignationCommand command, Action<string> reject)
    {
        // Vanilla TaskMine does not persist a roof target: a later worker reads
        // the current edit mode. Do not queue a roof job that could mine a wall.
        if (command.Roof && !command.Instant) { reject("Use instant building mode to remove roofs; queued roof targets are not retained by the game."); return; }
        // Harvest completion lives in Progress_Custom's callback, not in the
        // Task.OnProgressComplete method used by native instant building.
        if (command.Instant && command.Operation == DesignationOperation.Harvest) { reject("Use queued work mode for harvesting; the game's instant building path does not complete harvest tasks."); return; }
        if (player.Agent == null) { reject("The building agent is unavailable."); return; }
        using var context = new ConstructionContext(actor, new() { Instant = command.Instant, Start = command.Start }, terrain: true);
        var mine = new RemoteMine { Roof = command.Roof, mode = (TaskMine.Mode)command.Mode, ramp = command.Ramp };
        var previousMine = ActionMode.Mine;
        try {
            ActionMode.Mine = mine;
            ActionMode mode = command.Operation switch {
                DesignationOperation.Mine => mine,
                DesignationOperation.Dig => new AM_Dig { mode = (TaskDig.Mode)command.Mode, ramp = command.Ramp },
                DesignationOperation.Cut => new AM_Cut(),
                DesignationOperation.Harvest => new AM_Harvest(),
                _ => throw new InvalidOperationException("Not a gathering designation"),
            };
            scene.actionMode = mode;
            mode.OnActivate(); // Initializes native task list and mold, no UI activation.
            var selector = screen.tileSelector;
            selector.summary.SetRecipe(null);
            selector.ProcessTiles(command.Start!, command.End!, BaseTileSelector.ProcessMode.Summary);
            if (selector.summary.countValid == 0 || !selector.summary.CanExecute()) { reject("Work is blocked or the required currency is missing."); return; }
            mode.OnBeforeProcessTiles();
            selector.ProcessTiles(command.Start!, command.End!, BaseTileSelector.ProcessMode.Prpcess);
            selector.summary.Execute();
            DesignationManagement.Remember(command.OriginPeer, _map.tasks.undo.items.SelectMany(i => i.list).OfType<TaskDesignation>().Where(t => !t.isDestroyed).ToArray());
            EmpLog.Information("Terrain command: peer {Peer}, operation {Operation}, instant {Instant}, roof {Roof}, tiles {Tiles}, money {Money}",
                command.OriginPeer, command.Operation, command.Instant, command.Roof, selector.summary.countValid, selector.summary.money);
        } finally { ActionMode.Mine = previousMine; }
    }
}
