---
name: rogue-add-personality
description: Add or change NPC memories, traits, relationship effects, and their gameplay scenarios in Rogue Survivor.
---

# NPC personality content

Read `docs/npc-personality.md` and inspect `WRogue/Gameplay/Personality/PersonalityContent.cs`, `PersonalityDefinitions.cs`, and `PersonalitySystem.cs` before editing. Register content in `PersonalityContent.Create()` with stable string IDs. Never reuse or rename a persisted ID without a save migration. Keep memories private to their owner; player facing descriptions must not expose another NPC's memory list.

For a trait, choose the `DecisionKind` effects that alter a real AI choice, set eligibility and conflicts in the registry, and account for item parameters when applicable. For a memory, choose a real `SignificantEvent`, observer/trigger conditions, evidence, ordered outcomes, and the person/group roles and feeling change. Check that `Report` can observe the event and that `ResolveDue` can resolve it. Relationship history must retain the memory after resolution and use stable actor and faction identities, including former groups.

The main-menu `Read Records` chronicle is an explicit viewer of saved NPC history outside gameplay. Check `WRogue/Data/ResidentRecords.cs` when adding significant event kinds: give them readable event text, and keep creation/resolution entries and histories of removed NPCs intact. `WRogue/Engine/RecordsReader.cs` must read saves without restoring the active session or changing mods/options. Keep older archives marked partial rather than inventing missing history.

Add a deterministic named scenario in its own file under `tests/scenarios/cases/npc/`. Exercise the real event, decision, and resolution path; assert the gameplay effect and a boundary such as an unseen event, duplicate event, missing target, disabled preset, or ineligible outcome. If saved state changes, add a save/load scenario and update `docs/save-format.md`. If new production files are added, list them in `WRogue/RogueSurvivor.csproj`.

Run `sh tests/scenario.sh <name>` during development and `docker build --target test .` before finishing. Run `bash tests/e2e.sh` if startup, input, rendering, UI, or assets change. Do not restart a running game container unless the user asks.
