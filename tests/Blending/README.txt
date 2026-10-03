Blending regression, 2026-10-03

Native references: InvOwnerDraglet.CopyOnTransfer uses the existing target;
InvOwnerBlend._OnProcess calls owner.trait.OnBlend(target, recipient).
TraitDrink.OnBlend handles acidproof/cursed degradation, water blessing,
food poison/love and consumes one mixture. Food/water split a single target.

Previous MP generic inventory replay had no InvOwnerBlend destination handler,
while client ModNum was suppressed. Client coating could appear without host
application or potion consumption. Client log stack 70669/338 remained six;
host stack 19706/338 decremented normally.

Now client UI sends one validated, deduplicated host request; native host blend
produces normal inventory/count/enhancement deltas plus absolute coating,
blessing and food-element results. Split products are captured, not recreated.
Host UI and AI/native blends use the same result capture. No polling/save edit.

Tests link production request/result and Harmony handler methods, with native
and Unity boundary models. They do not replace a live two-peer playtest.

Playtest with both peers on protocol V31:
- Client blends ordinary acidproof liquid onto equipped uncoated amulet.
  Equipment stays equipped; potion decreases once; both see acidproof.
- Repeat on already coated item: native eligibility refuses, no consumption.
- Repeat using last potion: potion disappears, target remains equipped.
- Host coats client/ally item; client sees result without reconnecting.
- Cursed acidproof retains native degradation rather than adding coating.
- Blessed/cursed water on stack affects one item; food poison/love likewise.
- Confirm counts and effects remain correct after save/rejoin.
- Look for Blend resolved / Blend result applied / Blend rejected in logs.

Earlier client-only coatings are not retroactively charged or assumed valid.
Reload/rejoin loads the host's true item state before retesting.
