---
name: rogue-add-personality
description: Add or change NPC memories, traits, relationship effects, and their gameplay scenarios in Rogue Survivor.
---

# NPC personality content

Read `docs/npc-personality.md` and inspect `WRogue/Gameplay/Personality/PersonalityContent.cs`, `PersonalityDefinitions.cs`, and `PersonalitySystem.cs` before editing. Register content in `PersonalityContent.Create()` with stable string IDs. Never reuse or rename a persisted ID without a save migration. Keep memories private to their owner; player facing descriptions must not expose another NPC's memory list.

Unique-character, faction, gang, and world/story experience definitions live in `PersonalityWorldContent.cs`, registered by `PersonalityContent.Create()`. Extend the appropriate source table and register memories with `starting: false`; acquired traits must remain outside the starting pool. Match unique actors by the registered `Session.UniqueActors` reference, never by name or model. `FirstEncounter()` uses retained per-person memory history to prevent repeat rewards after resolution and loading. Test namesakes, line of sight, and repeated encounters.

Use `TowardFaction()` for an explicit collective memory feeling or acquired trait bias, including events without a person such as supply drops. Do not repeat the generic personal help/attack feeling in supplemental faction memories. Check `PersonalitySystem.Attitude()` and a real trade/recruitment/trust decision, plus an unrelated faction and disabled-preset boundary. Declare conflicts for incompatible faction traits and verify their skill fallback.

Publish world experiences from successful handlers in `RogueGame.Events.cs` or `RogueGame.Story.cs`; use `RogueGame.Personality.cs` for arrival/gang classification. Raid source actors must be the real leader/scout, so group history remains attributed correctly. Peaceful arrivals must use their own significant event kinds. Unspawned actors and failed arrivals must not generate memories. Per-turn visible unique encounters are scanned in `RogueGame.Flow.Turn.cs`. Add a deterministic world-handler scenario under `tests/scenarios/cases/world/` when changing these sources.

For a trait, choose the `DecisionKind` effects that alter a real AI choice, set eligibility and conflicts in the registry, and account for item parameters when applicable. For a memory, choose a real `SignificantEvent`, observer/trigger conditions, evidence, ordered outcomes, and the person/group roles and feeling change. Check that `Report` can observe the event and that `ResolveDue` can resolve it. Relationship history must retain the memory after resolution and use stable actor and faction identities, including former groups.

The main-menu `Read Records` chronicle is an explicit viewer of saved NPC history outside gameplay. Check `WRogue/Data/ResidentRecords.cs` when adding significant event kinds: give them readable event text, and keep creation/resolution entries and histories of removed NPCs intact. `WRogue/Engine/RecordsReader.cs` must read saves without restoring the active session or changing mods/options. Keep older archives marked partial rather than inventing missing history.

Add a deterministic named scenario in its own file under `tests/scenarios/cases/npc/`. Exercise the real event, decision, and resolution path; assert the gameplay effect and a boundary such as an unseen event, duplicate event, missing target, disabled preset, or ineligible outcome. If saved state changes, add a save/load scenario and update `docs/save-format.md`. If new production files are added, list them in `WRogue/RogueSurvivor.csproj`.

Run `sh tests/scenario.sh <name>` during development and `docker build --target test .` before finishing. Run `bash tests/e2e.sh` if startup, input, rendering, UI, or assets change. Do not restart a running game container unless the user asks.
