"""Drive a short game through the local VNC server in the test container."""

import os
import socket
import struct
import time


def read_exact(sock, size):
    data = b""
    while len(data) < size:
        chunk = sock.recv(size - len(data))
        if not chunk:
            raise RuntimeError("VNC connection closed")
        data += chunk
    return data


def key(sock, symbol):
    for pressed in (1, 0):
        sock.sendall(struct.pack(">BBHI", 4, pressed, 0, symbol))
        time.sleep(0.04)
    time.sleep(0.2)


def wait_for(path, seconds):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if os.path.exists(path) and os.path.getsize(path) > 100:
            return
        time.sleep(0.5)
    raise RuntimeError("Game did not create " + path)


with socket.create_connection(("127.0.0.1", 5900), timeout=10) as vnc:
    assert read_exact(vnc, 12).startswith(b"RFB 003.")
    vnc.sendall(b"RFB 003.008\n")
    methods = read_exact(vnc, 1)[0]
    assert 1 in read_exact(vnc, methods), "VNC has no unauthenticated connection"
    vnc.sendall(b"\x01")
    assert read_exact(vnc, 4) == b"\x00" * 4
    vnc.sendall(b"\x01")
    header = read_exact(vnc, 24)
    read_exact(vnc, struct.unpack(">I", header[20:24])[0])

    # Focus the game window and acknowledge first-run setup. Wait for the menu
    # frame before choosing a character, since initialization time varies.
    vnc.sendall(struct.pack(">BBHH", 5, 1, 500, 300))
    vnc.sendall(struct.pack(">BBHH", 5, 0, 500, 300))
    enter, down = 0xFF0D, 0xFF54
    log = "/opt/game/Config/log.txt"
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if os.path.exists(log) and "directory setup ready for confirmation" in open(log).read():
            break
        time.sleep(0.2)
    else:
        raise RuntimeError("First-run setup did not appear")
    key(vnc, enter)
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if "main menu ready" in open(log).read():
            break
        time.sleep(0.2)
    else:
        raise RuntimeError("Main menu did not appear")
    for symbol in (enter, enter, down, enter, down, enter, down, enter, ord("y")):
        key(vnc, symbol)

    # World generation can take several seconds on a cold CI worker.
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        if os.path.exists(log) and "new game ready" in open(log).read():
            break
        key(vnc, enter)  # Acknowledge the advisor and welcome screens.
        time.sleep(1)
    else:
        tail = open(log).read().splitlines()[-12:] if os.path.exists(log) else []
        raise RuntimeError("World generation or welcome screens did not finish: " + repr(tail))

    # Shift+S saves the real world graph, then Shift+L loads it again.
    shift = 0xFFE1
    vnc.sendall(struct.pack(">BBHI", 4, 1, 0, shift))
    key(vnc, ord("S"))
    vnc.sendall(struct.pack(">BBHI", 4, 0, 0, shift))
    save = "/opt/game/Config/Saves/save.dat"
    wait_for(save, 30)
    save_bytes = os.path.getsize(save)
    vnc.sendall(struct.pack(">BBHI", 4, 1, 0, shift))
    key(vnc, ord("L"))
    vnc.sendall(struct.pack(">BBHI", 4, 0, 0, shift))
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if "game load ready" in open(log).read():
            break
        time.sleep(0.5)
    else:
        raise RuntimeError("Saved game did not load: " + repr(open(log).read().splitlines()[-18:]))

print("VNC input, new game, save and load passed ({} bytes)".format(save_bytes))
