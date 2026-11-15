# macOS release install QA — v0.2.1

Tested the actual GitHub release DMG built from
`bdac0a0bd4c71ae3d98b1bac32c0a150e3df6100` in the Apple Silicon Studio VM.

- Downloaded the DMG and published SHA256SUMS.txt; SHA-256 matched.
- `hdiutil verify` passed; mounted the image read-only.
- Confirmed the Applications shortcut points to /Applications and install notes exist.
- Copied the packaged ESP.app to /Applications with ditto, equivalent to Finder's
  bundle copy, then ejected the DMG. No prior installed app was overwritten.
- Installed `codesign --verify --deep --strict` passed; `lipo -archs` reports x86_64 and arm64.
- Bundled build-info.json reports version 0.2.1 and the source revision above.
- Ran the installed executable with `-batchmode -espReleaseSmoke`, **without**
  `-nographics`. Exit 0; ESP_PLANET_OK reported 12 resources; ESP_RELEASE_SMOKE_OK
  reported graphics=Metal, sky=material-binding, vehicles=3 ascent/reset/map passed.
- Started the installed bundle through Launch Services with `open` for playtesting.

## Important acceptance limit

`spctl --assess --type execute` rejected the installed app (exit 3). It is ad-hoc
signed, not Developer ID signed/notarized. The terminal download did not establish
the browser-quarantine first-launch path. Do not equate successful direct launch
or CI headless smoke with Gatekeeper acceptance or a zero-prompt install.
No quarantine attributes were removed and no security policy was disabled.

The install notes now describe Apple's per-app first-launch path for a trusted
release. Developer ID signing/notarization remains necessary to eliminate that
prompt. Intel execution and interactive Windows installation were not tested here.
