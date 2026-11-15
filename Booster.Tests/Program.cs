using System;
using FlightCore;

internal static class Program
{
    const double G0 = 9.80665;
    static readonly WeatherAtmosphere Offline = new WeatherAtmosphere(null, new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(3));
    static StageDefinition[] Stages(double fuel = 100) => new[] {
        new StageDefinition(1000, fuel, 100000, 100000, 200, 200, 0, 1),
        new StageDefinition(200, 50, 50000, 50000, 200, 200, 0, 1)
    };
    static SolidBoosterDefinition Booster(double seconds = .35) => new SolidBoosterDefinition(100, 50000 * seconds / (200 * G0), 50000, 200);
    static RocketSimulation Create(double step, double fuel = 100, double burnSeconds = .35) => new RocketSimulation(step, Stages(fuel), Offline, .5, -1, 0, Booster(burnSeconds));
    static int Main()
    {
        try { Validation(); IndependentBurnAndReset(); ParallelConsumption(); BurnoutAndSeparation(); BothBurnoutsWithinOneStep(); PadHoldDown(); SlsGuidanceAscent(); Console.WriteLine("Solid booster tests passed."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Validation()
    {
        foreach (double invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
        {
            Throws(() => new SolidBoosterDefinition(invalid, 1, 1, 1));
            Throws(() => new SolidBoosterDefinition(1, invalid, 1, 1));
            Throws(() => new SolidBoosterDefinition(1, 1, invalid, 1));
            Throws(() => new SolidBoosterDefinition(1, 1, 1, invalid));
        }
        Near(Booster().BurnSeconds, .35, 1e-15, "declared burn duration");
    }
    static void IndependentBurnAndReset()
    {
        var r = Create(.02); var initial = r.State; double initialSolid = r.BoosterPropellantKg;
        if (!r.BoosterAttached || r.BoosterBurning || r.JettisonBoosters()) throw new Exception("Invalid ready-state booster flags");
        r.Step(); Near(r.BoosterPropellantKg, initialSolid, 0, "no ignition before launch");
        r.Throttle = 0; r.Launch(); Run(r, 5);
        if (!r.State.IsLaunched || !r.BoosterBurning) throw new Exception("Solid booster should lift off with core throttle zero");
        Near(r.BoosterPropellantKg, initialSolid - Booster().MassFlowKgPerSecond * .1, 1e-10, "independent solid flow");
        Near(r.State.PropellantKg, initial.PropellantKg, 0, "core remains shut down");
        if (r.JettisonBoosters()) throw new Exception("Burning booster jettison must be rejected");
        r.Reset();
        Near(r.BoosterPropellantKg, initialSolid, 0, "reset solid fuel"); Near(r.State.MassKg, initial.MassKg, 0, "reset mass");
        if (!r.BoosterAttached || r.BoosterBurning || r.State.IsLaunched) throw new Exception("Reset booster flags");
    }
    static void ParallelConsumption()
    {
        var r = Create(.02); var initial = r.State; r.Throttle = 1; r.Launch(); Run(r, 5);
        double coreUsed = 100000 * .1 / (200 * G0), solidUsed = 50000 * .1 / (200 * G0);
        Near(r.State.PropellantKg, initial.PropellantKg - coreUsed, 1e-10, "parallel core fuel");
        Near(r.BoosterPropellantKg, Booster().PropellantKg - solidUsed, 1e-10, "parallel booster fuel");
        Near(r.State.MassKg, initial.MassKg - coreUsed - solidUsed, 1e-10, "parallel mass accounting");
        var before = r.State; double solid = r.BoosterPropellantKg;
        if (r.Stage()) throw new Exception("Core stage must remain attached while boosters burn");
        var after = r.State;
        if (after.StageIndex != 0 || !r.BoosterBurning) throw new Exception("Rejected staging changed the stack");
        Near(after.MassKg, before.MassKg, 0, "rejected staging mass");
        Near(r.BoosterPropellantKg, solid, 0, "rejected staging booster fuel");
        Vector(before.Position, after.Position, 0, "rejected staging position"); Vector(before.Velocity, after.Velocity, 0, "rejected staging velocity");
    }
    static void BurnoutAndSeparation()
    {
        var r = Create(.02); r.Launch(); Run(r, 20);
        Near(r.BoosterPropellantKg, 0, 0, "solid cutoff");
        if (r.BoosterBurning || !r.BoosterAttached) throw new Exception("Spent dry mass must remain attached");
        var before = r.State;
        if (r.Stage() || r.State.StageIndex != 0) throw new Exception("Core stage must remain attached until spent boosters are jettisoned");
        Near(before.MassKg, 1000 + 100 + 200 + 50 + 100, 1e-10, "retained booster dry mass");
        if (!r.JettisonBoosters()) throw new Exception("Spent booster separation rejected");
        var after = r.State;
        Near(after.MassKg, before.MassKg - 100, 1e-10, "booster separation mass");
        Vector(after.Position, before.Position, 0, "booster separation position"); Vector(after.Velocity, before.Velocity, 0, "booster separation velocity");
        Near(after.MissionTime, before.MissionTime, 0, "booster separation time");
        if (r.JettisonBoosters() || r.BoosterAttached) throw new Exception("Booster separated more than once");
        Run(r, 2); Near(r.BoosterPropellantKg, 0, 0, "fuel remains zero");
        if (r.State.SpeedRelativeGround >= before.SpeedRelativeGround) throw new Exception("Spent boosters continued to accelerate the vehicle");
        before = r.State;
        if (!r.Stage()) throw new Exception("Core separation after booster jettison rejected");
        after = r.State;
        if (after.StageIndex != 1) throw new Exception("Core separation did not advance stage");
        Near(after.MassKg, before.MassKg - 1000 - before.PropellantKg, 1e-9, "core separation mass");
        Vector(before.Position, after.Position, 0, "core separation position"); Vector(before.Velocity, after.Velocity, 0, "core separation velocity");
    }
    static void BothBurnoutsWithinOneStep()
    {
        // Core cutoff at ~0.0392 s and booster cutoff at 0.15 s both occur inside the 0.2 s step.
        var coarse = Create(.2, 2, .15); var fine = Create(.005, 2, .15);
        coarse.Throttle = fine.Throttle = 1; coarse.Launch(); fine.Launch();
        coarse.Step(); Run(fine, 40);
        Near(coarse.State.PropellantKg, 0, 0, "coarse core cutoff"); Near(coarse.BoosterPropellantKg, 0, 0, "coarse solid cutoff");
        Near(fine.State.PropellantKg, 0, 0, "fine core cutoff"); Near(fine.BoosterPropellantKg, 0, 0, "fine solid cutoff");
        Near(coarse.State.MissionTime, .2, 1e-12, "whole step survives both events");
        Vector(coarse.State.Position, fine.State.Position, .005, "event split position convergence");
        Vector(coarse.State.Velocity, fine.State.Velocity, .005, "event split velocity convergence");
        Near(coarse.State.MassKg, fine.State.MassKg, 1e-10, "event split final mass");
    }
    static void PadHoldDown()
    {
        var weak = new SolidBoosterDefinition(100, 1, 1, 100);
        var r = new RocketSimulation(.02, Stages(), Offline, .5, -1, 0, weak);
        r.Launch(); r.Step();
        if (r.State.IsLaunched || !r.BoosterBurning) throw new Exception("Weak solid should burn while held down");
        Near(r.BoosterPropellantKg, 1 - .02 / (100 * G0), 1e-12, "pad solid fuel");
    }
    static void SlsGuidanceAscent()
    {
        foreach (var site in new[] { (28.627306, -80.620869), (25.997, -97.156) })
        foreach (double step in new[] { .02, .01 })
        {
            var r = new RocketSimulation(step, RealVehiclePresets.BuildSlsStages(), Offline,
                site.Item1 * Math.PI / 180, site.Item2 * Math.PI / 180, booster: RealVehiclePresets.BuildSlsBoosterParameters());
            var guidance = RealVehiclePresets.BuildSlsGuidance(); r.Launch();
            double peak = 0, maxDrop = 0, minimumOrbit = double.PositiveInfinity, minAfter80 = double.PositiveInfinity, maxQ = 0;
            bool passed80 = false;
            bool loggedUpper = false;
            bool sawBoostersOff = false, sawUpper = false;
            while (r.State.MissionTime < 2500)
            {
                guidance.Update(r); var state = r.State;
                if (!double.IsFinite(state.AltitudeMeters) || state.IsLanded)
                    throw new Exception($"SLS ascent landed or invalid at {state.MissionTime:F1}s alt={state.AltitudeMeters:F0}m");
                maxQ = Math.Max(maxQ, state.DynamicPressurePascals);
                passed80 |= state.AltitudeMeters >= 80000;
                if (passed80 && !guidance.HasCutOff) minAfter80 = Math.Min(minAfter80, state.AltitudeMeters);
                if (state.StageIndex == 1 && !loggedUpper)
                {
                    loggedUpper = true;
                    var orbitAtStage = OrbitDiagnostics.Compute(state.Position, state.Velocity);
                    var p = state.Position; var v = state.Velocity;
                    double radial = (p.X*v.X + p.Y*v.Y + p.Z*v.Z) / p.Magnitude();
                    Console.WriteLine($"SLS upper stage t={state.MissionTime:F1}s alt={state.AltitudeMeters:F0}m radial={radial:F1}m/s apoRadius={orbitAtStage.ApogeeRadiusMeters:F0}m periRadius={orbitAtStage.PerigeeRadiusMeters:F0}m");
                }
                sawBoostersOff |= !r.BoosterAttached;
                sawUpper |= state.StageIndex == 1;
                if (state.IsLaunched && !guidance.HasCutOff)
                {
                    peak = Math.Max(peak, state.AltitudeMeters);
                    maxDrop = Math.Max(maxDrop, peak - state.AltitudeMeters);
                }
                if (guidance.HasCutOff)
                {
                    var orbit = OrbitDiagnostics.Compute(state.Position, state.Velocity);
                    if (!orbit.IsBound || !double.IsFinite(orbit.MinimumWgs84AltitudeBoundMeters))
                        throw new Exception("SLS cutoff orbit invalid");
                    minimumOrbit = Math.Min(minimumOrbit, orbit.MinimumWgs84AltitudeBoundMeters);
                    if (state.MissionTime >= guidance.CutoffMissionTimeSeconds + 60)
                    {
                        if (!sawBoostersOff || !sawUpper || minimumOrbit < 180000 || maxDrop > 1000 || orbit.ApogeeRadiusMeters - EarthFrames.SemiMajorAxisMeters > 1000000)
                            throw new Exception($"SLS ascent/coast failed: drop={maxDrop:F0}m orbit={minimumOrbit:F0}m apogee={orbit.ApogeeRadiusMeters - EarthFrames.SemiMajorAxisMeters:F0}m stages={sawBoostersOff}/{sawUpper}");
                        Console.WriteLine($"SLS ascent site={site.Item1} step={step}: cutoff={guidance.CutoffMissionTimeSeconds:F1}s maxDrop={maxDrop:F0}m coastBound={minimumOrbit:F0}m apogeeRadius={orbit.ApogeeRadiusMeters:F0}m minAfter80km={minAfter80:F0}m maxQ={maxQ:F0}Pa");
                        break;
                    }
                }
                r.Step();
            }
            if (r.State.MissionTime >= 2500) throw new Exception($"SLS no 60s orbital coast; maxDrop={maxDrop:F0}m altitude={r.State.AltitudeMeters:F0}m fuel={r.State.PropellantKg:F0}kg");
        }
    }
    static void Run(RocketSimulation r, int n) { for (int i = 0; i < n; i++) r.Step(); }
    static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected invalid booster parameter rejection"); }
    static void Near(double a, double b, double tolerance, string name) { if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a-b)>tolerance) throw new Exception($"{name}: {a:R} vs {b:R}"); }
    static void Vector(Vector3d a, Vector3d b, double tolerance, string name) { Near(a.X,b.X,tolerance,name+" X");Near(a.Y,b.Y,tolerance,name+" Y");Near(a.Z,b.Z,tolerance,name+" Z"); }
}
