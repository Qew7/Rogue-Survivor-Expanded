---
name: rogue-add-world-event
description: Add a scheduled invasion, arrival, raid, story trigger, or other multi-turn world event.
---

# World event or story trigger

Use `WRogue/Engine/RogueGame.Events.cs` for event eligibility and effects, and wire the check into `RogueGame.Flow.cs` if it must run on world turns. Story events and one-off player triggers live in `RogueGame.Story.cs`. Inspect population helpers, district/map selection, scoring and messages. Define the exact time, place and one-time or repeat behavior; avoid relying on global random state in tests.

Connect significant arrivals, raids, discoveries, and story changes to the personality system: read `docs/npc-personality.md` and use `rogue-add-personality` when adding or changing memories or traits. Report a `SignificantEvent` through `RogueGame.ReportPersonalityEvent` after the world effect succeeds, at its actual position and with the real participants. Inspect `RogueGame.Personality.cs` and `NotifyOrderablesAI` for existing arrival/raid reports before adding another. A failed spawn or an eligibility check must not create an arrival memory. Distinguish peaceful arrivals from raids and preserve the actual gang leader/scout for group attribution.

Source-specific memories and acquired traits are registered in `PersonalityWorldContent.cs` via `PersonalityContent.Create()`. Keep their stable IDs outside the random starting pool; define observation conditions, person/group/faction roles, trait prerequisites, and ordered skill fallbacks with `rogue-add-personality`. Keep memories private during gameplay and retain resolved memories in relationship histories. Give new event kinds readable chronicle text in `ResidentRecords.cs` or `PersonalityWorldContent.EventName` so `Read Records` includes the event and its memory outcomes.

Add a scenario under `tests/scenarios/cases/world/` or `npc/` that sets a fixed day/seed, runs the real event or turn path, and asserts spawned actors/items, map changes and scoring or session flags. Cover an ineligible time or place and repeated invocation if duplication matters. Run it and `docker build --target test .`; use `bash tests/e2e.sh` if the event introduces a player-facing flow.

For personality integration, assert that the real handler creates the expected memory for a witness and attributes it to the correct person/group/faction; include an unseen or failed-event boundary. When adding outcomes, resolve the memory and assert the acquired trait or skill and its gameplay effect. Check one-time encounter rewards and retained resolved history across save/load when applicable; persistent changes also require updating `docs/save-format.md`.
