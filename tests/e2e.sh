#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
project="rogue-refactor-e2e-$$"
port="$(python3 - <<'PY'
import socket
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    print(sock.getsockname()[1])
PY
)"
export ROGUE_PORT="$port"

cleanup() {
    if [[ "$?" -ne 0 ]]; then docker compose -p "$project" logs --tail=80 game >&2 || true; fi
    docker compose -p "$project" down -v >/dev/null
}
trap cleanup EXIT

docker compose -p "$project" up --build -d
container="$(docker compose -p "$project" ps -q game)"
for attempt in {1..60}; do
    status="$(docker inspect --format '{{.State.Health.Status}}' "$container")"
    if [[ "$status" == healthy ]]; then break; fi
    if [[ "$status" == unhealthy ]]; then
        docker compose -p "$project" logs --tail=80 game
        exit 1
    fi
    sleep 1
done
[[ "$status" == healthy ]]

curl --fail --silent --show-error "http://127.0.0.1:$port/" | grep -F 'vnc.html' >/dev/null
docker compose -p "$project" exec -T game test -s /opt/game/Config/setup.dat
# Add a second, empty mod before the first-run confirmation opens mod selection.
docker compose -p "$project" exec -T -u root game mkdir /opt/game/mods/Auxiliary

python3 - "$port" <<'PY'
import socket
import sys

with socket.create_connection(('127.0.0.1', int(sys.argv[1])), timeout=5) as sock:
    request = (
        'GET /websockify HTTP/1.1\r\n'
        'Host: 127.0.0.1\r\n'
        'Upgrade: websocket\r\n'
        'Connection: Upgrade\r\n'
        'Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n'
        'Sec-WebSocket-Version: 13\r\n'
        'Sec-WebSocket-Protocol: binary\r\n\r\n'
    )
    sock.sendall(request.encode('ascii'))
    response = sock.recv(4096)
    if b'101 Switching Protocols' not in response:
        raise SystemExit('VNC WebSocket handshake failed: ' + repr(response))
print('Game startup, configuration, HTTP and VNC WebSocket checks passed')
PY

docker compose -p "$project" exec -T game python3 - < tests/e2e/vnc_play.py
docker compose -p "$project" logs game | grep -F 'loading images done' >/dev/null
