---
name: rogue-add-actor
description: Add a living actor, creature, or NPC archetype and verify its generation and behavior.
---

# Living actor or creature

Append the model ID and definition in `WRogue/Gameplay/GameActors.cs`, choosing abilities, stats, image or doll, and AI controller from a comparable actor. Set faction and initial equipment in `BaseTownGenerator.Population.cs` or the relevant event path. Check whether `GameImages.cs`, `GameItems.cs`, and actor CSV data need entries. Keep existing model IDs unchanged for saves.

Add a scenario under `tests/scenarios/cases/npc/` or `generation/`: create or generate the actor, assert model, faction, stats and equipment, and run a real controller decision in a situation that reveals its distinctive behavior. Include a save/load assertion if the model adds state. Run it and `docker build --target test .`.
