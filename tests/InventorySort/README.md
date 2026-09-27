# Client inventory sort regression

Run with the pinned SDK:

```text
dotnet run --project tests/InventorySort/InventorySort.csproj -c Release
```

The harness compiles the production sort-scope and stacking patches. Its game
boundary reproduces vanilla UIInventory.Sort's retry-until-no-merges loop,
with an iteration cap to turn a freeze into a failing test. It checks finite
request generation, ordered stack consolidation without quantity loss,
reserved rows, mixed ownership, normal drag stacking, local/host behavior,
and nested scope cleanup on exceptions. Unity/Harmony detours and live MP
packet delivery still require in-game validation.

In MP, put two or three compatible stacks in the client's inventory, enable
Always Sort, and reopen the inventory. The UI should stay responsive and the
stacks should consolidate after the host responds. Repeat for manual sorting
and a shop, and confirm both peers see the same total quantity. Also check
ordinary drag stacking and a container with a reserved inventory row.

No protocol change: this uses the existing CardTryStackToDelta and leaves the
host as the authority for item counts and destruction.
