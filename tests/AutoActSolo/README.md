# Auto Act multiplayer integration

Sources: AutoActMod upstream 66ae287, AutoActAllyExpansion upstream 61fb1f9,
installed DLL decompilation, and Elin stable 23.338 patch 2. The runtime remains
optional and uses the installed Auto Act implementation for custom terrain steps.

## Phases implemented

1. Shared progress-task serialization and local controller lifecycle. Each
   fully initialized child is queued before progress begins, including reused
   instances with new targets. Completion applies results before normal scheduler
   continuation. Cancellation stops the controller without Auto Act retries.
2. All remaining progress controllers use this same mechanism. Pouring preserves
   the custom subtype and targetCount; training uses DelegateProgress so host AI
   cannot duplicate melee/ranged/throw actions already sent by the client.
3. Custom cleaning, watering/refill and disarm use explicit host requests. Client
   navigation/delay runs locally; task continuation waits for the reply. Host
   validates sender, actor, zone, range, held-tool ownership and applicable target.
   Cleaning/watering execute the installed mod's bounded custom step on host,
   then synchronize affected cells, charges and stamina. Disarm uses vanilla
   behavior with the requesting PC scoped/restored for salvage ownership.
4. Auto-building (including sowing/fertilizing) awaits the existing build replay
   before continuing. Auto-chat keeps local dialogue but sends rumor rolls to
   host; interest/affinity are reconciled. The optional wake-up kick uses the
   existing action packet. Human/NPC companion assignment is guarded before
   Ally Expansion can select tools or change AI status.

## Action coverage

| Auto Act action | Multiplayer path |
| --- | --- |
| Harvest, collect, reap seeds, mine | TaskHarvest / TaskMine progress |
| Dig, plow | TaskDig / TaskPlow progress |
| Draw into water pot | TaskDrawWater progress |
| Pour water to selected depth | Exact SubActPourWater subtype and depth |
| Read | AI_Read progress |
| Shear, brush, slaughter | AI_Shear / AI_TendAnimal / AI_Slaughter progress |
| Steal, unlock | AI_Steal / AI_OpenLock progress |
| Clean | Custom host step + authoritative terrain/stamina |
| Water crops, refill watering can | Custom host step + terrain/charges |
| Build, plant, fertilize | Existing CharaBuildDelta + continuation barrier |
| Disarm | Vanilla host attempt/activation + acknowledgement |
| Talk | Local dialogue; host rumor rolls; affinity/interest reconciliation |
| Pick/hold | Existing Chara.Pick / held-item messages (audited, not replaced) |
| Smash | Existing melee action packet (audited, not replaced) |
| Throw milk | Existing ActThrow packet (audited, not replaced) |
| Ally Expansion music | Existing AI_PlayMusic task path |
| Ally Expansion training | Progress proxy; existing attack packets |
| Wait | Local controller only; no world mutation |

Client Auto Act controls its own human character. Only host may assign NPC
companion automation, and connected remote humans are excluded at the helper
entry points before tool/AI side effects. This does not add separate client-owned
companion parties.

## Validation

`tests/AutoActSolo` links the production bridge, progress hook, completion and
cancel deltas, and harvest/mine serializers. `tests/AutoActSteps` links the custom
request handler, coroutine/build barrier, ally guard, and remaining serializers.
Both use simulated Unity/network/mod boundaries. They do not prove live input,
animations, patch ordering or network timing. Other regression suites cover wand
replay, combat input routing and card transactions.

Both peers require this build (protocol V7). No game install is performed by the
staging build. The independent preserved wand anti-blink patch is also included.

## Live acceptance

- Start with Ally Expansion disabled: repeat each table action on 3+ targets.
  Compare host/client terrain, inventories, charges, experience and messages.
- Test both real-time and combat turn modes; encounter an enemy mid-task.
- Stop while navigating, working and awaiting a reply; immediately restart on
  a different target. No repeated effects or permanent wait should occur.
- Occupy/remove a target or transfer the tool before the host accepts a request.
  Rejection must stop automation without repeated retries or consumed charges.
- For custom water, test upgraded area tools at a map edge and automatic refill.
- For pouring, test depth > 1. For planting/fertilizing, verify the next target is
  chosen after the preceding build replay and held stack update.
- For chatting/disarming, verify host persistence and requester attribution.
  Dialogue-only discoveries such as favorite-food UI flags retain the existing
  local-dialogue behavior; this is not a general multiplayer dialogue rewrite.
- Enable Ally Expansion: host NPC companions may automate; the client's held
  tool and AI must remain untouched. Client automation must not assign host NPCs.
- Summon anti-blink acceptance remains separate: animation, one authoritative
  summon group, and summon-cap/failure feedback.

Known protocol limitation: legacy progress completion/cancellation is keyed by
actor/task type rather than a per-run ID. Custom steps have IDs and deduplication,
but rapid same-type restart races in the legacy path require live stress testing.
