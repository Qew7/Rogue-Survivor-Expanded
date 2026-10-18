---
name: rogue-add-combat-rule
description: Change melee, ranged, damage, defense, infection, or combat targeting rules with scenarios.
---

# Combat mechanic

Trace the full path through `Rules.Combat.cs`, `RogueGame.Combat.cs`, `RogueGame.Actions.Combat.cs`, and `Engine/Actions/ActionMeleeAttack.cs` or `ActionRangedAttack.cs`. Check actor skill bonuses, weapons, cover, AP/stamina/ammo cost, infection and death handling as relevant. Keep deterministic rolls in tests by using a fixed seed or an outcome with no ambiguity.

For a bonded companion's death, `RogueGame.Combat.cs` applies the single sanity cost using `NpcImmediateEmotion.BondDeathLoss`. Keep event observation and memory resolution separate from this immediate cost so the same death does not charge sanity twice.

Add a scenario under `tests/scenarios/cases/combat/` with real opposing actors and the actual action. Assert legal target selection, resources spent, damage or status, and a negative case such as blocked fire, no ammo or invalid range. If balance changes affect several actor types, add focused cases for those combinations. Run the named scenario and `docker build --target test .`.
