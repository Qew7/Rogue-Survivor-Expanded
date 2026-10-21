---
name: rogue-export-social-graph
description: Export a Rogue Survivor .dat or .sav save from Docker into the automatically loaded Read Records social atlas. Use when asked to process a save for NPC relationships, events, plans, or reactions; not for save performance profiling.
---

# Export a save to the social atlas

The normal game save is inside Docker, in the `game-data` volume mounted at `/opt/game/Config`; its active file is usually `/opt/game/Config/Saves/save.dat`. Find the actual file there before exporting. Use the existing `tools/social-graph-export.sh` from the repository root. It builds the portable game in Docker and writes JSON with the saved `ResidentRecords` plus the current social catalog. Do not restart the game or rewrite the original save.

1. From the repository root, find the game container with `docker compose ps -q game`. Inspect `/opt/game/Config/Saves` through `docker exec`; list actual `.dat` and `.sav` files and exclude `hiscores.dat`. If the container is stopped, inspect the Compose `game-data` volume read-only instead of starting or restarting the game. Prefer the active `save.dat` unless the user named another save. If more than one plausible save remains, ask which one to use. Do not silently choose a migration backup or `.bak`.
2. Copy the chosen save out of Docker with `docker cp` into a temporary local directory. The copy avoids modifying the live volume and gives the exporter a normal filesystem path. If the user gave a local save path instead, use it directly.
3. Run `sh tools/social-graph-export.sh "$local_save_path" "$repo_root/docs/social-graph-save.js"` from the repository root. This `.js` export is loaded automatically by `docs/social-graph.html` on page refresh and is ignored by Git because it contains private save history. If the user also wants a standalone JSON, export it to their chosen path or a new filename under `~/Downloads`; do not overwrite an existing JSON. Calling the script with no arguments only refreshes the catalog, not the save. Remove only the temporary copied save after a successful export.
4. Verify the `.js` export starts with `window.SOCIAL_GRAPH_DATA=` and that its JSON payload has `schema == 1` and arrays `nodes`, `edges`, and `residents`. Report the resident count and whether `partial` is true. Do not print the residents' full records into chat.
5. Give the user a clickable link to `docs/social-graph.html` and tell them to refresh it. The saved NPCs should appear immediately without choosing a file. The graph links people by archived shared events; it does not contain current numerical `Feeling` or `Trust` values. See `docs/social-graph.md` for interpretation.

If Docker or archive parsing fails, report the actual error and leave the save untouched. Do not fall back to a full world restore or fabricate missing history.
