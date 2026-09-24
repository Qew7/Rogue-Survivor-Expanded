---
name: rogue-add-item
description: Add a food, medicine, weapon, trap, armor, tool, or other inventory item with gameplay tests.
---

# Item

Inspect the closest model and instance type in `WRogue/Engine/Items/`. Append a stable ID in `WRogue/Gameplay/GameItems.cs`, register the model in `GameItems.Models.cs`, and update CSV loading in `GameItems.Loading.cs` plus the relevant resource row when the category uses data files. Register images in `GameImages.cs` and generation or loot placement in the relevant `BaseTownGenerator.*.cs` path. Check `Rules.Items.cs`, `RogueGame.Actions.Items.cs`, and AI item valuation for usable items.

Add a scenario under `tests/scenarios/cases/items/` that obtains the real item and performs its defining action. Assert inventory/ground state, consumed quantity or ammo, and the rule effect; also test a blocked or exhausted case. For special mutable items, round-trip a map save. Run it and `docker build --target test .`.
