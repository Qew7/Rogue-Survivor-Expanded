# Rogue Survivor Expanded

An expanded version of roguedjack's Rogue Survivor, based on [Tranquill6's source](https://github.com/Tranquill6) (Alpha 10.1). Current Expanded version: **0.4.1**.

## Play

**macOS / Linux:** Install Docker with Compose, then run:

```sh
docker compose up --build -d
```

Open [localhost:6080](http://localhost:6080), click the game window, and press Enter if prompted to create data folders. Saves persist in the `game-data` Docker volume.

**Windows:** Download the Windows ZIP from [Releases](../../releases), extract it, and run `RogueSurvivor.exe`. Requires .NET Framework 4.8 or 4.8.1.

## What's new

- Mouse movement and context menus.
- Mods and configurable game presets.
- NPC traits, memories, conversations, and emergent stories.
- Linked rumors of up to ten events and four radio stations with one shared hourly program per station. Familiar stories are favored, while unknown reports and station interludes still air. Bump a household radio to tune it; use a portable receiver from the inventory. Base robberies become news when witnessed or when their losses are discovered later; fatal raids can also be reported by witnesses or group members who find a body. Stolen goods keep the victim group's identity across gifts, trades, drops and death, allowing someone who recognizes them by sight or on pickup to continue the same story. Reports name a thief only when a witness identified them. The survivor station covers psychopath sightings, the gang station covers crew arrivals, and broadcasts use varied names for shelters.
- Claimable bases, storage rooms, and follower scavenging.

## Reference

- [All NPC personality traits and their effects](docs/npc-traits-guide.md)

## Contribute

Read [AGENTS.md](AGENTS.md), make your change, and open a pull request. For gameplay changes, add a [scenario](docs/gameplay-scenarios.md) and run:

```sh
sh tests/scenario.sh <name>
docker build --target test .
```

For UI, input, rendering, or asset changes, also run `bash tests/e2e.sh`.
