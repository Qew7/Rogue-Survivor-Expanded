---
name: rogue-add-ai-behavior
description: Add or alter a civilian, zombie, follower, or other NPC decision policy and test its action choice.
---

# NPC behavior

Find the controller in `WRogue/Gameplay/AI/` and follow its `UpdateSensors`/`SelectAction` path. Reuse `BaseAI` navigation, combat, inventory and perception helpers where appropriate; inspect the actor abilities that gate behavior. Keep the returned `ActorAction` legal and avoid depending on UI or wall-clock timing.

Add a fixed-seed scenario under `tests/scenarios/cases/npc/`. Place a production controller with one clear stimulus and an obstacle or competing choice. Use `ScenarioWorld.NpcTurn` to execute its real action; assert the chosen action's world effect, not merely that an action was returned. Include a case where the stimulus is unavailable. Run the scenario and `docker build --target test .`.
