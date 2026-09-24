---
name: rogue-add-survival-rule
description: Add or adjust hunger, sleep, stamina, sanity, infection, weather exposure, or resource decay.
---

# Survival mechanic

Find the owning rule in `Rules.cs` or `Rules.Actors.cs`, item effects in `Rules.Items.cs`, and per-turn application in `RogueGame.Simulation.cs` or `RogueGame.Progression.cs`. Check living and undead differences, skill modifiers, messages and limits. Preserve consistent current/max values when a rule changes capacity.

Add a fixed-seed scenario under `tests/scenarios/cases/survival/` that runs the real action or world turn and asserts the resource/status change. Include a threshold or protected case (for example, full meter, exhausted actor, indoors, or undead) and a save/load check for newly persistent state. Run it and `docker build --target test .`.
