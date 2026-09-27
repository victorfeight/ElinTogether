# Task tools and boss loot terrain

Both peers must install this build (network API V9). Existing local Timeout configuration is unchanged.

1. Alternate pick/axe and immediately start mining/harvesting, manually and using Auto Act. Repeat while another task is finishing. Host must use the selected tool without needing a second switch.
2. Defeat a boss near walls/objects. Both screens must show the same chest position and cleared tile; no extra neighboring wall should disappear only on the client.
3. Leave/re-enter any already-desynchronized map to load a fresh host snapshot; this build prevents new mismatches in these paths, not retrospective repair of existing tiles.

Logs: Task tool selected; selected tool missing or not owned; Applied boss loot terrain. Capture Player.log and the dated Session log from both peers if a failure remains.

Automated checks exercise production logic against simulated game/transport boundaries and validate the boss patch call sites against the installed Elin assembly. Live multiplayer acceptance remains required.
