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
        try { Validation(); IndependentBurnAndReset(); ParallelConsumption(); BurnoutAndSeparation(); BothBurnoutsWithinOneStep(); PadHoldDown(); Console.WriteLine("Solid booster tests passed."); return 0; }
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
        if (!r.Stage()) throw new Exception("Core stage separation rejected");
        var after = r.State;
        Vector(before.Position, after.Position, 0, "core staging position"); Vector(before.Velocity, after.Velocity, 0, "core staging velocity");
        Near(after.MassKg, before.MassKg - 1000 - before.PropellantKg, 1e-9, "core staging discards only core stage");
        Near(r.BoosterPropellantKg, solid, 0, "core staging preserves booster fuel");
        if (!r.BoosterBurning) throw new Exception("Core staging interrupted solid burn");
    }
    static void BurnoutAndSeparation()
    {
        var r = Create(.02); r.Launch(); Run(r, 20);
        Near(r.BoosterPropellantKg, 0, 0, "solid cutoff");
        if (r.BoosterBurning || !r.BoosterAttached) throw new Exception("Spent dry mass must remain attached");
        var before = r.State;
        Near(before.MassKg, 1000 + 100 + 200 + 50 + 100, 1e-10, "retained booster dry mass");
        if (!r.JettisonBoosters()) throw new Exception("Spent booster separation rejected");
        var after = r.State;
        Near(after.MassKg, before.MassKg - 100, 1e-10, "booster separation mass");
        Vector(after.Position, before.Position, 0, "booster separation position"); Vector(after.Velocity, before.Velocity, 0, "booster separation velocity");
        Near(after.MissionTime, before.MissionTime, 0, "booster separation time");
        if (r.JettisonBoosters() || r.BoosterAttached) throw new Exception("Booster separated more than once");
        Run(r, 2); Near(r.BoosterPropellantKg, 0, 0, "fuel remains zero");
        if (r.State.SpeedRelativeGround >= before.SpeedRelativeGround) throw new Exception("Spent boosters continued to accelerate the vehicle");
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
    static void Run(RocketSimulation r, int n) { for (int i = 0; i < n; i++) r.Step(); }
    static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected invalid booster parameter rejection"); }
    static void Near(double a, double b, double tolerance, string name) { if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a-b)>tolerance) throw new Exception($"{name}: {a:R} vs {b:R}"); }
    static void Vector(Vector3d a, Vector3d b, double tolerance, string name) { Near(a.X,b.X,tolerance,name+" X");Near(a.Y,b.Y,tolerance,name+" Y");Near(a.Z,b.Z,tolerance,name+" Z"); }
}
