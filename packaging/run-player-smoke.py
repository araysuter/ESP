"""Bounded startup/ascent smoke test for the actual release executable."""
import pathlib
import subprocess
import sys

exe, logfile = sys.argv[1:3]
args = [exe, "-batchmode", "-espReleaseSmoke", "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0", "-logFile", logfile]
# Standard hosted runners do not promise a graphics device.
# Graphical acceptance is performed on the downloaded Mac player on Studio.
args.append("-nographics")
try:
    result = subprocess.run(args, timeout=120, check=False)
except subprocess.TimeoutExpired:
    raise SystemExit("Player smoke timed out after 120 seconds")
log = pathlib.Path(logfile).read_text(errors="replace") if pathlib.Path(logfile).exists() else ""
if result.returncode != 0 or "ESP_RELEASE_SMOKE_OK" not in log or "ESP_RELEASE_SMOKE_FAIL" in log:
    print(log[-12000:])
    raise SystemExit(f"Release player smoke failed (exit {result.returncode})")
print(next(line for line in log.splitlines() if "ESP_RELEASE_SMOKE_OK" in line))
