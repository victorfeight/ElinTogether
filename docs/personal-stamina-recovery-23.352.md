# Personal combat-stamina recovery (23.352)

## Native behavior and gap

Chara.UseAbility adds actual SP spent by recovery-tagged actions to
Player.staminaRecovery for IsPC. TickConditions occasionally calls
Player.RecoverStamina for the PC; NPCs instead regenerate rnd(5)+1 stamina.
Player.RecoverStamina spends the allowance and modifies the PC's stamina.
Human death clears the allowance, and human OnSleep recovers the remainder.

The old profile allowlist omitted this new Player field. Joining/resuming loads
the host's Game before restoring personal fields, and solo selection reuses
Player: both could leave another character's recovery allowance active.
Companion snapshots and ordinary stamina messages also omitted this allowance.

## Ownership and implementation

- Human play keeps native Player.staminaRecovery as its live value. Native costs,
  recovery probability, amount, ordinary food recovery and sleep remain intact.
- The existing personal-profile checkpoint carries the scalar. A character int
  (`emp_stamina_recovery`) stores the dormant/AI value without serializing a whole
  profile on each NPC regeneration event. Capture/accept write this copy; restore
  prefers it because AI can have used credit since the last human checkpoint.
- Old profiles may omit only this new field and default it to zero. Other missing,
  unknown, invalid or negative fields still fail validation. No manual save edit.
- Existing stamina messages carry the scalar as well, so the host has the latest
  received human allowance before a disconnect. The existing change detector also
  compares allowance and owner, with no new polling loop, timer or periodic save.
  Host acceptance checks sender identity and human input ownership.
- One narrowly matched native NPC TickConditions call is wrapped: get_stamina,
  rnd(5), +1, Stats.Mod. The wrapper runs that call once and subtracts the actual
  recovered amount from dormant credit. It never creates new NPC combat credit.
  Remote human GoalRemote replays and all client-side mirrors do not debit it.
- NPC sleep clears tracked credit. Dead actors restore zero and revive clears
  their stored credit, including paths using the existing death reverse patch.
- Companion vitals carry the AI value through the existing spectator channel.
  Existing authority gates prevent late snapshots from overwriting resumed humans.
- Protocol V50 prevents older peers without these fields from joining.

The character key is the saved AI state; the profile scalar is the checkpoint
transport and last human snapshot. They are not independently competing totals.
An abrupt network loss can only preserve the latest update actually received,
as with existing stamina synchronization; this does not promise crash durability.

## Validation and remaining live checks

- Release build against installed 23.352: zero warnings/errors.
- StaminaRecovery: 20 native-IL/boundary checks, including precise call-site shape,
  capped regeneration, human/client exclusions, sleep/revival, packet ownership,
  late human rejection and no interception of ordinary food/direct recovery.
- SoloCharacter: 152 checks, including actual simulated save/reload switches with
  distinct allowances, checkpoint serialization, pre-update profiles, invalid
  credit, stale profile overridden after AI recovery and duplicate acknowledgement.
- HungerSync: 15 checks, including spectator allowance and late snapshot protection.
- Construction: 73 checks, including compiled member-reference resolution.

These harnesses check actual installed native IL and production logic against
isolated runtime boundaries. Live Unity multiplayer remains to be verified.

Staged build: `build/personal-stamina/ElinTogether.dll`. Installed on request after
confirming Elin.exe was closed, into `Package/Mod_ElinTogether/ElinTogether.dll`.
Installed SHA256: `0a5f8e57f727bd12ba26767ef387627185678101e6b2c7b50aaa06f24902c915`.
Previous DLL backup: `C:\Users\Vic\ElinTogether-backups\20261005-212256-before-personal-stamina\ElinTogether.dll`.

In game, use a recovery-tagged combat ability on each human, save/rejoin, and
confirm each recovery allowance follows its owner. Repeat with take-a-break,
NPC regeneration, resume, sleep and offline character switching. Recovery already
consumed by the AI must not reappear when a human resumes. Both peers need V50.
