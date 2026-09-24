# Rogue Survivor development

Project skills live in `.agents/skills/`. Use the matching `rogue-add-*` skill when extending a gameplay system; its instructions point to the actual registration and behavior paths.

Every gameplay extension needs a deterministic, named scenario in its own file under `tests/scenarios/cases/<topic>/`. Exercise the real game model, rule, action, AI decision, or generator and assert the resulting state, including an invalid or boundary case where relevant. See `docs/gameplay-scenarios.md` for the runner. Run `sh tests/scenario.sh <name>` while developing, then `docker build --target test .`; run `bash tests/e2e.sh` for changes to startup, UI, input, rendering, or assets.

The original Windows project lists C# source files explicitly in `WRogue/RogueSurvivor.csproj`. Add new production files there and verify the portable Docker build. Preserve existing numeric content IDs because saved games store them; inspect ranges and sentinel values before adding an ID. For persistent fields, also check `docs/save-format.md` and add a save/load scenario.
