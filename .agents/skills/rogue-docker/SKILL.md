---
name: rogue-docker
description: Run Rogue Survivor scenarios, tests, UI e2e, and benchmarks through Docker. Use when executing Docker-backed checks in this repository.
---

# Docker checks

Run from the repository root:

- Named scenario: `sh tests/scenario.sh <name>`.
- Full test suite: `docker build --progress=plain --target test .`.
- Startup, UI, input, rendering, or asset changes: `bash tests/e2e.sh`. This script uses an isolated Compose project.
- Benchmarks: follow the matching `rogue-bench-*` skill.

Request elevated tool access when launching a Docker-backed command. Wait for authorization and for that command to finish; do not start another copy while either is pending. Diagnose Docker only if the command actually fails after it starts.

The scripts show Docker build steps and phase labels after authorization. Approval review happens before the scripts start, so it has no build progress or ETA.
