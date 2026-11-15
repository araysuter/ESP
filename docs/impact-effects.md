# Crash effects

Destructive ground impacts use `RocketSimulation.IsDestroyed`, with the existing
10 m/s surface-relative impact-speed threshold and crash-destruction cheat.
This threshold is a gameplay rule, not a structural failure prediction. Terrain
contact remains the flight core's reference ellipsoid/pad surface; satellite
imagery does not add collision geometry.

`RocketExplosion` observes the first destruction transition of each simulation.
The driver arms it before the first simulation step. It hides the intact vehicle's
renderers and draws one original, bounded effect: a flash, rolling fire, smoke,
dust and sparks. Remaining attached propellant influences visual size and fire;
no chemical reaction, blast pressure, fragmentation damage or secondary explosion
is simulated. Water impacts currently use the same effect, without a water plume.
The effect is not a physically calibrated detonation model.

The effect uses 209 procedural quads, one mesh and one material, and expires after
12 unpaused real seconds. Pause freezes it; map view hides it. Mission restart
clears it, restores captured renderer flags and does not replay it. The impact
notice includes a restart button. Gentle landings and disabled crash destruction
do not trigger it.

## Packaged verification

Run a development player with `-espExplosionSmoke -espVehicle 1` to exercise real
Starship flights, not injected impact states. The fixture performs a 12-second
vertical burn and cutoff, checks one effect, pause, expiry and restart, repeats
with crash destruction disabled, then performs a gentle 0.2-second hop. It logs
`ESP_EXPLOSION_SMOKE_OK` and captures flash/fire/smoke/reset screenshots.

The macOS VM run passed: destructive impact approximately 103 m/s, damage-disabled
impact approximately 103 m/s, gentle touchdown approximately 1.8 m/s. Visual review
confirmed the fire and restart action. Other platforms remain unverified.
