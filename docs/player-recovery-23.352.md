# Multiplayer death recovery and resurrection (V52)

## Native paths checked

Elin 23.352: Chara.Die, Revive, GetRevived; Zone.RemoveCard;
Scene.OnUpdate and its three death-dialog callbacks; Dialog.InputName/List;
TraitScrollStatic.OnRead -> ActEffect.ProcAt -> Proc; Act.Perform -> ProcAt.
Existing MP: AIReadArgs/CharaTaskDelta, CharaActPerformDelta, CardOnUseDelta,
CharaDieEvent, CharaReviveEvent/Delta, ZoneAddCardEvent, PauseGame and combat
participant filtering.

Native Revive drops non-container items whose own weight exceeds the player's
weight limit, when droppable and their immediate container is unlocked (or a
practice chest). It traverses inventory with onlyAccessible=false. This occurs
after choosing to revive, too late for another player to recover an item before
shared travel. It is not a general all-inventory death drop.

Resurrection's native effect randomly chooses a dead ally. Client prediction and
host replay must not independently choose different allies. Native GetRevived
also uses the singleton PC as the position anchor, which is the host rather than
the client caster during replay. Remote humans die through an NPC-shaped path:
Zone.RemoveCard clears their currentZone. The native death-choice callbacks do
not recheck whether the player has since been rescued.

## Changes

* Capture the human player's floor/position before death. After confirmed death,
  the host moves the same vanilla-eligible overweight items there, with
  ignoreAutoPick=true. Existing placement deltas synchronize the actual items.
  Native revival keeps its existing fallback check; already moved/recovered
  items are no longer in the dead character's inventory, so no double drop.
* No change to normal gear, container exclusion, lock rules, solo death,
  deployed tents, item height or rendering. This does not fix Hide Roofs hiding
  the separately diagnosed height-10 tent.
* Only the host executes EffectId.Revive. Existing scroll/spell/task routes keep
  their native consumption/casting paths. There is no new free-revive request.
* A player caster prioritizes a fallen human on the same floor (explicit target
  first, otherwise random among those humans). Retain the death floor for a
  removed remote body. Without such a target, native ally selection still runs,
  including its normal eligibility and possible off-floor allies.
* Native Revive still restores health/stamina/etc and retains existing personal
  death penalties. Rescue occurs near the caster. Native NPC GetRevived uses
  the caster's position only during the resurrection effect scope.
* Host outcomes synchronize player and active-map NPC revivals. Remote state
  application does not issue another revival request. Client crawl-home
  requests are validated against their controlled character.
* Guard the three native death-dialog callbacks against an already living PC;
  close only the tracked death dialogue after a rescue. Delayed opening and
  old crawl-home callbacks cannot act after rescue. No mandatory waiting mode:
  an unrescued player can still choose native crawl-home at any time.

The existing UI pause patch allows peers to act during ordinary host menus;
combat participant collection excludes dead characters. No new timer/polling or
save migration was added. Death-floor tracking resets on load and revival.

## Validation

Release staging build passes with zero warnings/errors. The PlayerRecovery
harness exercises production helpers/hooks with simulated boundaries and checks
native DLL metadata: 35 checks for eligibility, nesting/locks, no duplicate
drops, removed remote bodies, both rescue directions, NPC outcome publication,
duplicate results, request ownership, native scroll/spell entry points and
death-dialog/position patch shapes. This is not a live Unity/Steam MP test.

## Two-player tests

1. Carry an overweight item (including a packed tent), die with no resurrection
   available, and leave the death dialogue open. The survivor should be able to
   pick up the item before crawl-home. Crawl home and confirm it stays with the
   survivor. Verify ordinary items/containers stayed with the dead character.
2. Client uses resurrection scroll while host is dead; repeat with spell.
   Confirm one scroll use / normal casting cost and host revived near client.
3. Reverse roles. Confirm the removed client body returns and client regains
   control. Try rescuing at the last-words and crawl-home dialogue stages.
4. Confirm a rescued player's old dialogue does not move the party home.
5. With no dead human, resurrect a dead NPC ally and verify matching identity
   and position on both screens. Repeat with multiple dead allies.
6. Repeat key tests in realtime and turn-based combat. No resurrection must
   still allow normal crawl-home. Inspect `Death recovery drop` and
   `Player resurrection` entries on the host, plus revive/placement on client.

Staged at `build/player-recovery/ElinTogether.dll`. Both peers require V52.
