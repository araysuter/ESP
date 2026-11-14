# Shared Unity editor

## Connection

Use the private connection sheet supplied by the coordinator. It contains the
HTTPS endpoint, a per-person bearer key, and its expiry. Transport is Streamable
HTTP, endpoint path `/mcp`, header `Authorization: Bearer <your-key>`.
Never commit connection sheets or paste keys into prompts, issues, or logs.

The bridge currently controls the qualification project, not the game project.
The coordinator must explicitly switch the bridge project when the game project
is ready; opening another editor does not switch the bridge automatically.

Verified on November 14: public HTTPS rejects absent/incorrect keys with 401;
authenticated discovery returns 199 tools and `editor_status` succeeds. The
transport was tested with FastMCP's HTTP client. Actual Codex and Cursor client
configuration remains to be tested on teammates' machines.

## Ownership

Multiple sessions control the same editor. Before any mutation, claim ownership
with the team. Only one contributor may edit the shared scene/project at a time.
Use separate branches/worktrees for independent code work.

At handoff record the owner, project, branch/commit, active scene, unsaved changes,
running tasks, and next owner. Save changes and stop Play Mode before handing off.
Infrastructure qualification has finished; no contributor currently holds a
game-editing lease. This document is a protocol, not an automatic lock.

## Access lifecycle

The initial coordinator and partner keys expire November 15, 2026 at 12:00 noon
America/Detroit (17:00 UTC). Key expiry does not delete the tunnel or stop the VM.
The coordinator can revoke an individual key by removing its hash record from
the private auth file; verification reloads that file on requests.

After submission, stop the dedicated tunnel and bridge LaunchAgents and remove
the project DNS route/tunnel. Preserve project source before shutting down the VM.
Do not stop unrelated host tunnels or jobs. No automatic VM shutdown is installed.

Blender is installed, but its MCP connection and Blender-to-Unity asset import
are not yet qualified. Do not use the Unity endpoint as a Blender endpoint.
