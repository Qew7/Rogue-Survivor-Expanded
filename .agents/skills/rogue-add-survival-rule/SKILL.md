---
name: rogue-add-survival-rule
description: Add or adjust hunger, sleep, stamina, sanity, infection, weather exposure, or resource decay.
---

# Survival mechanic

Find the owning rule in `Rules.cs` or `Rules.Actors.cs`, item effects in `Rules.Items.cs`, and per-turn application in `RogueGame.Flow.Turn.cs`, `RogueGame.Simulation.cs` or `RogueGame.Progression.cs`. Check living and undead differences, skill modifiers, messages and limits. Preserve consistent current/max values when a rule changes capacity.

For a sanity response to an NPC event or radio report, follow `rogue-add-personality`: immediate effects go through `NpcImmediateEmotion` and change recoverable sanity, while lasting disposition changes come from resolved memories. Bonded companion death already spends sanity in `RogueGame.Combat.cs`; adjust that cost there without adding a second charge for the same death. Check that the player's existing rule remains intact when changing NPC-only responses.

Add a fixed-seed scenario under `tests/scenarios/cases/survival/` for a survival rule, or under `npc/` for an NPC personality reaction. Run the real action or world turn and assert the resource/status change. Include a threshold or protected case (for example, full meter, exhausted actor, indoors, or undead) and a save/load check for newly persistent state. Run it and `docker build --target test .`.
