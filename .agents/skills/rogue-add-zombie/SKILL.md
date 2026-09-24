---
name: rogue-add-zombie
description: Add a zombie or other undead actor variant, including spawn, evolution, AI, art, and behavior scenarios.
---

# Undead actor

Append a stable ID and model in `WRogue/Gameplay/GameActors.cs`; set abilities and controller consistently with nearby undead models. Check stats in `WRogue/Resources/Data/Actors.csv`, `GameImages.cs`, and `ZombieAI.cs` or another controller. Wire intentional spawn routes in `BaseTownGenerator.Population.cs`, `RogueGame.Events.cs`, and evolution or zombification branches where needed. A model does not spawn merely because it exists.

Add a scenario under `tests/scenarios/cases/npc/` that constructs or spawns the real model at a fixed seed, asserts undead faction, abilities and relevant stats, then runs a production AI action against a meaningful map situation. If it is an evolution, assert when the transition can and cannot occur; if it persists, round-trip a map save. Run the scenario and `docker build --target test .`.
