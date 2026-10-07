# Relic equipment compatibility — 23.352

## Source and root causes

Checked installed Elin.dll and the stable 23.352 decompilation, especially
CharaBody.IsEquippable/Equip/Unequip, DNA.Apply, Chara.OnSleep,
DramaOutcome.reward_sorin, TraitSorin and DramaManager.CheckIF. Also read the
installed `_Elona/Lang/_Dialog/Drama/sorin.xlsx` rather than guessing its gates.

* Native relic affordability reads `pc.feat`; replaying another player's equipment
  must use that equipment owner's budget.
* Native equipment spends/refunds feat points and applies/reverses DNA. Our point
  setter hook could send the final total before the equipment packet, causing
  replay to spend/refund again. Deferred element snapshots were another competing
  writer for those native effects.
* AI_Equip already replays as a task. Relics need a single equipment mutation
  path, even when the task first walks to and picks up the item.
* Sorin adds permanent slot 46 directly; ordinary equipment synchronization did
  not transmit this reward. His dialogue gates the reward using the shared
  Player flag `sorin_relic`, despite granting the slot to one Chara.
* Native human sleep clears item flag 135. This lock needs synchronization, and
  a saved human sleeping under host AI control does not satisfy native IsPC.

## Implementation

Native equip/unequip and DNA.Apply remain responsible for gameplay mutations.
A scoped relic operation captures the original/final feat total and lock state
in CharaEquipDelta. The existing feat and element publishers skip changes inside
that scope; normal progression and player-facing messages remain enabled.
Replay uses the original budget for native eligibility, retains the final total,
and deduplicates successfully applied operation IDs. Nested replacement refunds
are preserved in order. Ordinary gear retains its existing task/delta routing.

The remote AI_Equip task still handles movement/pickup; its relic equipment
mutation waits for the completed equipment packet. Invalid ownership, inactive
peers and unresolved targets do not apply relic outcomes. Unequip finds the
actual item if body-slot indexing has changed.

RelicStateDelta only adds the missing permanent slot and updates locks on matching
equipped item UIDs. It never replaces the body or reapplies DNA. Native Sorin
reward and sleep publish this state. Host-controlled saved humans receive the
human relic sleep unlock; spectators receive the resulting lock state.

For multiplayer and saved character profiles, Sorin's two exact flag conditions
and quest icon use the current character's relic slot. Each character can claim
the native reward once, including after the host has claimed it. Other dialogue
flags are untouched. No save migration, periodic polling or new profile fields.

Protocol V51: both peers must use this build.

## Validation

Release staging build: zero warnings/errors.

* RelicEquipment: 38 checks, including native IL shape, owner budget, duplicate
  replay, nested swaps/refunds, AI_Equip routing, sleep/spectator lock state,
  Sorin gates, ownership rejection, exception scope cleanup and ordinary gear.
* UpstreamIntegration: 54 existing checks passed.
* Construction: 73 checks passed; all 1,552 compiled game API references resolve
  against the installed Elin.dll.

These exercise production helpers with simulated game boundaries and inspect
native/compiled metadata. They are not live two-player Unity tests.

## In-game verification after installing on both peers

1. Speak to Sorin with each character, including the second after the first has
   claimed the reward. Verify each receives one relic slot and the recipe.
2. Give client and host different feat budgets. Equip a relic affordable only
   to the client. Confirm the client's points decrease by the displayed cost
   once; the host's points remain unchanged.
3. Verify native pre-sleep removal restrictions. Sleep, remove and re-equip;
   confirm one refund and matching effects/lock on both machines.
4. Sleep then swap two relics with different costs/effects. Confirm correct
   final points and no doubled or lingering effects.
5. Equip via a ground-item task and through the inventory. Check both paths.
6. Sleep in Take a Break mode, resume control and verify the relic unlock.
7. Save/rejoin and switch saved characters; verify each body's slot, equipped
   relic, points and effects persist. Smoke-test ordinary equipment as well.

Built to `build/relics/ElinTogether.dll`; not installed by this change.
