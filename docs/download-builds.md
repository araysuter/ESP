# Downloadable releases

`Downloadable app builds` uses standard GitHub-hosted runners only. No self-hosted runner or connection to a teammate's machine is needed. The public repository's standard hosted runner execution is free under GitHub's public-repository policy.

## Release v0.1.0 and later

From Actions → Downloadable app builds → Run workflow, select a branch, enter a version such as `0.1.0`, choose `windows` to rehearse only Windows, and leave `publish` disabled. The `ESP-Windows` artifact contains the installer and portable ZIP if the Windows package checks succeed. A Windows-only run does not depend on the macOS build. Choose `macos` for a macOS-only rehearsal, or `both` for the complete rehearsal.

To publish, select `main`, choose `both`, and enable `publish`. The workflow builds and tests both packages before creating the matching `v0.1.0` release. Alternatively, publishing a GitHub Release with a `vMAJOR.MINOR.PATCH` tag automatically builds that exact revision for both platforms and attaches the packages when checks pass. Release commits must be on main. Existing tags pointing to another commit are never moved.

Each release includes:

- `ESP-VERSION-macOS.dmg`, containing ESP.app, an Applications shortcut, and a short install guide.
- `ESP-VERSION-Windows-Setup.exe`, a per-user installer without administrator requirements.
- `ESP-VERSION-Windows.zip`, the complete portable player (extract all files before running ESP.exe).
- `SHA256SUMS.txt` for the packages.

Player version, filenames, installer version and tag use the same version. Packages include `build-info.json` with the exact source commit, plus the project's license and asset attribution documents. The existing development-player path remains available for interactive work.

## Unity authentication

Add `UNITY_EMAIL` and `UNITY_PASSWORD` as repository Actions secrets. They must be a Unity account with an eligible Personal entitlement. Do not commit credentials or paste them into issues/chat. GameCI unity-builder v6.0.0 with CLI v0.1.75 supports account-based Personal activation; no legacy `.ulf` / `UNITY_LICENSE` is required. Studio's UnityEntitlementLicense.xml is not a portable replacement for that file.

The macOS and Windows builds run concurrently with eligible Personal activation; GameCI returns each temporary entitlement after its build. Independent branches, platforms and release versions can also build concurrently. Publications of the same version are serialized to prevent conflicting uploads. Rehearsals of the same branch and platform share a concurrency group; GitHub may replace an older pending run when another is queued, but an active run is not canceled. An account challenge, unavailable seat, or authentication failure must be resolved in Unity before CI can succeed. Do not disable account protections as an automatic workaround. The setup is not verified merely because the secrets exist: check the actual activation and build logs.

## Build and package checks

Unity 6000.6.9f1 is pinned in ProjectVersion.txt. Git LFS assets are hydrated and checked. The macOS player is built on `macos-15-intel`; Windows Mono is cross-built on `ubuntu-24.04`, then packaged and tested on `windows-2025`. All are standard hosted labels. Separate per-platform Library caches avoid mixing imported targets. macOS and Windows compile concurrently in independent jobs. The Windows installer starts as soon as the Windows player is ready, without waiting for macOS; publication still requires both platforms and the installer to pass. Imported assets and shader caches are saved immediately after a successful Unity build, before packaging, so a later package or smoke-test failure does not discard them. The pinned macOS Unity editor installation has a separate exact-version/architecture/module cache; Hub account state and credentials are excluded. Hosted cache storage limits and eviction can force a cold build. Already compressed DMGs, player archives, installers and ZIPs are uploaded without an extra compression pass. These changes shorten the build dependency chain and reuse completed imports; actual speedup depends on cache availability and hosted runner queue times.

`BuildMission.Build` accepts `-espRelease -espVersion VERSION -espBuildPath PATH` and generates the scene and detailed prefabs before building. Relative output paths resolve against the repository root. Command-line parameters work both inside GameCI's Linux container and on the native macOS runner; custom environment variables are not assumed to cross a container boundary.

The opt-in `-espReleaseSmoke` player probe checks scene components, all three vehicle meshes, actual ascent above 100 m, reset and the offline sky. It is disabled during normal play. macOS mounts the finished DMG, checks the Applications shortcut, copies ESP.app to a temporary Applications folder, then ejects the image and verifies and runs the installed copy. Windows silently installs the actual installer and runs its installed executable in headless mode. A missing marker, crash, nonzero exit or timeout fails packaging. Only successful packages reach the publishing job; logs remain as Actions artifacts.

The packages are not Apple Developer ID notarized or Windows Authenticode signed. Graphical acceptance of the downloaded macOS release runs separately on Studio. Native graphics and usability on Windows still need testing on an actual Windows PC: a headless CI smoke is not a graphical playtest. Existing Studio visual tests likewise do not substitute for testing the downloaded release build. No auto-updater is bundled; download and install a later release to update.
