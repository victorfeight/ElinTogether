# Eternal League of Networking (EMP)

[![Elin Together CI Deploy](https://github.com/ElinTogether/ElinTogether/actions/workflows/emp_ci.yml/badge.svg)](https://github.com/ElinTogether/ElinTogether/actions/workflows/emp_ci.yml) [![GitHub tag](https://img.shields.io/github/tag/ElinTogether/ElinTogether.svg)](https://GitHub.com/ElinTogether/ElinTogether/tags/) [![.NET SDK 11.0.x](https://img.shields.io/badge/11-green?logoColor=blue&label=dotnet%20SDK&labelColor=blue)](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)


English | [中文](README_zh.md) | [日本語](README_ja.md)

Adventure through the world of [Elin](https://store.steampowered.com/app/2135150/Elin/) with your friends — build a home, dive into nefias, and watch error popups together.

After months of development, this mod is now in public beta. Please report any bugs you run into.

## Play

Requires [YK Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3400020753). Make sure it's placed above Elin Together in the mod viewer.

You can install this mod package via [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3773298709) or the automated builds from [GitHub Releases](https://github.com/ElinTogether/ElinTogether/releases).

### Version

The workshop release always tracks the latest Nightly build; if you run into compatibility issues with the stable game version, you can download the Stable build from GitHub instead.

### To host

- Launch the game **via Steam**, load a save or create a new game (recommended)
- Press **Esc** → **Mods** → **Elin Together** to open the multiplayer panel
- Start hosting there
- Invite players from the panel or use your Steam friends list

![Elin Together panel](https://i.postimg.cc/vHqQLbV0/Pix-Pin-2026-07-28-09-25-19.png)

To play with friends, keep the mod list as small as possible and identical for every player — Steam Workshop Collections make this easy to share.

## FAQ

### How to communicate with other players?

You can ping by `P` key or press `Return` to chat.

### How does the turn-based world work?

Each player acts at their own speed, and the host's world advances accordingly. Player actions are concurrent and do not block one another. You can also configure a shared average speed.

### How does combat work?

On top of the fluid turn sync system, you can also enable classic turn-based combat in the config, where each player decides their action before the world continues.

### Client players can't change map.

It's intended. Only the host player can change maps.

### Client players can't advance some quests.

In this fork, supported client quest actions are requests to the host, which owns the shared quest state. This includes supported quest-board and expansion actions; it does not make every custom quest script safe to execute on a client. See the compatibility index below for the adapter boundaries.

### Client players may see ghost items that can't be interacted with.

If items are out of sync, try resyncing — a quick resync can be initiated from the Elin Together panel on either the host or the client side.

### Connection froze; not responding; can't rejoin...

Restart the game to clean up the Steam connections.

### Is this compatible with X mod?

This fork includes the optional integrations below. They cover specific actions, not blanket compatibility with every version or feature of a mod. Both peers should use matching game, EMP, and gameplay-mod builds.

#### Compatibility source index

| Mod / integration | Entry points | Scope and boundary |
| --- | --- | --- |
| AutoAct | [Adapters](ElinTogether/Compatibility/AutoAct), [custom-step requests](ElinTogether/Models/Delta/Act/AutoActStepDelta.cs), [pour-water serializer](ElinTogether/Models/Delta/Task/TaskPourWaterArgs.cs) | Client action selection uses existing host-authoritative task execution. Custom cleaning, watering, refill, disarm and chat steps have validated requests; building waits for the host result. Mod-specific pour-water depth stays in its task serializer. |
| AutoAct Ally Expansion | [Assignment guard](ElinTogether/Compatibility/AutoActAllyExpansion/AutoActAllyGuard.cs) | Only the host assigns ally AI. Connected human players are excluded unless companion control is active. This is separate from a client's own AutoAct controller. |
| RandomQuest & GuildQuest Expansion Pack (`RandomQuestExpantion`) | [Adapters](ElinTogether/Compatibility/Quests/RandomQuestExpansion) | Host-owned quest callbacks and synchronized results. Includes an isolated thief-reward correction for the supported mod implementation. |
| Quest Board Expansion (`QuestPack`) | [Adapters](ElinTogether/Compatibility/Quests/QuestBoardExpansion) | Host-owned callbacks, shared quest results and persisted timed-skill participant ownership. |
| Both quest expansions | [Shared compatibility checks](ElinTogether/Compatibility/Quests/QuestExpansionCompat.cs), [world mutations](ElinTogether/Compatibility/Quests/QuestExpansionWorldMutationPatch.cs) | Shared adapter plumbing; ordinary quest replication remains in core. Custom drama and instance execution still belong to the host. |
| TpMagicAppendix | [Cast interception](ElinTogether/Patches/Compatibility/TpMagicAppendixPatch.cs), [spell bridge](ElinTogether/Models/TpSpellBridge.cs) | Explicit spell ID/alias allowlist. Executes native effects on the host and publishes results; unrelated spells are not automatically covered. |
| Quest timer UI | [Quest widget synchronization](ElinTogether/Models/QuestEventWidgetSync.cs) | Replicates host quest-event presentation rather than running the host's quest events on the client. |
| More Notifications / affinity UI | [Social notification hook](ElinTogether/Models/SocialNotifications.cs) | Emits confirmed client affinity changes for an optional UI subscriber. The subscriber lives outside EMP; initial loading does not emit affinity gains. |
| TpHugeShippingContainer | [Separate compatibility rebuild](compat/TpHugeShippingContainer/README.md) | Standalone mod correction, not automatically installed or enabled by EMP. Its own README records its target version. |

YKFramework is a required dependency, not an optional adapter. General inventory, terrain, container and quest synchronization stays in core even where a third-party mod exposed the original problem. Such fixes are not a promise of complete support for every mod that uses those systems.

AutoAct adapter checks live in [AutoActSolo](tests/AutoActSolo) and [AutoActSteps](tests/AutoActSteps); quest adapter checks live in [QuestReplication](tests/QuestReplication). These regression checks complement in-game host/client testing; they do not certify future third-party updates.

For maintenance, keep optional hooks under `Compatibility/<mod>` as they are organized, keep shared authority and packet schemas in core, and retain existing type identities when moving files. The TpMagicAppendix paths above still use the older layout. Avoid duplicating generic synchronization inside individual adapters.

## Report Bugs & Feature Requests

Use the [issue template here](https://github.com/ElinTogether/ElinTogether/issues/new/choose).

Reports left in the Steam Workshop comments section are ignored.

## Build

This project requires 2 environment variables:

`ElinGamePath` set to the root folder of the Elin game installation.
```
ElinGamePath/
├─ BepInEx/
│  ├─ core/
│  │  ├─ *.dll
├─ Elin_Data/
│  ├─ Managed/
│  │  ├─ *.dll
```

`SteamContentPath` set to your `steamapps/workshop/content` directory so `YKFramework.dll` can be referenced.

This project uses [.NET SDK 11.0](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) to compile correctly.

Clone the project:
```ps
git clone https://github.com/ElinTogether/ElinTogether.git
cd ElinTogether
```

Install the deps:
```ps
dotnet restore ./ElinTogether --locked-mode
```

Build the project:
```ps
dotnet build ./ElinTogether -c Debug -o ./out --no-restore
```

## Contributing

Please explain the changes and link any related issues. Be responsible for any AI-generated codes and do not push slop without reviewing and testing.

## Credits

- [DK](https://github.com/gottyduke) - code, framework
- [Redgeioz](https://github.com/Redgeioz) - code, framework
- [105gun](https://github.com/105gun) - code
- [Han](https://github.com/chuahan) - testing, a lot of
- [Omega](https://steamcommunity.com/profiles/76561198004587603) - testing
- [InuiDame](https://github.com/InuiDame) - testing
- [Drakeny](https://github.com/Drakeny) - testing
- [Overlord](https://github.com/overlord-99) - testing
- noa - supporting the project and modding community

---
<p align="center">MIT License, 2025-present</p>
