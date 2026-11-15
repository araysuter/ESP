# UI layout audit

Baseline: `c293a0d3085612eda003c15ccdff80966b51e2d3`.

The stats panel was clamped using resolved layout dimensions without rejecting a
hidden panel or zero/nonfinite viewport and panel extents. Layout callbacks can
run while the menu hides the panel. Clamping then can overwrite the remembered
drag position before usable geometry returns. The clamp now waits for a visible
panel and finite positive extents, and rejects nonfinite pointer positions.

The existing opt-in IntegrationSmoke now checks a temporary window resize while
the panel is hidden, followed by returning to the original viewport and resuming.
The panel should retain its prior position when that position still fits.

Validation for this draft: source review and `git diff --check` passed. No Unity
editor, GUI automation, packaged player, or Studio job was started for this audit.
The added runtime assertion is unexecuted; prior packaged UI results predate this
patch and do not verify it. Re-run the existing opt-in UI smoke on an unlocked
authorized test machine before merging.
