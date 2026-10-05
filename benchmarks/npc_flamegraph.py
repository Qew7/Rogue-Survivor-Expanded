#!/usr/bin/env python3
"""Render Mono's call report as a flamegraph of attributed self time."""

import re
import sys
from collections import defaultdict


ROOT = "NpcTurnBenchmarks:RunTurns ("
METHOD = re.compile(r"^\s*(\d+)\s+(\d+)\s+(\d+)\s+(.+)$")
CALLS = re.compile(r"^\t(\d+) calls from:$")


def escape(value):
    return (value.replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))


def name_short(name):
    name = name.split(" (", 1)[0]
    return name.replace("djack.RogueSurvivor.", "")


def color(name):
    if "RunTurns" in name:
        return "#394352"
    if "GetZonesAt" in name or "Zone:" in name or "Rectangle:Contains" in name:
        return "#77b58b"
    if "LOS:" in name or "LOSSensor:" in name or "FOV" in name:
        return "#e7a965"
    if ".AI." in name or "NpcIntent" in name or "NpcKnowledge" in name:
        return "#7d9fca"
    return "#afb6be"


def add(tree, stack, weight):
    node = tree
    for method in stack:
        node = node["children"].setdefault(method, {"name": method, "self": 0.0,
                                                      "total": 0.0, "children": {}})
        node["total"] += weight
    node["self"] += weight


def main(report_path, svg_path, folded_path):
    tree = {"children": {}}
    folded = defaultdict(float)
    root_name = None
    root_total = 0
    current = None
    traces = []
    trace_count = None
    trace_path = []

    def finish_trace():
        nonlocal trace_count, trace_path
        if trace_count is not None:
            traces.append((trace_count, trace_path))
        trace_count = None
        trace_path = []

    def finish_method():
        nonlocal root_name, root_total, current, traces
        finish_trace()
        if current is None:
            return
        total, self_ms, count, method = current
        if method.startswith(ROOT):
            root_name, root_total = method, total
            if self_ms:
                add(tree, [method], self_ms)
                folded[(method,)] += self_ms
        elif self_ms and traces:
            all_calls = sum(n for n, _ in traces)
            for n, path in traces:
                start = next((i for i, frame in enumerate(path)
                              if frame.startswith(ROOT)), None)
                if start is None:
                    continue
                stack = tuple(path[start:] + [method])
                weight = self_ms * n / all_calls
                add(tree, stack, weight)
                folded[stack] += weight
        current = None
        traces = []

    with open(report_path, encoding="utf-8", errors="replace") as report:
        for line in report:
            match = METHOD.match(line)
            if match and not line.startswith("\t"):
                finish_method()
                current = (int(match[1]), int(match[2]), int(match[3]), match[4].strip())
            elif current is not None:
                match = CALLS.match(line)
                if match:
                    finish_trace()
                    trace_count = int(match[1])
                elif trace_count is not None and line.startswith("\t\t"):
                    trace_path.append(line.strip())
        finish_method()

    if root_name is None or root_name not in tree["children"]:
        raise SystemExit("RunTurns was not found in Mono call report")
    root = tree["children"][root_name]
    missing = root_total - root["total"]
    if missing > 1:
        label = "Other / native / rounded"
        add(root, [label], missing)
        root["total"] += missing
        folded[(root_name, label)] += missing

    with open(folded_path, "w", encoding="utf-8") as output:
        for stack, ms in sorted(folded.items()):
            if ms > 0:
                output.write(";".join(name_short(frame) for frame in stack)
                             + " " + str(round(ms * 1000)) + "\n")

    width, left, top, row = 1600, 24, 108, 22
    plot_width = width - 2 * left
    scale = plot_width / root["total"]

    def depth(node):
        visible = [child for child in node["children"].values()
                   if child["total"] * scale >= 2]
        return 1 + max((depth(child) for child in visible), default=0)

    max_depth = depth(root)
    height = top + max_depth * row + 45
    shapes = []

    def draw(node, x, level, lineage):
        w = node["total"] * scale
        y = top + (max_depth - level - 1) * row
        label = name_short(node["name"])
        title = " → ".join(name_short(part) for part in lineage + [node["name"]])
        title += " | inclusive ≈ %.1f ms, self ≈ %.1f ms (under Mono profiler)" % (
            node["total"], node["self"])
        shapes.append('<g><title>%s</title><rect x="%.2f" y="%d" width="%.2f" '
                      'height="21" fill="%s" stroke="#fff" stroke-width="0.6"/>' % (
                          escape(title), x, y, max(w, 0.1), color(node["name"])))
        if w >= 45:
            chars = max(0, int((w - 10) / 7.2))
            shown = label if len(label) <= chars else label[:max(0, chars - 1)] + "…"
            shapes.append('<text x="%.2f" y="%d" font-size="12" '
                          'font-family="monospace" fill="#17202a">%s</text>' % (
                              x + 5, y + 15, escape(shown)))
        shapes.append("</g>")
        child_x = x
        children = sorted(node["children"].values(), key=lambda c: -c["total"])
        small = 0.0
        for child in children:
            if child["total"] * scale < 2:
                small += child["total"]
                continue
            draw(child, child_x, level + 1, lineage + [node["name"]])
            child_x += child["total"] * scale
        if small * scale >= 2:
            draw({"name": "Other small calls", "self": small, "total": small,
                  "children": {}}, child_x, level + 1, lineage + [node["name"]])

    draw(root, left, 0, [])
    with open(svg_path, "w", encoding="utf-8") as output:
        output.write('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" '
                     'viewBox="0 0 %d %d">\n' % (width, height, width, height))
        output.write('<rect width="100%" height="100%" fill="#fff"/>\n')
        output.write('<text x="24" y="35" font-size="23" font-family="sans-serif" '
                     'font-weight="600" fill="#17202a">NPC turn: managed call flamegraph</text>\n')
        output.write('<text x="24" y="59" font-size="13" font-family="sans-serif" '
                     'fill="#475569">8 turns · 1800 actions · width = attributed self time; '
                     'hover a block for its call path</text>\n')
        output.write('<text x="24" y="79" font-size="12" font-family="sans-serif" '
                     'fill="#475569">Mono method instrumentation greatly slows execution; '
                     'time split across multiple callers is estimated from call counts.</text>\n')
        output.write("\n".join(shapes))
        output.write('\n<text x="24" y="%d" font-size="12" font-family="sans-serif" '
                     'fill="#475569">Orange: FOV · green: zones · blue: AI · gray: other '
                     '· Mono total: %d ms · attributed: %.1f ms</text>\n</svg>\n' % (
                         height - 18, root_total, root["total"]))


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("usage: npc_flamegraph.py calls.txt flamegraph.svg flamegraph.folded")
    main(*sys.argv[1:])
