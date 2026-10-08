---
name: rogue-add-ai-behavior
description: Add or alter a civilian, zombie, follower, or other NPC decision policy and test its action choice.
---

# NPC behavior

Find the controller in `WRogue/Gameplay/AI/` and follow its `UpdateSensors`/`SelectAction` path. Reuse `BaseAI` navigation, combat, inventory and perception helpers where appropriate; inspect the actor abilities that gate behavior. Keep the returned `ActorAction` legal and avoid depending on UI or wall-clock timing.

For trait-driven goals or planner operators, read `docs/npc-content-modules.md`. Register goal sources, desired facts, reusable `NpcOperatorSource` providers, action factories and perception/event subscriptions in a feature module under `WRogue/Gameplay/Npc/Content/`; common generator/planner/runtime services resolve contracts without per-ID branches. Validate real effects through `NpcActionContext` and reuse `BaseAI` navigation through `NpcExecutionContext`. Use `NpcInterestDefinition` for a lasting personal priority and `NpcCollectiveDefinition` for a group or faction task. Preserve saved IDs, and bump a module revision when changing plan parameters.

For behavior with a lasting social or emotional consequence, inspect `docs/npc-personality.md` and use `rogue-add-personality` when adding or changing memories or traits. Reuse an existing `SignificantEvent` kind when it describes the action; otherwise report a new kind through `RogueGame.ReportPersonalityEvent` from the successfully performed action. Selecting, considering, or failing an action must not create its consequence. Check existing action hooks before adding a report to avoid duplicate events. Routine movement or sensing does not need a new memory by itself.
If that event should be retold as a rumor or radio story, follow `rogue-story-narration` for `RetainFact`, report wording, the ongoing `StoryId` and real `CauseId`; the shared composer handles new kinds without a new narration path.

Use `PersonalitySystem.Bias`, `HasTrait`, and `Attitude` where traits or remembered relationships affect the choice; retain the existing behavior when the preset disables personalities. Attribute events to the actual participants and position so observation and person/group/faction histories remain correct. Keep NPC memories private during gameplay. For a new event kind, register an `NpcEventDefinition` in its module with payload requirements, archive categories/prose, audience and optional report prose. Use observation phases for responses and `AfterEvent` for cross-feature completion after all witnesses; avoid action-to-action subsystem calls. For an inferred private event, specify `PrivateAudience` so a named subject does not receive it.

When the chosen behavior has a known trigger, propagate its actual event ID through the goal, performed action and reply so Read Records can display the archived antecedent. The reader cannot safely explain a refusal as revenge or fear unless that reason affected the real decision and was recorded. For observed resource caches, update a known cache to zero when it empties, but do not create zero-unit entries for unrelated places in the bounded knowledge list.

When a goal uses remembered people or places, handle a person with no mapped `Place` and a zero flag from `NpcPlanDomain.At` before building an operator or speaking a map name. Guard optional faction data at both goal selection and motivation scoring. For shared stories, an individual goal's abandonment must not clear the group plan while the director still has active roles. Make combat scenario hit chances deterministic before asserting strict damage; prefer a specific event or state transition over a non-strict count assertion.

Add a fixed-seed scenario under `tests/scenarios/cases/npc/`. Place a production controller with one clear stimulus and an obstacle or competing choice. Use `ScenarioWorld.NpcTurn` to execute its real action; assert the chosen action's world effect, not merely that an action was returned. Include a case where the stimulus is unavailable. Run the scenario and `docker build --target test .`.

When the behavior emits a significant event or uses memories, also assert the resulting observation/memory and its participant attribution. Exercise memory resolution and the resulting AI choice or relationship effect when adding an outcome. Cover a relevant boundary such as an unseen action, a failed action, or disabled personalities. Resolved attributed memories must remain in relationship history; persistent changes need a save/load scenario as described in `docs/save-format.md`.
