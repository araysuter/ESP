# Assisted SLS parking-orbit ascent

The previous altitude-only 120 km/cubic pitch schedule tipped over too early. A standard-atmosphere rollout from Kennedy rose to 104 km, fell to 27 km around T+420 s, then recovered. Reaching the perigee success threshold alone did not establish an acceptable ascent.

SLS now has a dedicated guidance factory. Below 30 km it uses a gravity-turn schedule. Above that, vertical-speed feedback and gravity/centripetal feedforward command the existing finite-rate pitch controller. After solid-booster separation, core engine cutoff and normal stage separation occur once horizontal speed is at least 7200 m/s and predicted apogee reaches 240 km; residual core fuel is discarded with the core. The upper stage coasts toward apogee before latching its circularization burn, then cuts off when conservative WGS84 perigee reaches 200 km. Flight position, velocity and time are always produced by the existing fixed-step physics; guidance changes only pitch, throttle and stage commands.

This is a representative Earth parking-orbit controller for the game's estimated SLS mass/thrust model, not NASA's flight guidance or an Artemis lunar-injection mission. Starship and the ESP demo vehicle retain their prior guidance.

## Physics validation

On the remote development machine, standard-atmosphere runs at Kennedy and South Texas, using both 20 ms and 10 ms steps, passed stage separation, zero measured altitude drawdown before insertion, a 60-second unpowered coast, perigee above 180 km and apogee below 1000 km. The Kennedy 20 ms run separated the core at T+444.6 s / 165.8 km, cut off the upper stage at T+909.6 s, and coasted in an approximately 200 x 321 km orbit. The 10 ms result had closely matching timing and orbital bounds. Vehicle regression tests also passed Starship and SLS at all three sites and both step sizes.

`Booster.Tests` checks ascent drawdown and the upper apogee bound to reject both the former atmospheric dive and a misleading high-energy elliptical insertion. Packaged verification uses `-espVisualPolishSmoke`; final rendered-flight results must be recorded separately from the pure-core tests.
