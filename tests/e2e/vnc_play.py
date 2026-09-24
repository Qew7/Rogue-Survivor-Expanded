"""Drive a short game through the local VNC server in the test container."""

import os
import json
import re
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


def click(sock, x, y, button=1):
    sock.sendall(struct.pack(">BBHH", 5, button, x, y))
    time.sleep(0.06)
    sock.sendall(struct.pack(">BBHH", 5, 0, x, y))
    time.sleep(0.3)


def wait_for(path, seconds):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if os.path.exists(path) and os.path.getsize(path) > 100:
            return
        time.sleep(0.5)
    raise RuntimeError("Game did not create " + path)


def wait_for_log(path, text, seconds=10):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if os.path.exists(path) and text in open(path).read():
            return
        time.sleep(0.2)
    raise RuntimeError("Game log did not report " + text)


def read_saved_mods(path):
    with open(path, "rb") as saved:
        assert saved.read(5) == b"RSE1\x03", "Save has no mod manifest"
        count = struct.unpack("<I", saved.read(4))[0]

        def read_string():
            length = 0
            shift = 0
            while True:
                byte = saved.read(1)[0]
                length |= (byte & 0x7F) << shift
                if not byte & 0x80:
                    break
                shift += 7
            return saved.read(length).decode("utf-8")

        return [(read_string(), read_string()) for _ in range(count)]


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
    enter, down, up = 0xFF0D, 0xFF54, 0xFF52
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
    for _ in range(4):
        key(vnc, down)
    key(vnc, enter)  # Mods in the main menu.
    wait_for_log(log, "mod selection ready")
    key(vnc, 0x20)  # Enable Auxiliary.
    key(vnc, down)
    key(vnc, 0x20)  # Enable Deonapocalypse.
    key(vnc, 0xFF51)  # Move Deonapocalypse above Auxiliary.
    key(vnc, enter)
    wait_for_log(log, "selected mods: Deonapocalypse, Auxiliary")
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if "reloading mod resources done" in open(log).read():
            break
        time.sleep(0.2)
    else:
        raise RuntimeError("Selected mod resources were not loaded")
    profile = "/opt/game/Config/mod-profile.json"
    for _ in range(50):
        if os.path.exists(profile):
            break
        time.sleep(0.2)
    assert [mod["name"] for mod in json.load(open(profile))] == [
        "Deonapocalypse", "Auxiliary"
    ], "Menu profile did not retain selected mods and priority"
    for _ in range(4):
        key(vnc, up)
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

    time.sleep(1)  # Let the play loop begin reading input after its ready log.
    key(vnc, ord("m"))  # Mouse movement mode.
    for x, y in ((400, 400), (400, 360), (360, 400), (440, 400),
                 (360, 360), (440, 360), (400, 440), (336, 336)):
        click(vnc, x, y, 4)
        if "mouse context menu opened" in open(log).read():
            break
        time.sleep(0.2)
    else:
        raise RuntimeError("Right-click did not open the context menu")
    matches = re.findall(
        r"mouse context menu opened: target=(-?\d+),(-?\d+) player=(-?\d+),(-?\d+) actions=(\d+)",
        open(log).read(),
    )
    assert matches, "Right-click did not open the context menu"
    assert int(matches[-1][-1]) > 0, "Context menu has no actions"
    key(vnc, enter)  # Execute the first available action.
    wait_for_log(log, "mouse context action:")

    # Shift+S saves the real world graph, then Shift+L loads it again.
    shift = 0xFFE1
    vnc.sendall(struct.pack(">BBHI", 4, 1, 0, shift))
    key(vnc, ord("S"))
    vnc.sendall(struct.pack(">BBHI", 4, 0, 0, shift))
    save = "/opt/game/Config/Saves/save.dat"
    wait_for(save, 30)
    save_bytes = os.path.getsize(save)
    assert read_saved_mods(save) == [
        ("Deonapocalypse", "1.0.0"), ("Auxiliary", "")
    ], "Save did not retain mod names, versions and priority"
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
