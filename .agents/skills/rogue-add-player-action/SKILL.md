---
name: rogue-add-player-action
description: Add a player command or interactive action, including input, legality, world effect, and scenario coverage.
---

# Player action and input

Separate input from execution. For a new key command, check `PlayerCommand.cs`, `Keybindings.cs`, `PlayerCommandCatalog.cs`, `InputTranslator.cs`, and the matching `RogueGame.PlayerActions.*.cs` handler. Implement game rules in `Rules*.cs` and the action in `Engine/Actions/` using `IsLegal` and `Perform`; put resulting state changes in the matching `RogueGame.Actions.*.cs` part. Update help text if the player sees a new command.

Add a scenario under `tests/scenarios/cases/` in the relevant gameplay topic. Execute the real action and assert AP cost, target and world changes, then try an illegal target and assert no effect. Add an input test if key translation is new. Run the scenario and `docker build --target test .`; run `bash tests/e2e.sh` for mouse, keyboard or UI flow changes.
