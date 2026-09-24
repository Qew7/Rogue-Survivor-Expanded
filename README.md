# Rogue Survivor Expanded

An expanded version of roguedjack's Rogue Survivor, based on the source from
[Tranquill6](https://github.com/Tranquill6).

## Play on macOS or Linux

Install Docker with Compose, then run:

```sh
docker compose up --build -d
```

Open [http://localhost:6080](http://localhost:6080), click the game window to
focus it, and press Enter if the game asks to create its data folders. Save with
Shift+S. Saves live in the `game-data` Docker volume and survive
`docker compose down`; `docker compose down -v` deletes them. To use another web
port, start with `ROGUE_PORT=6081 docker compose up --build -d`. Sound is
unavailable in the browser session.

On Windows, build `RogueSurvivor.sln` in `Release|Any CPU` with .NET Framework
4.8 and run the executable from the `WRogue` directory.

## New features

### Mouse movement

Press M to toggle mouse movement. Hover over a destination to see the route and
step count, then left-click to walk. Clicking an adjacent enemy or interactive
object performs the usual bump action. Taking damage interrupts travel.

Right-click for a context menu of available actions. At a district edge,
right-click the exit marker (or your character while standing next to the edge)
and choose **Leave district** to travel to the neighboring district without a
numpad. Shift+M shows the mouse control help in the game log.

### Mods

Open **Mods** from the main menu to enable multiple mods and set their priority.
Use Space to toggle a mod, Left/Right to change its priority, Enter to apply,
and Esc to cancel. Higher-priority mods override files from lower-priority mods.

Put each mod in its own folder under `WRogue/mods`. A mod can replace files in
`Data/` and `Images/`; an optional `authors.json` can provide a description and
one or more authors and websites. `Deonapocalypse` is included as an example.

## Develop and test

```sh
docker build --target test .
bash tests/e2e.sh
sh tests/scenario.sh --list
```

Gameplay scenarios and how to run one are documented in
[docs/gameplay-scenarios.md](docs/gameplay-scenarios.md). See
[AGENTS.md](AGENTS.md) for contributor guidance and
[docs/save-format.md](docs/save-format.md) for save compatibility. GitHub
Actions runs the automated checks.
