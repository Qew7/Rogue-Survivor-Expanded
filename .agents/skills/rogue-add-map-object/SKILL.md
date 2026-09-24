---
name: rogue-add-map-object
description: Add an interactive or breakable map object, including its action, rendering, persistence, and scenarios.
---

# Map object

Choose a model from `WRogue/Engine/MapObjects/` (`Door`, `Board`, `Fortification`, `PowerGenerator`) and extend `MapObject` or `StateMapObject` only if its behavior requires it. Wire placement in the relevant `BaseTownGenerator.*.cs` file, image IDs in `GameImages.cs`, action legality in `Rules.Interactions.cs`, and player/NPC interaction in `RogueGame.Actions.Objects.cs` and `Engine/Actions/`. Add a new production `.cs` file to `WRogue/RogueSurvivor.csproj`.

Add a scenario under `tests/scenarios/cases/world/` or `doors/` using `TownScenarioFactory.Arena`. Place the object, execute the actual action, assert state, AP cost, collision or blocking, and an illegal interaction. If it has mutable state, save and reload the map and assert it remains intact. Run the scenario and `docker build --target test .`.
