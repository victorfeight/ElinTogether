# Gift and conversation reconciliation

## Phase 1 — Resolve the gift once on the host

Native source checked: `HotItemHeld.TrySetAct` gift callback, `Chara.SplitHeld`,
`Card.Split`/`Duplicate`, `Chara.CanAcceptGift`/`IsValidGiftWeight`/`GiveGift`,
`Affinity.OnGift`, and `ThingContainer.IsFull`.

The UI splits one held item before calling GiveGift. A client split receives a
pending UID; CardSplitEvent records the original stack, but the former gift
packet did not use PendingSplit. RemoteCard.Find cannot resolve an unknown
pending UID on the host. Giving locally before asking the host also ran effects
and random affinity rolls independently.

Implemented: reuse PendingSplit; suppress client execution; validate requester,
zone, distance, recipient, source ownership, quantity and vanilla acceptance;
split one item on the host under Simulate; publish normal inventory outcomes.
Request IDs prevent duplicate consumption. A live split abandoned by an early
return is returned to its owner. No general inventory capacity restriction was
removed. The old replay/reverse-patch gift route was replaced.

## Phase 2 — Share the actor scope and resolved social results

Native source checked: `Affinity.OnTalkRumor`, `Chara.ModAffinity`,
`DramaCustomSequence.Default`, and `_affinity` storage. ModAffinity returns
immediately for self-affinity. Replaying client-to-host gifting with the host
as EClass.pc therefore cannot award affinity correctly.

Implemented: SocialInteractions provides the same exception-safe player,
static affinity, karma and message scope for gifts and conversation. Normal
dialogue sends intent; AutoAct's existing request/completion path delegates to
the same host resolver. Results carry absolute affinity, interest, disclosed
favourites, and changed actor/recipient elements. Explicit element results
are necessary because ordinary ElementChangeDelta ignores the receiving PC.

The host's native character fields remain the persistence source. No new
relationship table, profile checkpoint field, migration, or periodic capture
was added. NPC affinity remains one shared value, as in the previous save
model; this does not create independent relationships with each of three or
more players. Existing client-only affinity increases cannot be reconstructed.

Human gift recipients keep equipment and inventory control; native TryEquip,
TryClearInventory and nextUse follow-up remain available to AI companions.

Ticket audit: GiveGift can schedule AI_Massage on the giver or AI_ArmPillow on
the recipient. CharaTaskRemoteEvent replaces connected-human AI with
GoalRemote. A scoped capture preserves those native ticket actions and sends
them to the owning client, once, where normal MP task execution begins.
Massage now has a task serializer, alongside the existing arm-pillow one.
Late results do not start tasks after a zone change or while local input is
blocked for a control handoff. Consumables, conditions and quest callbacks
continue through native GiveGift and existing MP result channels.

## Phase 3 — Verification and multiplayer acceptance

Automated coverage links production gift/talk requests, split mapping, scopes,
patches and result application to a simulated Unity/transport boundary.
It checks split and last-item gifting, duplicate requests, ownership/range/
zone/capacity/important-item rejection, exception cleanup, human/companion
behavior, both gift directions, conversation affinity and negotiation XP,
favourite fields, ticket ownership/deduplication/control handoff, and solo
passthrough. AutoAct's existing regression suite checks delegation to the
shared conversation resolver. These are not live Unity/Steam acceptance tests.

In-game acceptance, with the same V27 build on both machines:

1. Client gives one ordinary food from a stack of five to host: four remain,
   recipient gains exactly one; both screens agree. Repeat with the last item.
2. Reverse the direction; then gift a normal NPC. Check affinity and that
   human equipment does not change automatically.
3. Try a full recipient inventory and an important-marked item. Native
   restrictions still apply and no item is consumed. Weight rejection occurs
   after selecting Give; a full inventory can hide Give altogether.
4. Compare ordinary talking with AutoAct talking. Check affinity, interest and
   the speaker's negotiation XP; the other player's skill must not gain XP.
5. Test favourites and a special gift separately. For massage/arm-pillow, check
   who starts the action, cancellation, completion, conditions and stamina.
6. Host saves; reconnect, then test solo character switching from a backup.
   Confirm the item, affinity and learned favourites persist. Also test a
   recipient in Take a Break mode and after resuming human control.

Keep host and client Player.log and Session logs for these checks. The host
`Gift resolved` entry records request ID, giver, recipient, source and split
item IDs, remaining count and destruction state. Pair this with existing
inventory transfer logs when examining an outcome.

Deployed with the subsequent V28 inventory-capacity, live land-expansion and
local-widget fixes. Both peers need V28. User confirmed gift giving works in
live multiplayer; the remaining acceptance cases above are not implied by
that confirmation.
