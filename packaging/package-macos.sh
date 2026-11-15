#!/bin/bash
set -euo pipefail
version=${1:?release version required}
source_dir="$PWD/build/StandaloneOSX"
app="$source_dir/ESP.app"
test -d "$app/Contents"
exe=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Contents/Info.plist")
file "$app/Contents/MacOS/$exe"
lipo -verify_arch x86_64 arm64 "$app/Contents/MacOS/$exe"
# Bundling notices modifies the app; refresh its ad-hoc integrity signature.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
mkdir -p dist build
# Test the same release player that will be inside the DMG, with a bounded run.
python3 packaging/run-player-smoke.py "$app/Contents/MacOS/$exe" "$PWD/build/macos-smoke.log"
ln -s /Applications "$source_dir/Applications"
hdiutil create -volname "ESP $version" -srcfolder "$source_dir" -ov -format UDZO "dist/ESP-$version-macOS.dmg"
hdiutil verify "dist/ESP-$version-macOS.dmg"
mountpoint=$(mktemp -d)
hdiutil attach "dist/ESP-$version-macOS.dmg" -readonly -nobrowse -mountpoint "$mountpoint"
trap 'hdiutil detach "$mountpoint" >/dev/null || true; rmdir "$mountpoint" || true' EXIT
test -x "$mountpoint/ESP.app/Contents/MacOS/$exe"
test -f "$mountpoint/Notices/LICENSE"
