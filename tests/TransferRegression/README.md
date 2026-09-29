# Transfer / interrupted-join regression checks

Run with the repository's Windows .NET 11 SDK:
`dotnet run --project tests/TransferRegression/TransferRegression.csproj -c Release`

Links the production client-startup iterator and inventory-sort patches. Small
stubs model readiness, scheduler subscription, input and Harmony attributes.
Checks an abandoned startup followed by an independent successful rejoin,
already-ready startup, one subscription per completed iterator, the host/client
sort-guard input matrix, delayed sorting after Shift release, and existing nested
client merge-scope cleanup. These are boundary checks, not Unity/Harmony or live
Steam integration tests. Unity stopping the abandoned iterator is modeled by
ceasing to advance it; the test does not claim IEnumerator.Dispose by itself
prevents callers from invoking MoveNext again.

Live acceptance:
- Quit/restart both games once when installing (clears the old game's static helper).
- Join and place the previously failing box from the client. Confirm on BOTH screens.
- Disconnect while joining, then rejoin without restarting the client; repeat placement.
- Confirm `Started client delta sending after game became ready`, then the
  item/build batch send and matching host build apply/completion entries.
- Open a box with right-click, then Shift-click an adjacent non-stackable pair.
  Only the clicked item should move, with no auto-sort between transfer and callback.
- Repeat with host and client doing the transfer; retain deliberate sweep transfer
  and ordinary auto-sort after Shift release.
