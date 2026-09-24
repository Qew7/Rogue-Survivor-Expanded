This is the latest version of roguedjack's game Rogue Survivor, I would have just forked it, but he did not upload the latest version of his source code onto GitHub. He even said directly on one of his repos, containing an older version of the source code, to download it from his blog.

I plan on expanding this game for fun to learn C#.

## New Features

### Mouse movement

During play, press **M** to turn mouse movement on or off. When it is on,
hover over a visible map tile to see the route arrow and step count, then
left-click to walk there. The route avoids blocked tiles and stops if the
way becomes blocked or the player takes damage while walking. Click an adjacent
enemy or object to perform the same bump action as a directional movement key;
an orange arrow marks such targets. Shift+M still opens the message log.

## Run on macOS or Linux

Install Docker with Compose, then run from this repository's root:

```sh
docker compose up --build -d
```

Open [http://localhost:6080](http://localhost:6080) in a browser and click the
game window to give it keyboard focus. On the first launch, press Enter to
confirm creation of the game directories. The initial build downloads the
compiler and Linux packages; later launches can use `docker compose up -d`.

The container builds the bundled `WRogue` sources with Mono and displays the
game through noVNC. Docker chooses the host architecture automatically. The
portable build uses GDI+ and has no sound. `docker/build.rb` creates a portable
project from the original project; platform differences live behind `PORTABLE`
compile conditions in the shared sources. File paths use the host platform's
separators; the build does not rewrite C# sources or require `MONO_IOMAP`.

The Windows project builds on .NET Framework 4.8 with GDI+ and no sound. Its
DirectX and SFML binaries were absent from the repository, so the supported
Windows build uses the same renderer as the container. CI compiles the Windows
project and runs the Linux game and test suite.

On Windows with Visual Studio's .NET Framework 4.8 build tools, build
`RogueSurvivor.sln` in `Release|Any CPU` and run
`bin/Release/RogueSurvivor.exe` from the `WRogue` directory so relative
resource paths resolve. The old Config setup utility is excluded from the
solution because the portable build selects GDI+ automatically.

Save in the game with Shift+S before stopping the container. Saves, settings,
and logs are kept in the Docker volume `game-data`, which survives rebuilds and
`docker compose down`. `docker compose down -v` deletes that volume and its
saved games. Closing the browser tab only disconnects the viewer; exiting the
game stops the container.

```sh
docker compose down                 # stop
docker compose ps                   # status
docker compose logs --tail=100 game # diagnostics
```

If port 6080 is already in use, run `ROGUE_PORT=6081 docker compose up -d` and
open `http://localhost:6081`. The browser endpoint is bound to localhost; the
VNC port is not exposed. All browser tabs control the same game session.

This setup was previously checked on macOS ARM64 with Docker Desktop, including
game startup, input, saving, and loading. A separate Linux host and AMD64 build
have not yet been checked.

## Working on the large game classes

`RogueGame`, `BaseAI`, `Rules`, `BaseTownGenerator`, and `GameItems` are split into
partial classes by responsibility. Start in each class's main `.cs` file for
shared state, then open the named parts for actions, rules, AI behaviors, town
locations, or item models. All parts are listed explicitly in
`WRogue/RogueSurvivor.csproj` for the original Windows build and the generated
Linux build. Every C# source file is kept at or below 1500 lines; the test stage
checks this limit and project entries. Player input, manual navigation,
overlays, and background simulation have separate components with focused
tests; `RogueGame` coordinates them.

Run the regression checks after changing these classes:

```sh
docker build --target test .
bash tests/e2e.sh
```

For a fast, named game situation, run `sh tests/scenario.sh --list` and then
`sh tests/scenario.sh movement/wall` (or `--all`). Scenarios use fixed seeds,
exercise game actions without the UI, and show the map when an assertion fails.
See [the scenario guide](docs/gameplay-scenarios.md) for adding player, NPC,
and map cases. The suite has a separate scenario for every skill and covers
combat, factions, followers, doors, items, survival, weather, generation, and
map exits and saving.
Test sources are grouped in `tests/unit`, `tests/integration`, and
`tests/scenarios`; each gameplay scenario has its own file in
`tests/scenarios/cases`, grouped by game system. Living and undead skills have
separate directories. Shared helpers live in `tests/support`, and the VNC
driver is in `tests/e2e`. The commands above remain the entry points.

Project-specific agent skills for adding skills, weather, factions, actors,
items, objects, AI behavior, locations, actions, combat rules, survival rules,
and world events live in `.agents/skills/`. `AGENTS.md` points agents to these
workflows and the required scenario tests.

The unit suites cover rules, AI, generation, stable content IDs, command
bindings, input, movement, and save and load behavior. The end-to-end script
starts an isolated game container, creates a character through VNC, saves and
loads the game, and checks the browser endpoint. It removes its test volume
afterward. GitHub Actions runs both commands on pushes and pull requests.
`.editorconfig` records whitespace rules; `ruby tests/layout.rb` checks file
sizes, project entries, and explicit content IDs without Docker.

Binary saves have a version marker and an automatically retained `.bak` copy.
New saves use a compressed object graph format that preserves cycles and shared
references. Existing unmarked saves and version 1 saves still load through the
legacy `BinaryFormatter` reader, then migrate when saved again. The new format
records private field names, so changes to serialized classes still need a
compatibility test; see [the format notes](docs/save-format.md). The simulation
worker stops cooperatively before save and load; deterministic random state is
kept in the saved session.
