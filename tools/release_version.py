#!/usr/bin/env python3
"""Update and check every published copy of the game's version."""

import argparse
import os
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VERSION_RE = re.compile(r"\d+\.\d+\.\d+")
COPIES = {
    "WRogue/SetupConfig.cs": [
        r'public const string GAME_VERSION = "(?P<version>\d+\.\d+\.\d+)";'
    ],
    "WRogue/Properties/AssemblyInfo.cs": [
        r'\[assembly: AssemblyVersion\("(?P<version>\d+\.\d+\.\d+)\.0"\)\]',
        r'\[assembly: AssemblyFileVersion\("(?P<version>\d+\.\d+\.\d+)\.0"\)\]',
    ],
    "WRogue/mods/Deonapocalypse/authors.json": [
        r'"game_version": "(?P<version>\d+\.\d+\.\d+)"'
    ],
    "README.md": [
        r'Current Expanded version: \*\*(?P<version>\d+\.\d+\.\d+)\*\*',
        r'The `game_version` value must name this version \(`(?P<version>\d+\.\d+\.\d+)`\)',
    ],
    "docs/save-format.md": [
        r'Version 4 saves record the Rogue Survivor Expanded version \(`(?P<version>\d+\.\d+\.\d+)` at'
    ],
}


def valid_version(value):
    if not VERSION_RE.fullmatch(value) or any(int(part) > 65535 for part in value.split(".")):
        raise ValueError("version must be three numeric components from 0 to 65535")
    return value


def updated_copy(path, patterns, version, check):
    original = path.read_text(encoding="utf-8")
    updated = original
    for pattern in patterns:
        matches = list(re.finditer(pattern, updated))
        if len(matches) != 1:
            raise ValueError(f"{path}: expected exactly one match for {pattern!r}")
        match = matches[0]
        if check and match.group("version") != version:
            raise ValueError(f"{path}: found {match.group('version')}, expected {version}")
        start, end = match.span("version")
        updated = updated[:start] + version + updated[end:]
    return updated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version", nargs="?", help="new game and release version, such as 0.2.1")
    parser.add_argument("--check", action="store_true", help="verify copies against VERSION")
    args = parser.parse_args()
    if (args.check and args.version) or (not args.check and not args.version):
        parser.error("use either --check or a new version")

    version_file = ROOT / "VERSION"
    version = valid_version(version_file.read_text(encoding="utf-8").strip()) if args.check else valid_version(args.version)
    updates = {}
    for relative, patterns in COPIES.items():
        updates[ROOT / relative] = updated_copy(ROOT / relative, patterns, version, args.check)
    if not args.check:
        for path, content in updates.items():
            if path.read_text(encoding="utf-8") != content:
                path.write_text(content, encoding="utf-8")
        version_file.write_text(version + "\n", encoding="utf-8")

    tag = os.environ.get("GITHUB_REF_NAME", "") if os.environ.get("GITHUB_REF_TYPE") == "tag" else ""
    if args.check and tag.startswith("v") and tag != "v" + version:
        raise ValueError(f"release tag {tag} does not match VERSION {version}")
    print(f"Version {version}: {'all copies match' if args.check else 'release files updated'}")


if __name__ == "__main__":
    try:
        main()
    except ValueError as error:
        print(f"release_version: {error}", file=sys.stderr)
        raise SystemExit(1)
