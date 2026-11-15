# Downloadable builds

The Downloadable app builds workflow runs on pushes to main and manual dispatch. Each successful pair of builds creates a versioned GitHub prerelease containing ESP-macOS.dmg and ESP-Windows-Setup.exe. The installer bundles the Windows executable and its required data files. Failed builds retain logs and preserve previous releases.

## Required setup

Register dedicated self-hosted macOS and Windows runners with the additional label `esp-unity`. Install and activate Unity 6000.6.9f1 under the runner account, with the corresponding desktop build module. Set `UNITY_EDITOR` in each runner's environment to its editor executable. Install Git LFS on both machines and Inno Setup 6 on Windows, with ISCC.exe on PATH. Restart the runner service after environment changes. Use runner-owned checkouts distinct from interactive Unity projects. Review host storage and available resources before installing anything.

The workflow uses the activated runner account's license. No account passwords or activation material belong in the repository. Builds run only from main or manually dispatched trusted refs. Restrict access to these runners accordingly.

Packages are unsigned. Apple signing/notarization and Windows code signing require separate credentials and configuration. These packages have not been generated or exercised by this workflow yet. macOS architecture follows the project's build configuration. Verify the actual produced architecture and launch behavior on each target OS before announcing support.

Download releases from the repository's Releases page. Every successful main update gets a new prerelease named with its commit, retaining earlier packages for rollback. This automation updates the downloadable builds; installed copies are updated by downloading and installing a newer package.
