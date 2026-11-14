# ESP implementation plan

Status: active hackathon, November 14, 2026. Unity + URP infrastructure and macOS ARM packaging are qualified. The map/launch UI is on main; gravity/orbit, atmosphere and the new two-stage propulsion/staging/assisted-ascent core are merged into flight-core through PR #10. All .NET 9 CI suites pass. Both launch sites and a historical NASA GEOS weather profile reach a conservative 200 km orbit and coast for 60 seconds in the headless mission tests at 20 ms and 10 ms steps. The integrated macOS ARM player has completed a NASA-weather pad-to-orbit, map and site-reset rehearsal; see `mission-integration.md` for the current integration contract. Earlier setup observations below are historical, not the current installed state.

Draft due midnight ending November 14; final due November 15 at noon America/Detroit. Reserve the final three hours for packaging and submission. No Windows build has been qualified.

## Current team starting point

Use Unity 6000.6.9f1 + URP, Unity CLI/Pipeline 1.0.0, and Blender 5.2.3 LTS. Read `shared-editor.md` before editor access. The later qualification sections supersede historical setup observations below. Partner client setup, Blender MCP/import, and game-project creation remain pending.

Start independent branches for flight core/tests, Unity gameplay integration, and assets/data. Build one two-stage launch-to-orbit mission first. Never edit the shared editor concurrently.

## Direction

Create an original single-player rocket sandbox: modify a two-stage rocket, launch from a recognizable location on a realistic Earth, stage, reach a verified orbit, and reset. The user has selected Unity + URP, Blender, and shared VM compute. The older master prompt specifies Unreal; retain its gameplay and numerical requirements with Unity now confirmed. Do not silently preserve Unreal-specific C++, Blueprint, UMG, Niagara, or packaging instructions in the Unity implementation.

Proposed stack: a pinned stable Unity LTS release with URP; pure C# double-precision flight library; thin Unity presentation layer; Blender for original assets; local NASA globe imagery first. Select exact versions after compatibility and licensing checks. Unity 6.3 LTS is the candidate release line; the exact patch is not pinned or installed yet. Cesium for Unity is optional only after inspecting the actual imagery format and proving that it reduces work.

## Observed infrastructure

- Host access and guest command execution have both been verified through existing authenticated host access and VMPal CLI.
- Host and guest are Apple Silicon macOS. Guest applications inspected did not include Unity or Blender.
- GPU/rendering, Unity activation, editor operation, packaging, remote desktop, and partner access remain unverified.
- Host runs unrelated workloads. Guest allocation and installer budget need headroom review before changes.
- User authorized excluding the project VM bundle from Time Machine; the exclusion is applied and verified. Keep project/Git inside the VM; no NAS copies or separate host backup, per user preference. Local Git history inside that VM is not an independent backup; remote pushes remain a separate action.
- Host free space is the limiting storage budget; do not rely on the much larger guest-reported logical free space.

Private connection details deliberately live outside this repository.

## Access architecture

1. Bootstrap using existing host access → VMPal CLI → guest command execution. Verified calls to VMPal's MCP tools through `vmpal tool`: VM status and desktop observation. Guest command execution works; administrator execution is not yet enabled. A separate native remote MCP entry in Codex has not been configured.
2. Use temporary Cloudflare Tunnel access for teammates, as requested. Proposed route: Codex/Cursor → HTTPS + Cloudflare Access service token → tunnel → loopback MCP endpoint. Give each person a separate revocable credential. Cloudflare account/domain and dashboard access are verified; confirm each client's custom-header support. Do not publish an unprotected endpoint while testing.
3. Keep one integration Unity editor and one Blender asset-authoring instance initially. Three human roles: flight core/tests; engine integration/gameplay; assets/data/demo. Code contributors use separate workspaces. One explicit owner at a time mutates each shared editor, with saved state and a written handoff.
4. Blender's addon socket is not an HTTP MCP endpoint. It needs its MCP bridge and a qualified HTTP transport adapter for Cloudflare access; do not route the raw Python-command socket to a public hostname. Timebox adapter setup and retain coordinator-operated Blender as a fallback.
5. Keep VMPal administration on the existing coordinator route initially. Shared engine/asset endpoints do not require sharing host-wide VM administration with the team. Human remote desktop is a separate capability to qualify if needed.
6. At submission, export source/assets/builds, verify the backup, revoke team credentials, stop the temporary tunnel, and then shut down the project VM as requested. Do not stop unrelated host services. Do not claim this teardown is scheduled until an actual mechanism is installed.

