---
name: rogue-add-location
description: Add a building, district location, basement, or connected map to town generation.
---

# Generated location

Work in the matching `WRogue/Gameplay/Generators/BaseTownGenerator.*.cs` part or `StdTownGenerator.cs`. Trace placement from block selection through tiles, map objects, zones, population and exits. For a new tile or decoration, update `GameTiles.cs`, `GameImages.cs`, and assets. Linked maps need reciprocal `Exit` targets, valid walkable arrival tiles and district references.

Add a deterministic scenario under `tests/scenarios/cases/generation/` with a fixed seed or direct generator call. Assert topology, reachable entrances, intended objects/items/NPCs and connected exits. If probability makes a seed brittle, expose a focused generation operation rather than asserting only that a random district contains something. Run the scenario and `docker build --target test .`.
