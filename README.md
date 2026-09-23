This is the latest version of roguedjack's game Rogue Survivor, I would have just forked it, but he did not upload the latest version of his source code onto GitHub. He even said directly on one of his repos, containing an older version of the source code, to download it from his blog.

I plan on expanding this game for fun to learn C#.

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
Linux build uses GDI+ and has no sound. The original Windows project and source
files are not changed: `docker/build.rb` creates a Linux project inside the
Docker build and `docker/prepare.rb` applies the compatibility edits there.

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
