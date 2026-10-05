using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class ConstructionCommand : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public string RecipeId { get; set; } = "";
    [Key(3)] public int StockUid { get; set; }
    [Key(4)] public int[] Ingredients { get; set; } = [];
    [Key(5)] public int Skin { get; set; }
    [Key(6)] public int Direction { get; set; }
    [Key(7)] public int Altitude { get; set; }
    [Key(8)] public bool Instant { get; set; }
    [Key(9)] public bool Roof { get; set; }
    [Key(10)] public bool IgnoreStackHeight { get; set; }
    [Key(11)] public Position? Start { get; set; }
    [Key(12)] public Position End { get; set; } = new() { X = -1, Z = -1 };
    [Key(13)] public bool FreePosition { get; set; }
    [Key(14)] public float X { get; set; }
    [Key(15)] public float Y { get; set; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) ConstructionManagement.Execute(host, this); }
}

internal sealed class ConstructionManagement : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    private static readonly Dictionary<Guid, (AM_Build Mode, Recipe Recipe, int Zone)> Pending = [];
    internal static void ResetNetwork() { Completed.Clear(); Pending.Clear(); }
    [ElinPreLoad] private static void Reset(GameIOContext _) => ResetNetwork();
    internal static void Finish(Guid id)
    {
        if (!Pending.TryGetValue(id, out var pending)) return;
        Pending.Remove(id);
        if (pending.Zone != _zone.uid || !pending.Mode.IsActive || pending.Mode.recipe != pending.Recipe || !BuildMenu.Instance) return;
        pending.Mode.OnFinishProcessTiles();
        BuildMenu.Instance.info1.Refresh();
        screen.tileSelector.RefreshMouseInfo(force: true);
    }
    internal static bool TrySubmit(AM_Build mode, Point? start, Point end)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || mode.recipe == null) return false;
        // God-mode flood fill and house templates are not rectangular build intents.
        if (mode.IsFillMode() || mode.houseBoard != null) return false;
        var id = Guid.NewGuid(); Pending[id] = (mode, mode.recipe, _zone.uid);
        client.Delta.AddRemote(new ConstructionCommand { Id = id, ZoneUid = _zone.uid,
            RecipeId = mode.recipe.id, StockUid = mode.recipe.UseStock ? mode.recipe.ingredients[0].uid : 0,
            Ingredients = mode.recipe.ingredients.Select(i => i.uid).ToArray(), Skin = mode.recipe.idSkin,
            Direction = mode.recipe._dir, Altitude = mode.altitude, Instant = player.instaComplete,
            Roof = mode.IsRoofEditMode(), IgnoreStackHeight = UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl),
            Start = start, End = end, FreePosition = mode.recipe is RecipeCard { freePos: true },
            X = (mode.recipe as RecipeCard)?.fx ?? 0, Y = (mode.recipe as RecipeCard)?.fy ?? 0 });
        return true;
    }
    internal static void Execute(ElinNetHost host, ConstructionCommand command)
    {
        if (command.ZoneUid != _zone.uid || !host.AcceptsPlayerInput(command.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(command.OriginPeer, out var actor) || !actor.IsInActiveMap ||
            actor.isDead || actor.IsDisabled || command.Id == Guid.Empty || !Completed.Add((command.OriginPeer, command.Id))) return;
        var replied = false;
        void Reply() { if (!replied) host.SendDeltaTo(command.OriginPeer, new ConstructionReply { Id = command.Id }); replied = true; }
        void Reject(string why) { host.SendDeltaTo(command.OriginPeer, new MsgSayDelta { Text = why, R = 1, G = 1, B = 1, A = 1 }); Reply(); }
        var start = command.Start ?? command.End;
        if (!_zone.IsPCFactionOrTent || !start.IsInActiveMapBounds || !command.End.IsInActiveMapBounds ||
            (long)(Math.Abs(start.X - command.End.X) + 1) * (Math.Abs(start.Z - command.End.Z) + 1) > 4096 ||
            command.Direction < 0 || command.Direction > 63 || !float.IsFinite(command.X) || !float.IsFinite(command.Y) || player.Agent == null) { Reject("Invalid construction selection."); return; }
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        using var context = new ConstructionContext(actor, command);
        try {
            Thing? Accessible(int uid) {
                var t = actor.things.Find(uid) ?? _map.Stocked.Find(uid);
                return t is { isDestroyed: false, isEquipped: false } && (t.parentCard == null || t.parentCard.c_lockLv <= 0) ? t : null;
            }
            Recipe? recipe;
            if (command.StockUid != 0) {
                var stock = Accessible(command.StockUid);
                if (stock == null || !stock.trait.CanBeDropped || stock.trait.CanOnlyCarry || stock.IsInstalled) { Reject("The selected stock item is no longer available."); return; }
                recipe = Recipe.Create(stock);
                if (recipe == null || recipe.id != command.RecipeId || recipe.source.noListing) { Reject("This stock item cannot be used for construction."); return; }
            } else {
                if (!RecipeManager.dict.TryGetValue(command.RecipeId, out var source) || source.noListing ||
                    source.row.tileType.EditorTile || !(source.alwaysKnown || player.recipes.IsKnown(source.id)) || source.type == "Custom") {
                    Reject("This construction recipe is not available."); return;
                }
                recipe = Recipe.Create(source); recipe.BuildIngredientList();
                if (command.Ingredients.Length != recipe.ingredients.Count) { Reject("The recipe ingredients changed."); return; }
                for (var i = 0; i < recipe.ingredients.Count; i++) {
                    var slot = recipe.ingredients[i];
                    var ingredient = command.Ingredients[i] == 0 ? null : Accessible(command.Ingredients[i]);
                    if (ingredient == null ? !slot.optional : !slot.IsValidIngredient(ingredient)) { Reject("A selected ingredient is missing or invalid."); return; }
                    slot.SetThing(ingredient);
                }
            }
            if (command.Roof && !command.Instant && !recipe.tileType.CanInstaComplete) { Reject("Use instant building mode for roofs; queued building tasks do not retain a roof target."); return; }
            if (command.Skin < 0 || command.Skin > (recipe.renderRow.skins?.Length ?? 0)) { Reject("Invalid construction appearance."); return; }
            recipe.idSkin = command.Skin; recipe._dir = command.Direction;
            if (recipe is RecipeCard cardRecipe) { cardRecipe.freePos = command.FreePosition; cardRecipe.fx = command.X; cardRecipe.fy = command.Y; }
            var mode = context.Mode; mode.recipe = recipe;
            if (command.Altitude < (recipe.IsBridge ? -10 : 0) || command.Altitude > mode.MaxAltitude) { Reject("Invalid construction height."); return; }
            mode.CreateNewMold(); mode.SetAltitude(command.Altitude); mode.FixBridge(start, recipe);
            var selector = screen.tileSelector;
            selector.summary.SetRecipe(recipe);
            selector.ProcessTiles(start, command.End, BaseTileSelector.ProcessMode.Summary);
            if (selector.summary.countValid == 0 || !selector.summary.CanExecute()) { Reject("Construction is blocked, or the required materials, workshop or currency are missing."); return; }
            // Use the same validated native batch and payment as TryProcessTiles,
            // omitting only cursor/menu refresh and OnFinishProcessTiles UI work.
            mode.OnBeforeProcessTiles();
            if (command.Start == null) mode.OnProcessTiles(command.End, -1);
            else selector.ProcessTiles(start, command.End, BaseTileSelector.ProcessMode.Prpcess);
            selector.summary.Execute();
            DesignationManagement.Remember(command.OriginPeer, _map.tasks.undo.items.SelectMany(i => i.list).OfType<TaskDesignation>().Where(t => !t.isDestroyed).ToArray());
            EmpLog.Information("Construction applied: peer {Peer}, recipe {Recipe}, instant {Instant}, tiles {Tiles}, money {Money}",
                command.OriginPeer, recipe.id, command.Instant, selector.summary.countValid, selector.summary.money);
        } finally {
            BuildingPlanning.Dirty();
            Reply();
        }
    }
}

