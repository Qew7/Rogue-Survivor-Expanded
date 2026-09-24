---
name: rogue-add-world-event
description: Add a scheduled invasion, arrival, raid, story trigger, or other multi-turn world event.
---

# World event or story trigger

Use `WRogue/Engine/RogueGame.Events.cs` for event eligibility and effects, and wire the check into `RogueGame.Flow.cs` if it must run on world turns. Story events and one-off player triggers live in `RogueGame.Story.cs`. Inspect population helpers, district/map selection, scoring and messages. Define the exact time, place and one-time or repeat behavior; avoid relying on global random state in tests.

Add a scenario under `tests/scenarios/cases/world/` or `npc/` that sets a fixed day/seed, runs the real event or turn path, and asserts spawned actors/items, map changes and scoring or session flags. Cover an ineligible time or place and repeated invocation if duplication matters. Run it and `docker build --target test .`; use `bash tests/e2e.sh` if the event introduces a player-facing flow.
