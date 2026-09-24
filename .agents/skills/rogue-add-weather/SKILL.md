---
name: rogue-add-weather
description: Add a weather state or change weather transitions and effects in Rogue Survivor.
---

# Weather

Start with `WRogue/Data/Weather.cs` and `RogueGame.Progression.cs:ChangeWeather`. Add transitions and arrival text for the new state. Trace every `Weather` switch, especially `Rules.cs` (vision, rain classification, odors), `RogueGame.Descriptions.cs`, and `RogueGame.Rendering.Elements.cs`; decide how the state affects each system. Check initial weather selection in `RogueGame.WorldGeneration.cs` and persistence through `World.Weather`.

Add `tests/scenarios/cases/world/<Name>WeatherScenario.cs`. Set a fixed weather in `Session.Get.World`, assert a real rule or world effect and a contrasting state, then check transitions or rendering-related state when applicable. A new enum value must survive a save/load scenario. Run the named scenario and `docker build --target test .`; use `bash tests/e2e.sh` if visuals changed.
