# Rogue Survivor Expanded

An expanded version of roguedjack's Rogue Survivor, based on [Tranquill6's source](https://github.com/Tranquill6) (Alpha 10.1). Current Expanded version: **0.4.1**.

## Play

**macOS / Linux:** Install Docker with Compose, then run:

```sh
docker compose up --build -d
```

Open [localhost:6080](http://localhost:6080), click the game window, and press Enter if prompted to create data folders. Saves persist in the `game-data` Docker volume.

**Windows:** Download the Windows ZIP from [Releases](../../releases), extract it, and run `RogueSurvivor.exe`. Requires .NET Framework 4.8.

## What's new

- Mouse movement and context menus.
- Mods and configurable game presets.
- NPC traits, memories, conversations, and emergent stories.
- Claimable bases, storage rooms, and follower scavenging.

## Справочник

- [Все черты характера NPC и их влияние](docs/npc-traits-guide.md)

## Contribute

Read [AGENTS.md](AGENTS.md), make your change, and open a pull request. For gameplay changes, add a [scenario](docs/gameplay-scenarios.md) and run:

```sh
sh tests/scenario.sh <name>
docker build --target test .
```

For UI, input, rendering, or asset changes, also run `bash tests/e2e.sh`.
