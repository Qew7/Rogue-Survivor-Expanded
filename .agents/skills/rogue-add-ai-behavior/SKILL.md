---
name: rogue-add-ai-behavior
description: Add or alter a civilian, zombie, follower, or other NPC decision policy and test its action choice.
---

# NPC behavior

Find the controller in `WRogue/Gameplay/AI/` and follow its `UpdateSensors`/`SelectAction` path. Reuse `BaseAI` navigation, combat, inventory and perception helpers where appropriate; inspect the actor abilities that gate behavior. Keep the returned `ActorAction` legal and avoid depending on UI or wall-clock timing.

For behavior with a lasting social or emotional consequence, inspect `docs/npc-personality.md` and use `rogue-add-personality` when adding or changing memories or traits. Reuse an existing `SignificantEvent` kind when it describes the action; otherwise report a new kind through `RogueGame.ReportPersonalityEvent` from the successfully performed action. Selecting, considering, or failing an action must not create its consequence. Check existing action hooks before adding a report to avoid duplicate events. Routine movement or sensing does not need a new memory by itself.

Use `PersonalitySystem.Bias`, `HasTrait`, and `Attitude` where traits or remembered relationships affect the choice; retain the existing behavior when the preset disables personalities. Attribute events to the actual participants and position so observation and person/group/faction histories remain correct. Keep NPC memories private during gameplay. For a new event kind, provide readable chronicle text in `ResidentRecords.cs` or `PersonalityWorldContent.EventName` for `Read Records`.

Add a fixed-seed scenario under `tests/scenarios/cases/npc/`. Place a production controller with one clear stimulus and an obstacle or competing choice. Use `ScenarioWorld.NpcTurn` to execute its real action; assert the chosen action's world effect, not merely that an action was returned. Include a case where the stimulus is unavailable. Run the scenario and `docker build --target test .`.

When the behavior emits a significant event or uses memories, also assert the resulting observation/memory and its participant attribution. Exercise memory resolution and the resulting AI choice or relationship effect when adding an outcome. Cover a relevant boundary such as an unseen action, a failed action, or disabled personalities. Resolved attributed memories must remain in relationship history; persistent changes need a save/load scenario as described in `docs/save-format.md`.
