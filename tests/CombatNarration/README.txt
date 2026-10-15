Combat narration: source-driven presentation repair

Native source inspected: installed stable Elin 23.355.
AttackProcess.Perform rolls hit/miss, emits attackMelee/attackMeleeToPC, then
synchronously calls Card.DamageHP for its dmgMelee/kill_* ending.
Installed sharedassets0.assets language entries include:
  attackMelee: #1 #3 #2 and
  attackBlunt: smash(es)
  kill_attack: kill(s) #him2. (one of the native randomized alternatives)

EMP previously reran Act.Perform on clients, including a new hit roll, while
blocking client DamageHP and replaying the host damage separately next flush.
Consequently the introduction and ending could disagree or be separated.

Implementation boundaries:
- Scope AttackProcess.Perform, ActMelee.Attack (including nested reactions), and
  Card.DamageHP. Explicitly scope the reverse-patched damage entry too, since
  ordinary Harmony prefixes/finalizers do not wrap reverse patch invocation.
- Capture only native LangGame rows in group "combat". Select random templates
  on the host and retain native semantic arguments, colors and explicit newlines.
- Nest scopes into one completed-operation packet. No change to damage queues,
  death replay, combat rolls, animation, experience, or turn scheduling.
- Suppress the same combat-group messages during client prediction/replay.
- Format the selected templates using native GameLang.Parse and recipient-local
  Card names/pronouns. Do not reroll the template or reinterpret resolved text as
  a language key. Removed-card fallback uses the host's already resolved text.
- Preserve ordinary host visibility while capturing offscreen messages for a
  client that can see the speaker. Client filtering ignores a newly dead speaker's
  dead flag, since the packet describes an already completed action.
- Respect ignoreAll and intentional relay suppression. Personal relay scopes
  must not additionally forward combat fragments as raw messages.
- Solo gameplay and messages outside these scopes/groups retain their paths.
- Protocol V55 is required on both peers; new union ID 624.

Automated verification:
  dotnet run --project tests/CombatNarration/CombatNarration.csproj
  dotnet run --project tests/CombatNarrationWire/CombatNarrationWire.csproj -- <staged package> <Elin directory>
The first links production capture/formatting code to a simulated game boundary.
The second loads the staged DLL, resolves Harmony targets against the actual
installed assemblies and round-trips an actual EMP message envelope/union using
SteamNetSerializer. It does not start Unity or apply Harmony patches in-game.
Affected regressions: UpstreamIntegration, AutoActSteps, FighterBounty. The bounty
suite accepts the staged package directory as its optional first argument.

Two-player acceptance (same V55 build on both peers):
1. Host hits, misses and kills an enemy; client sees complete coherent sentences.
2. Client attacks: no unfinished predicted introduction; confirmed text arrives
   with network latency and calls the client "you", not the host.
3. Enemy attacks host, then client. Check names, pronouns and displayed outcomes.
4. Multi-hit melee, ranged attacks, counters/parries, critical and elemental hits.
   Verify that introductions and endings stay paired with notifications enabled.
5. Fight visible only to client. Confirm host receives no offscreen log spam and
   client still sees combat. Repeat with a killing blow and disappearing target.
6. Confirm ordinary dialogue, social notifications, Auto Act confirmations and
   fighter bounties are neither duplicated nor suppressed.
7. Solo session: combat narration continues immediately through vanilla.

Limits: this targets combat narration, not a global rewrite of every mod's UI.
Direct WidgetMainText output from third-party mods is not replicated by this path.
Existing general dialogue relay/history ordering issues are separate work.
