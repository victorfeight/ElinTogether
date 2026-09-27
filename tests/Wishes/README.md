# Host-authorized wishes

Run `dotnet run --project tests/Wishes/Wishes.csproj -c Release` with the pinned SDK.

The harness compiles the production wish-effect patch, prompt/reply packets and
pending-request lifecycle against a small game boundary. It checks suppression
of client prediction, solo/host fallthrough, recipient selection, prompt and reply
deduplication, sender validation, cancellation, simultaneous wishes, authoritative
power/blessing, player-context restoration (also on exceptions), and invalidation
on disconnect, session reset, death and zone changes.

These tests do not run Unity, the Harmony detour, vanilla's item matcher, or real
network serialization/delivery. The full mod is separately compiled against the
installed game's assemblies. Live MP acceptance is still required.

## Live acceptance

Both peers need API V10. Back up the host save before spending wishes in testing.

1. Client uses a charged wish rod while targeting their own tile. Exactly one
   normal wish dialog opens on that client; none opens on the host.
2. Enter `big fridge`. Confirm exactly one real item appears at the wishing
   character's position on both screens and survives pickup/reopening inventory.
3. Verify the original cast consumes only one charge. Submitting the dialog must
   not consume another charge or award casting experience again.
4. Test cancellation and an unmatched item name against vanilla behavior.
5. Verify normal/blessed/cursed rods retain vanilla reward calculations.
6. Host wishes should retain their normal local dialog and behavior.

Host logs include `Wish granted`, `Wish resolved`, `Wish cancelled`, and rejection
reasons, with request and recipient identifiers. The protocol adds only the
prompt and reply; reward generation/placement reuse existing card deltas and
wish confirmations reuse MsgSayDelta. The core Wish implementation is unchanged.

The common effect hook can serve other host-executed wish sources. This does not
establish that every upstream well/fishing event is already simulated correctly
for remote players; those trigger paths require separate gameplay verification.