MCP candidates, not installed or qualified:

- [CoplayDev Unity MCP](https://github.com/CoplayDev/unity-mcp): evaluate a pinned release and its supported HTTP/stdio transport against each teammate's client. Verify session targeting, reconnects, and editor reload behavior.
- [MCP for Blender](https://github.com/ahujasid/mcp-for-blender): local MCP bridge plus Blender addon. The addon socket can execute Python and has no authentication/encryption; retain loopback binding and use an authenticated tunnel. Pin the bridge and addon together. Prefer disabling optional external asset services and telemetry unless the team chooses them.

## Qualification sequence

1. Use confirmed Unity + URP and deadlines. Resolve target platforms, Devpost rules, imagery endpoints, account activation owner and backup plan.
2. Review available resources while preserving host workloads. Document installation locations, downloads, caches, expandable VM disk, and working headroom. Verify exclusions before downloads.
3. Install Unity Hub, the selected Apple Silicon editor and required build modules; activate with the responsible user's account. Install a matching supported Blender release and isolated MCP dependencies.
4. Create a tiny Unity scene, compile a script, enter Play Mode, render it, and build/run a macOS player. Record renderer, logs, actual frame behavior, and build result. Unity's published system requirements are a baseline, not proof of VM compatibility.
5. Open Blender, create an original test mesh, save/reopen it, export and import it into Unity with correct scale/materials. Test viewport and a small render.
6. Add MCPs one at a time. Prove read, reversible mutation, save, reconnect, and target isolation. Repeat from one partner's computer. Document generic client setup without credentials.
7. If VM graphics or licensing blocks progress, decide whether to use native editors on an authorized machine with the VM for code/tests/services. Timebox this investigation before kickoff; do not spend the hackathon debugging virtualization indefinitely.

## Game architecture and milestones

Proposed repository areas: `UnityProject/`, `FlightCore/`, `FlightCore.Tests/`, `AssetsSource/`, `data/`, and `docs/`. Finalize how the pure C# core is consumed by Unity before scaffolding. Avoid duplicate copies of flight logic.

- Flight core: inertial double-precision state, Earth-fixed and local transforms, sourced gravity/atmosphere, propellant/mass/staging, finite steering, orbit prediction and impact. Select numeric tolerances before relying on results.
- Presentation: camera-relative rendering, WGS84 globe/pad, chase and orbit views, original rocket meshes, readable telemetry and controls.
- Builder: small part catalog, working two-stage preset, one engine/tank swap, mass/TWR/delta-v preview.
- Data: inspect supplied NASA/MSGC service, document coverage/resolution/CRS/terms, bundle permitted fallback imagery. A generic NASA API key alone does not establish a suitable Earth imagery source.
- Demo: real assisted pad-to-orbit mission using normal controls; failed suborbital example; labeled checkpoints from the verified run; offline launch and reset.

Original 24-hour milestone budget below is a reference only. Kickoff has passed; compress it against the confirmed remaining time and reserve the final three hours. Do not treat hour 0 as now:

| Window | Result |
| --- | --- |
| Hours 0–1 | Toolchain and ownership confirmed; tiny scene compiles and runs; engine/platform go/no-go |
| Hours 1–3 | Packaged smoke test on required platforms; headless flight checks started |
| Hours 3–8 | Pad, rocket, manual controls, staging; headless assisted ascent reaches verified orbit |
| Hours 8–15 | Flight/presentation integration, part swap, telemetry, orbit view, reset |
| Hours 15–21 | Package/profile, polish three camera shots, capture verified demo checkpoints |
| Hours 21–24 | Feature freeze, offline rehearsal, blocker fixes, submission |

Cut Moon, effects, freeform building, and live imagery before the core launch-to-orbit demonstration. Keep the master prompt's numerical checks and honest demo labeling. Required platforms and build owners remain unresolved; do not assume macOS and Windows are still mandatory until confirmed.

## Open decisions

- Unity + URP confirmed; exact editor and Blender versions still needed.
- Kickoff and deadlines confirmed by user; exact Devpost rules and demo/submission requirements still needed.
- Team size, computer OSes, AI clients, private network access, simultaneous editor needs.
- Unity activation/license owner and required build platforms/test hardware.
- Exact NASA/MSGC endpoints or supplied files; credentials supplied privately if needed.
- Scope changes to the master prompt, visual priorities, and launch location.
- Resource budget, VM storage exclusions, durable source/asset backup destination.

## References

- [Original master prompt](../hack-the-nest-master-prompt.md)
- [Unity system requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html)
- MCP upstream repositories linked above; recheck release-specific instructions during installation.

## Engine comparison and recommendation

| Criterion | Unity | Unreal |
| --- | --- | --- |
| MCP | Community CoplayDev integration; documented HTTP remote hosting | Epic built-in MCP in documented 5.8 release; experimental, local-only design and no authentication |
| Flight code | Proposed pure C# core and thin engine adapter | Portable C++ core and engine adapter, matching original prompt |
| This hackathon | Recommended: URP, small scenes, simple data-driven parts and UI | Consider if a teammate already has a working project/toolchain and substantial familiarity |
| VM status | Not installed or graphically qualified | Not installed or graphically qualified |

Recommendation is an engineering judgment about this project, not a measured engine performance comparison. Unity is confirmed; pin a stable supported patch after checking requirements. Do not install both engines merely to compare them during the hackathon.

Sources: [Unity MCP server documentation](https://github.com/CoplayDev/unity-mcp/blob/beta/Server/README.md), [Epic Unreal MCP 5.8 documentation](https://dev.epicgames.com/documentation/unreal-engine/unreal-mcp-in-unreal-editor?application_version=5.8), [Cloudflare Access service tokens](https://developers.cloudflare.com/cloudflare-one/access-controls/service-credentials/service-tokens/). Remote-hosted Unity mode may require a key-validation service; evaluate its exact pinned configuration before selecting the authentication layout. Cloudflare edge authentication and Unity session authentication are distinct.

## Confirmed team and scope

- Three humans; reported tools: two $20/month Codex plans, one $100/month Codex plan, and one $20/month Cursor plan. These describe user-reported access, not verified usage limits or API credits.
- Primary shared Studio plus another teammate's Studio used for local AI. No work on the second Studio is authorized by this plan.
- Preserve the proposed Earth launch, staging, manual/assisted controls and orbit demo.
- Candidate rocket presets: Starship, Saturn V, SLS. Ship one working two-stage preset first; additional vehicles are stretch catalog entries. These vehicles have different stage/booster layouts, so do not relabel one physics preset as three accurate rockets.
- Allow tank size, thrust and delta-v exploration. Clearly distinguish real specifications from player-modified or estimated values.
- Find licensed online models to save time; inspect redistribution terms, attribution, polygon count, textures, pivots and stage separability before adoption. Preserve source URLs and licenses. Placeholder geometry is acceptable while physics and data work proceed.
- NASA/MSGC imagery/API links still pending. Judging OS, Devpost event link, activation owner and backup arrangement remain needed. Deadline, Unity selection and Cloudflare account/domain are confirmed.

## Confirmed delivery and infrastructure decisions

- Unity + URP selected; one working rocket first.
- Draft deadline: 2026-11-15 00:00 America/Detroit (midnight ending Saturday November 14).
- Final deadline: 2026-11-15 12:00 America/Detroit. Aim for tonight or early morning; preserve 09:00–12:00 for final verification/submission at the latest.
- Submission is via Devpost; exact event URL and judging hardware remain unknown. Do not promise a specific required platform until rules are checked.
- Temporary Cloudflare deployment is authorized on the user's specified domain/account. Keep account identity, credentials and machine connection details outside this repository. Use a dedicated project subdomain and tunnel; preserve existing DNS/apps.
- Next gates: Cloudflare authentication, Unity activation, and rendered/package smoke test.

## Setup checkpoint

- Cloudflare dashboard sign-in succeeded and the requested domain was verified in the intended account. No tunnel, DNS, Access policy or credential was created.
- No standard local Cloudflare CLI credential files were found in the checked locations. Dashboard access does not imply API authentication.
- Host free space rechecked at approximately 322 GiB. Guest has no selected Apple developer toolchain and no installed Unity/Blender.
- Storage decision: exclude only the project VM bundle (applied and verified). User explicitly chooses project and Git inside the VM without NAS or separate host copies.
- With exclusion verified, budget roughly 60–100 GiB for editor/tools/cache/build working space (planning allowance, not measured installer size), and stop before exhausting host headroom.
- Activate Unity through the responsible human's account; never record account passwords or license secrets in this repository.
- First qualification: blank URP project → compile → Play Mode → standalone macOS build → launch. Then Blender mesh → export → Unity import. Remote team access follows local MCP validation.
- Proposed temporary endpoints use separate Unity and Blender subdomains under the authorized domain, an ESP-only tunnel and per-person credentials. No endpoint is live yet. Test missing/wrong credentials, valid MCP initialization, reconnect and editor ownership before team use.

- User will sign into Unity Hub when it is ready; Devpost link will follow when available.

## Installed tools and integration correction

- Unity Hub 4.0.0 Apple Silicon installed inside the guest; installer SHA-256 and app code signature verified. Opened successfully to the license agreement; user is handling agreement/sign-in/activation.
- Prefer the official Unity CLI with Unity Pipeline and its supported `unity mcp` mode. Current Unity documentation says the old in-editor AI Assistant MCP is deprecated, while CLI MCP remains supported and does not require a Unity AI subscription. The community MCP is no longer the first implementation choice.
- Source: https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli
- Official CLI supports Unity 6.0 LTS or later for editor control. Its local Pipeline listener stays loopback-only; shared Cloudflare transport is still to be qualified, not assumed.
- Preserve the user's live Unity sign-in session; do not take GUI control during handoff.

- Official Unity CLI 1.0.0 installed in the guest; checksum verified, version/help commands pass. Its MCP transport is stdio, so an HTTP adapter is required before Cloudflare publication. Editor-side Pipeline connection remains untested until editor/project setup.

- Blender 5.2.3 LTS installed inside the guest; installer checksum and app signature verified. Headless factory-startup/Python check passed (`ESP_BLENDER_OK 5.2.3 LTS`). GUI, rendering and Unity asset import still pending.

## Qualification results — November 14

- User requested 36 GiB VM RAM. VM shut down cleanly and restarted; guest reports 38,654,705,664 bytes (36 GiB).
- User's Hub install selected Unity 6000.6.9f1 arm64 with Web support. Use this version rather than downloading a second editor. Unity Personal activation verified.
- Created an isolated URP blank qualification project. C# smoke scene and standalone macOS Development build succeeded (171,793,720 bytes reported by build).
- Packaged app launched with Metal on Apple Paravirtual device, advanced to frame 90 in 3.05 seconds, and saved a screenshot showing the lit cube. This is basic viability evidence, not a game performance benchmark or offline acceptance test.
- Optional Gaussian/Bokeh depth-of-field and Panini post-processing shaders were unavailable/stripped. Avoid those effects until specifically qualified.
- Official Unity Pipeline 1.0.0 added to qualification project; live editor control is being checked. Cloudflare sharing remains unconfigured.

## Live editor and MCP qualification

- Editor 6000.6.9f1 reports ready; official Pipeline 1.0.0 successfully returned editor status and scene hierarchy.
- Official `unity mcp` initialization and tool discovery passed: server `unity-mcp` 1.0.0, 199 tools.
- A loopback HTTP bridge is running inside the guest. Missing/wrong bearer tokens return 401. Valid-token discovery returns 199 tools; `editor_status` call succeeds through HTTP.
- Bridge dependencies pinned to FastMCP 2.14.5 and MCP SDK 1.30.0. SDK 2.x expects response fields absent from Unity's current server; do not upgrade this pair without repeating protocol tests.
- Token verification uses private hash records with expiry; the raw coordinator token remains in a mode-0600 private file outside the repository. No credentials are in source. A dedicated user LaunchAgent owns the bridge.
- Proposed external endpoint: a dedicated project Unity hostname on the authorized domain. No Cloudflare tunnel or DNS route has been created yet. Repeat authentication and tool-call tests from outside the VM before giving teammates access.
- Multiple MCP sessions still share one Unity editor. They are not independent editing workspaces; keep an explicit active owner.

## Cloudflare qualification — current checkpoint

This checkpoint supersedes earlier statements that the tunnel is unconfigured.

- With user approval, created a dedicated project tunnel and Unity DNS route.
  The guest connector is cloudflared 2026.11.0, installed from Cloudflare's official
  release, running as a dedicated user LaunchAgent with a private token file.
- Cloudflare reports a healthy connector. The route targets only the loopback
  authenticated HTTP bridge; the raw Unity Pipeline port is not published.
- Public HTTPS checks: missing and wrong bearer keys return 401; valid-key MCP
  discovery returns 199 tools and `editor_status` succeeds.
- Coordinator and per-partner keys expire at the submission deadline. Credentials
  and account-specific endpoint details remain outside Git. See
  `docs/shared-editor.md` for handoff and shutdown instructions.
- Remaining gates: teammates' actual Codex/Cursor configurations, Blender MCP,
  Blender export/import qualification, and game-project creation. The current
  endpoint controls the qualification project only.
