# ESP contributor instructions

## Scope and planning

- Build an original Earth-scale rocket sandbox for Hack The Nest. Read `docs/implementation-plan.md` and `hack-the-nest-master-prompt.md` before implementation.
- Unity + URP is confirmed. Use Blender for assets and a pure C# flight core. The master prompt's Unreal-specific engine, plugin, C++, Blueprint, UMG and Niagara instructions are superseded.
- The hackathon has already started, as confirmed by the user. Prioritize infrastructure qualification and the smallest playable mission. Draft is due at midnight ending November 14, 2026; final submission is due November 15, 2026 at noon. Use America/Detroit (EST) unless corrected; aim to submit tonight or early morning. Do not restart the clock.
- Keep unresolved decisions explicit. Do not claim a tool, package, platform, endpoint, or trajectory works until checked.

## Git and collaboration

- Use the GitHub plugin for commits, pushes, and pull requests, not the GitHub CLI.
- On implementation tasks, commit each coherent, verified set of changes to the assigned branch as you go. Review the staged diff before each commit, include only files that belong to the task, and report the commit hashes and checks run. Follow any explicit task instruction about when to commit, push, or open a pull request.
- Inspect Git status before edits and preserve others' work. Use `asher/codex/` for new branches unless directed otherwise.
- Give concurrent contributors separate branches/worktrees. Never open one Unity project directory in multiple editor processes.
- Assign one owner at a time to each shared Unity editor, scene/prefab, and Blender file. Record a handoff before another contributor mutates it. Multiple MCP connections do not provide edit isolation.
- Commit Unity `.meta` files alongside their assets. Use visible metadata and text asset serialization. Exclude generated caches, local credentials, and builds from Git. Establish Git LFS rules before adding large binary assets.

## Shared compute

- Keep machine addresses, usernames, private host paths, credentials, and personal machine guidance out of repository documentation. Use placeholders in team setup examples.
- Preserve unrelated host jobs. Do not reboot, resize/restart the VM, or stop workloads as an incidental setup step.
- Before large installs/downloads, verify host free space and actual cache/disk destinations, including host backup exclusions. Guest logical disk capacity is not available physical host capacity.
- User has authorized excluding only the project VM from Time Machine and keeping project/Git inside it for simplicity. Do not copy to NAS or create separate host backups. Preserve source and assets in the VM; local Git is not an independent backup. Use the GitHub plugin for authorized remote commits/pushes.
- Bind development control services to loopback. The user prefers temporary Cloudflare Tunnel access with authentication instead of requiring teammates to join Tailscale. Protect every published control endpoint before exposing it, and verify unauthenticated requests are rejected. Never expose the Blender command socket directly to the public internet. Verify the chosen MCP transport and client compatibility before promising team access.
- Pin installed engine, Blender, package, and MCP revisions after qualification. Keep account activation and credentials outside Git.

## Simulation and assets

- Preserve the master prompt's SI units, full-scale Earth, WGS84 placement, double-precision authoritative flight state, fixed simulation step, and explicit inertial/Earth-fixed/local frame conventions.
- Keep the flight core independent of rendering and network services. For Unity, prefer a pure C# library with a thin Unity adapter, subject to the implementation plan.
- Use local camera-relative coordinates for rendering; never use float scene transforms as authoritative orbital state.
- Validate gravity/orbit, frame transforms, atmosphere samples, fuel cutoff, staging continuity, step convergence, successful ascent, and failed suborbital flight.
- Separate imagery from terrain/collision data. Record data provenance, license, attribution, and approximations. Bundle an offline visual fallback.
- Demo checkpoints must originate from verified simulation runs and be labeled. Keep true elapsed mission time and time rate visible.
- Export Blender assets explicitly with documented units, axes, pivots, and materials; test their Unity import. Preserve editable source assets.

## Delivery

- Qualify the VM with a rendered scene and packaged smoke test before building the game around it.
- Verify packages on actual target operating systems and report untested targets honestly.
- Reserve the last three hackathon hours for packaging, offline rehearsal, and submission once the actual kickoff/deadline is confirmed.