[MessagePackObject]
public sealed class ConstructionReply : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    protected override void OnApply(ElinNetBase net) { if (!net.IsHost) ConstructionManagement.Finish(Id); }
}

// This synchronous scope borrows the native tile processor without activating
// or replacing the host's UI. All shared selector/player fields are restored.
internal sealed class ConstructionContext : EClass, IDisposable
{
    internal static ConstructionContext? Current { get; private set; }
    internal sealed class RemoteMode : AM_Build
    {
        internal bool Roof;
        public override string id => "Build";
        public override bool IsRoofEditMode(Card? c = null) => Roof && (c == null || !c.trait.CanOnlyCarry);
        public override bool IsFillMode() => false;
    }
    internal readonly RemoteMode Mode;
    internal readonly bool IgnoreStackHeight;
    internal readonly bool Terrain;
    private readonly ConstructionContext? _previous;
    private readonly Chara _actor;
    private readonly ActionMode _mode;
    private readonly AM_Build _build;
    private readonly bool _instant, _processing, _first;
    private readonly HitSummary _summary;
    private readonly Point? _start;
    private readonly Point _temp, _agent;
    private readonly UndoManager _undo;
    private readonly Map _mapOwner;
    internal ConstructionContext(Chara actor, ConstructionCommand command, bool terrain = false)
    {
        _previous = Current; _actor = pc; _mode = scene.actionMode; _build = ActionMode.Build;
        _instant = player.instaComplete; _summary = screen.tileSelector.summary; _start = screen.tileSelector.start;
        _temp = screen.tileSelector.temp.Copy(); _agent = player.Agent.pos.Copy(); _processing = screen.tileSelector.processing; _first = screen.tileSelector.firstInMulti;
        _mapOwner = _map; _undo = _mapOwner.tasks.undo; Terrain = terrain;
        Mode = new() { Roof = command.Roof, list = _map.tasks.designations.build }; IgnoreStackHeight = command.IgnoreStackHeight;
        Current = this; player.chara = actor; player.instaComplete = command.Instant;
        scene.actionMode = ActionMode.Build = Mode;
        screen.tileSelector.summary = new(); screen.tileSelector.start = command.Start; screen.tileSelector.processing = false;
        _map.tasks.undo = new();
    }
    public void Dispose()
    {
        try {
            // A native recipe preview is not a placed product or a queued resource.
            if (Mode.recipe is RecipeCard { _mold: { } mold } recipe) { recipe._mold = null; mold.Destroy(); }
        } finally {
            _mapOwner.tasks.undo = _undo; player.Agent.pos.Set(_agent);
            screen.tileSelector.summary = _summary; screen.tileSelector.start = _start; screen.tileSelector.temp.Set(_temp);
            screen.tileSelector.processing = _processing; screen.tileSelector.firstInMulti = _first;
            scene.actionMode = _mode; ActionMode.Build = _build; player.instaComplete = _instant; player.chara = _actor; Current = _previous;
        }
    }
    internal static void RecordMaterial(Recipe recipe)
    {
        if (Current == null) BuildMenu.Instance.info1.lastMats[recipe.id] = recipe.ingredients[0].mat;
    }
    internal static bool ReadKey(UnityEngine.KeyCode key) => Current != null && key == UnityEngine.KeyCode.LeftControl
        ? Current.IgnoreStackHeight : UnityEngine.Input.GetKey(key);
}
