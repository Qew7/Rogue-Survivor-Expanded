---
name: rogue-add-faction
description: Add a faction, its hostility and membership rules, and faction scenarios in Rogue Survivor.
---

# Faction

Register the faction in `WRogue/Gameplay/GameFactions.cs`: append its ID, update `_COUNT`, model array, accessor, construction and directed enemy relations. Inspect `WRogue/Data/Faction.cs` before assuming hostility is symmetric. Trace actor spawning and membership through `GameActors.cs`, `BaseTownGenerator.Population.cs`, `RogueGame.Events.cs`, and `Rules.Actors.cs` as applicable.

Add a named scenario under `tests/scenarios/cases/factions/` that places members of the new and existing factions and asserts hostility from both directions, legal attacks, and any recruitment or leadership rule the feature changes. If spawns are added, verify their faction in a deterministic generation or event scenario. Run the scenario and `docker build --target test .`.
