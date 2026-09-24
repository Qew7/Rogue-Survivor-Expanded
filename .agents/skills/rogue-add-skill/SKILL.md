---
name: rogue-add-skill
description: Add or change a living or undead character skill, its upgrade behavior, and gameplay tests in Rogue Survivor.
---

# Character skill

Use `WRogue/Gameplay/Skills.cs` for IDs, living/undead ranges, display names and CSV loading; add values to `WRogue/Resources/Data/Skills.csv`. Existing living and undead IDs occupy contiguous ranges: inserting a living ID shifts undead IDs stored in saves. Preserve old IDs by explicitly assigning values and adapting range-based rolls, or implement a documented save migration before changing them. Update sentinels and `UNDEAD_SKILLS` for undead choices.

Implement an observable effect in the relevant `WRogue/Engine/Rules*.cs` or `RogueGame*.cs` path. Check upgrade choice and NPC valuation in `RogueGame.Progression.cs`, descriptions in `RogueGame.Descriptions.cs`, and `OnSkillUpgrade` for effects that update current state, such as inventory capacity.

Add a separate `Skill<Name>Scenario.cs` under `tests/scenarios/cases/skills/living/` or `skills/undead/`. Compare a real rule or action before and after `SkillUpgrade`; check level and at least one behavior, not only metadata. `SkillScenario.Register` helps with numeric effects, while complex skills can use `ScenarioRunner.Add`. `SkillScenario.AssertCoverage` requires a named case for every ID. Run that scenario and `docker build --target test .`.
