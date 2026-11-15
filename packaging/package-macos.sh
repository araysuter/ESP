#!/bin/bash
set -euo pipefail
version=${1:?release version required}
source_dir="$PWD/build/StandaloneOSX"
app="$source_dir/ESP.app"
test -d "$app/Contents"
exe=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Contents/Info.plist")
binary="$app/Contents/MacOS/$exe"
file "$binary"
architectures=$(lipo -archs "$binary")
for architecture in x86_64 arm64; do
    if [[ " $architectures " != *" $architecture "* ]]; then
        echo "ESP is missing the $architecture slice ($architectures)" >&2
        exit 1
    fi
done
# Bundling notices modifies the app; refresh its ad-hoc integrity signature.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
mkdir -p dist build
work=$(mktemp -d)
mountpoint="$work/mount"
mounted=false
cleanup() {
    if "$mounted"; then
        hdiutil detach "$mountpoint" >/dev/null || return
    fi
    rm -rf "$work"
}
trap cleanup EXIT

stage="$work/image"
image="$work/ESP-$version-macOS.dmg"
install_dir="$work/Applications"
mkdir -p "$stage" "$mountpoint" "$install_dir"
ditto "$app" "$stage/ESP.app"
ln -s /Applications "$stage/Applications"
cp packaging/install-macos.txt "$stage/Install ESP.txt"
hdiutil create -volname "ESP $version" -srcfolder "$stage" -format UDZO "$image"
hdiutil verify "$image"

# Exercise the same drag-to-Applications copy that users make in Finder.
hdiutil attach "$image" -readonly -nobrowse -mountpoint "$mountpoint"
mounted=true
test "$(readlink "$mountpoint/Applications")" = /Applications
test -f "$mountpoint/Install ESP.txt"
ditto "$mountpoint/ESP.app" "$install_dir/ESP.app"
hdiutil detach "$mountpoint"
mounted=false

installed_app="$install_dir/ESP.app"
installed_exe="$installed_app/Contents/MacOS/$exe"
test -x "$installed_exe"
test -f "$installed_app/Contents/Resources/Notices/LICENSE"
test -f "$installed_app/Contents/Resources/build-info.json"
codesign --verify --deep --strict "$installed_app"
smoke_log="$PWD/build/macos-smoke.log"
rm -f "$smoke_log"
python3 packaging/run-player-smoke.py "$installed_exe" "$smoke_log"
mv "$image" dist/
