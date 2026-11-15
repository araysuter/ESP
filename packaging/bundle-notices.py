"""Bundle repository provenance alongside every distributable player."""
import json
import pathlib
import shutil
import sys

output = pathlib.Path(sys.argv[1])
version, commit = sys.argv[2:4]
if not output.is_dir():
    raise SystemExit(f"Missing player directory: {output}")
notices = output / "Notices"
notices.mkdir(exist_ok=True)
for filename in ("LICENSE", "LICENSE-SCOPE.md"):
    shutil.copy2(filename, notices / filename)
shutil.copytree("docs", notices / "docs", dirs_exist_ok=True)
shutil.copy2("data/solar-system-texture-manifest.json", notices / "solar-system-texture-manifest.json")
for path in pathlib.Path("UnityProject/Assets").rglob("*"):
    if path.is_file() and any(word in path.name.lower() for word in ("license", "notice", "attribution")) and path.suffix != ".meta":
        target = notices / path
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
(output / "build-info.json").write_text(json.dumps({"version": version, "commit": commit, "unity": "6000.6.9f1"}, indent=2) + "\n")
shutil.copy2("packaging/README.txt", output / "README.txt")

# Keep notices with the installed Mac app when users drag only ESP.app out of the DMG.
resources = output / "ESP.app/Contents/Resources"
if resources.is_dir():
    shutil.copytree(notices, resources / "Notices", dirs_exist_ok=True)
    shutil.copy2(output / "build-info.json", resources / "build-info.json")
    shutil.copy2(output / "README.txt", resources / "README.txt")
