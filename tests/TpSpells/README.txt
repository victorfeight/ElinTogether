TpMagicAppendix optional multiplayer integration (protocol V33)

WHY MP OWNS THIS
The installed Tp mod prefixes parameterless Act.Perform. All 15 effect methods
covering its 17 spell IDs gate their effect on Act.CC.IsPC. Native IsPC identifies
player.chara, so the generic multiplayer replay is not enough: the client creates
local predicted effects while the host treats that same actor as a non-PC.

BOUNDARIES
TpMagicAppendixPatch runs before Tp's actual Harmony owner
(MagicIMagicAppendixndex). In multiplayer, a client queues a validated request
instead of running Tp's side effects. Native UseAbility retains cast costs,
stock and XP; the host calls Perform only, never UseAbility a second time.
The host temporarily supplies the casting human as player.chara. A finalizer
restores the original player and message/terrain scopes even on an exception.
The original Tp mod retains its formulas, eligibility checks and random rolls.
Exact spell IDs plus aliases and presence of the Tp type gate the adapter.
No optional DLL reference, third-party DLL modification, polling or save migration.
Both peers need the same MP V33 build and matching Tp spell definitions.

RESULTS
- Milk, eggs, genes, meat and gathered items: existing host CardGen/placement/
  inventory/count streams; no independently generated client items.
- Brainwash: existing CharaStateSnapshot ownership/hostility reconciliation.
- Weather: existing WeatherStateSnapshot, without rerolling weather on clients.
- Sense: existing MapRevealDelta, including cells already known to the host.
- Demolition/drilling/floor removal: existing scoped native terrain capture,
  extended for floor setters. Clients apply final tiles, never mine them again.
- Teleport: actual host-resolved positions, including the casting client's own
  character which ordinary movement reconciliation deliberately skips.
- Return: shared host-owned returnInfo and destination dialog. This does NOT
  implement independent player travel to different zones.
- Blizzard: native deaths through existing MP death results, plus weather.
The generic act replay excludes Tp, preventing a second execution on peers.

VALIDATION
TpSpells tests link the production adapter/request/result/prefix, with simulated
game/network boundaries. Mono.Cecil also checks the installed Tp DLL's effect
methods, local-player guards, and all 17 spell IDs/aliases. This is NOT a live
Unity/Harmony or two-player test. BossLootTerrain tests check actual installed
Elin terrain method shapes and production tile reconciliation. WandReplay tests
cover the existing native wand/summon path.

IN-GAME CHECKLIST (host and client on V33)
1. Client milk/ovulation on an eligible target: both see the same single product
   and its attributes; pick it up, trade it, reload. Egg fertility remains a roll.
2. Brainwash an eligible weak enemy: both see the same minion/master. Also test
   an ineligible/too-strong target; no conversion can be a correct native result.
3. Teleport self, another character, and an empty tile: both agree on positions
   and can move afterward. Test blocked destinations and repeated casts.
4. Demolition, drilling and floor removal: both see identical tiles and drops;
   do not get duplicate resources; normal mining still works afterward.
5. Gathering/CollectAll with space and while full: both agree on the resulting
   inventory/ground items. Existing vanilla overflow rules remain in force.
6. Sense and each weather spell: both see the same reveal/weather result.
7. Return: host receives the shared destination flow and both travel together.
8. Gene/meat spells and Blizzard: check native products/deaths on both peers.
9. Repeat the main casts as host, then in solo with MP disconnected.
10. Check mana, spell stock and XP once per native cast (including multicasts).

LOGS
Correlate TpSpell requested -> executed/outcomes -> received by request GUID.
Product diagnostics include UID, item ID, reference character, count and destroyed
state. A dispatched cast is not proof of a successful power/eligibility check.
Keep both Player.log and session logs for a failed case. Spell-specific visual
flourishes are not replayed by re-running Tp; verify presentation in live testing.
