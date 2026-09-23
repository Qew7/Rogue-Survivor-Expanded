#!/bin/bash
set -euo pipefail
pids=()
cleanup() {
    trap - EXIT TERM INT
    if ((${#pids[@]})); then
        kill "${pids[@]}" 2>/dev/null || true
        wait "${pids[@]}" 2>/dev/null || true
    fi
}
trap cleanup EXIT
trap 'exit 0' TERM INT

Xvfb "$DISPLAY" -screen 0 1280x800x24 -nolisten tcp &
pids+=("$!")
ready=false
for attempt in {1..100}; do
    if xdpyinfo -display "$DISPLAY" >/dev/null 2>&1; then ready=true; break; fi
    sleep 0.1
done
if ! "$ready"; then echo "Virtual display failed to start" >&2; exit 1; fi
openbox --sm-disable &
pids+=("$!")
x11vnc -display "$DISPLAY" -localhost -rfbport 5900 -forever -shared -nopw -noxdamage &
pids+=("$!")
websockify --web=/usr/share/novnc 6080 localhost:5900 &
pids+=("$!")
mono RogueSurvivor.exe &
pids+=("$!")

# Stop the whole container if the game or one of its display services exits.
wait -n "${pids[@]}"
