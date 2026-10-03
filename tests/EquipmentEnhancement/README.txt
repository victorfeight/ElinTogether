Equipment enhancement synchronization regression — 2026-10-03

Observed: host grindstone repair completed; client received the same girdle
UID 29300 at 04:04:30 and shoes UID 40047 at 04:04:36, retaining old stats.

Native TraitCrafter.Craft rust branch calls equipment.ModEncLv(1), consumes
one powder. Card.ModEncLv changes encLV, rebuilds material bonuses and modifies
armor/weapon elements. Previous element events only watched Chara; RemoteCard
returns cached items before examining transfer snapshot data. Ownership transfer
therefore did not refresh existing equipment enhancement.

Production fix: host Card.ModEncLv postfix emits absolute equipment level;
receiver uses native SetEncLv, preserving item identity and native equipment links.
Host AddThing completion also emits current level for equipment, so transferring
pre-fix repaired items reconciles their stale cached copies. Completion-time
updates join the existing result bundle. No full item replacement or repair replay.
Both players require protocol V29. MoreNotifications is unchanged.

Tests link production event and packet code. Mono.Cecil checks the installed Elin
method signatures and repair/setter/material-link call graph. Simulated boundaries
exercise two-step repair, cached equipped items, duplicate final states, transfer
recovery, negative/weapon changes, authority rejection and loading/generation.
These are not live Unity/Steam tests.

Live test: host and client update together; repair negative gear, compare its
modifier and detailed armor/weapon stats on both screens before/after returning it.
For the already repaired items, transfer again (or reload from the host's state).
No additional powder or save editing should be necessary for client-only stale data.
Check 'Equipment enhancement published' on host and 'Equipment enhancement applied'
on client for matching UID and final level. Verify equipped character stats update.
