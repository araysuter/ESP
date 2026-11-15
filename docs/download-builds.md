# Downloadable releases

`Downloadable app builds` uses standard GitHub-hosted runners only. No self-hosted runner or connection to a teammate's machine is needed. The public repository's standard hosted runner execution is free under GitHub's public-repository policy.

## Release v0.1.0 and later

From Actions → Downloadable app builds → Run workflow, select `main`, enter a version such as `0.1.0`, and enable `publish`. The workflow builds and tests both packages before creating the matching `v0.1.0` release. Leave publish disabled for a rehearsal with downloadable Actions artifacts only. Alternatively, publishing a GitHub Release with a `vMAJOR.MINOR.PATCH` tag automatically builds that exact revision and attaches the packages when checks pass. Release commits must be on main. Existing tags pointing to another commit are never moved.

Each release includes:

- `ESP-VERSION-macOS.dmg`, containing ESP.app and an Applications shortcut.
- `ESP-VERSION-Windows-Setup.exe`, a per-user installer without administrator requirements.
- `ESP-VERSION-Windows.zip`, the complete portable player (extract all files before running ESP.exe).
- `SHA256SUMS.txt` for the packages.

Player version, filenames, installer version and tag use the same version. Packages include `build-info.json` with the exact source commit, plus the project's license and asset attribution documents. The existing development-player path remains available for interactive work.

## Unity authentication

Add `UNITY_EMAIL` and `UNITY_PASSWORD` as repository Actions secrets. They must be a Unity account with an eligible Personal entitlement. Do not commit credentials or paste them into issues/chat. GameCI unity-builder v6.0.0 with CLI v0.1.75 supports account-based Personal activation; no legacy `.ulf` / `UNITY_LICENSE` is required. Studio's UnityEntitlementLicense.xml is not a portable replacement for that file.

Build jobs run sequentially, reducing simultaneous activation requirements; GameCI returns the temporary seat after the build. An account challenge, unavailable seat, or authentication failure must be resolved in Unity before CI can succeed. Do not disable account protections as an automatic workaround. The setup is not verified merely because the secrets exist: check the actual activation and build logs.

## Build and package checks

Unity 6000.6.9f1 is pinned in ProjectVersion.txt. Git LFS assets are hydrated and checked. The macOS player is built on `macos-15-intel`; Windows Mono is cross-built on `ubuntu-24.04`, then packaged and tested on `windows-2025`. All are standard hosted labels. Separate per-platform Library caches avoid mixing imported targets.

`BuildMission.Build` accepts `-espRelease -espVersion VERSION -espBuildPath PATH` and generates the scene and detailed prefabs before building. Relative output paths resolve against the repository root. Command-line parameters work both inside GameCI's Linux container and on the native macOS runner; custom environment variables are not assumed to cross a container boundary.

The opt-in `-espReleaseSmoke` player probe checks scene components, all three vehicle meshes, actual ascent above 100 m, reset and the offline sky. It is disabled during normal play. macOS runs the release player headlessly before making and verifying the DMG. Windows silently installs the actual installer and runs its installed executable in headless mode. A missing marker, crash, nonzero exit or timeout fails packaging. Only successful packages reach the publishing job; logs remain as Actions artifacts.

The packages are not Apple Developer ID notarized or Windows Authenticode signed. Graphical acceptance of the downloaded macOS release runs separately on Studio. Native graphics and usability on Windows still need testing on an actual Windows PC: a headless CI smoke is not a graphical playtest. Existing Studio visual tests likewise do not substitute for testing the downloaded release build. No auto-updater is bundled; download and install a later release to update.
