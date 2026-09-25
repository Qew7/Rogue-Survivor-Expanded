# Rogue Survivor Expanded

An expanded version of roguedjack's Rogue Survivor, based on the source from
[Tranquill6](https://github.com/Tranquill6).
Current Expanded version: **0.1.1** (based on Rogue Survivor Alpha 10.1).

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

When an action asks for a direction (push, pull, barricade, build, give, trade,
and similar actions), click an adjacent tile. Click your character for actions
that can target yourself, or right-click to cancel. Direction keys and Escape
still work. While aiming a grenade, hover over a tile to preview the target and
left-click to throw; you can still aim with direction keys and throw with F.

### Mods

Open **Mods** from the main menu to enable multiple mods and set their priority.
Use Space to toggle a mod, Left/Right to change its priority, Enter to apply,
and Esc to cancel. Higher-priority mods override files from lower-priority mods.

Put each mod in its own folder under `WRogue/mods`. A mod can replace files in
`Data/` and `Images/`; an optional `authors.json` can provide a description and
one or more authors and websites, plus `version` and `game_version`.
The `game_version` value must name this version (`0.1.1`) or an earlier patch
in the `0.1` series to enable the mod. `Deonapocalypse` is included as an example. The selected set is remembered for
new games. Loading a save automatically uses its recorded mods and priority;
returning to the menu restores the previous selection. Missing mods fall back
to original files when the save can still be read; otherwise the game reports
the required mod and version. New saves also record the Expanded version and reject incompatible versions
with a message showing both version numbers.

### Configurable game presets

New games open the configuration screen with Standard settings. Adjust zombie types, infection,
corpses, evolution, bases, survival thresholds, and decay. **Other gameplay
options** opens the existing settings for population, spawn rates, events, and
other rules. **Load preset** offers **Standard**, **Corpses & Infection**,
**Vintage**, **Expanded**, and your saved configurations. The options screen
shows the loaded values. In the preset list, move with Up/Down and page through
the option preview with Left/Right. **Save as preset** stores a named configuration for
future games. On the configuration screen, hover over a setting or select it with
Up/Down to read its description. Page Up/Down move between pages of settings;
Left/Right change the selected value.
The chosen rules and settings are stored with the game save. Older saves keep
their original mode rules.

The **Expanded** preset enables claimable bases. Stand inside an enclosed
building and press **Ctrl+B** to preview its boundaries; press **Y** to claim
it. Hostile actors and undead block their rooms. Fortified passages can connect
clear buildings into one base. Living NPC leaders with followers can claim bases;
only the leader and their current followers share ownership. The player can
claim a base alone. Your base boundary is highlighted in
green on the minimap, and its district coordinate appears beside your name and
faction. While standing inside another group's base, your status shows
**FOREIGN BASE**; foreign bases are not marked on the minimap. A basement or
another level can join the same base when its claimable
area has a two-way stair connection to an already claimed area. Claim each
level separately with **Ctrl+B** and **Y**. Open subway tracks are not claimed
with a station room. Claiming an unconnected area releases every level of the
previous base; you can own one base at a time. A group leader's death also
releases every level of its base.

In your base, stand in a room and press **Ctrl+B**, then **F** to assign food
storage or **W** to assign weapon storage. In the follower order menu, press
**E** to send a follower for supplies. They take needed provisions from storage,
search their district and neighboring districts through map exits, and return
found food and weapons to the assigned rooms. Enable **Claimable bases** in
any preset to use these mechanics. If you move your base during a scavenging
trip, the follower brings collected supplies to the new base.
NPC residents also make supply trips for their own bases. They leave stored food
and weapons in place until they need them. Taking supplies from another group's
base is possible, but an awake owner who sees the theft becomes hostile to the
thief.

## Develop and test

```sh
docker build --target test .
bash tests/e2e.sh
sh tests/scenario.sh --list
sh tests/scenario.sh --bench-ai
```

Gameplay scenarios and how to run one are documented in
[docs/gameplay-scenarios.md](docs/gameplay-scenarios.md). See
[optimizations.md](optimizations.md) for a summary of completed changes and
[docs/performance.md](docs/performance.md) for their measurements,
[AGENTS.md](AGENTS.md) for contributor guidance and
[docs/save-format.md](docs/save-format.md) for save compatibility. GitHub
Actions runs the automated checks.
