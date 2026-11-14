using System;
using FlightCore;

internal static class Program
{
    private static readonly WeatherAtmosphere Offline = new WeatherAtmosphere(null, new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(3));
    private static StageDefinition[] TinyStages() => new[] {
        new StageDefinition(1000, 2, 100000, 100000, 200, 200, 0.5, 1),
        new StageDefinition(500, 1, 50000, 50000, 200, 200, 0.5, 1)
    };
    private static RocketSimulation Create(double step, StageDefinition[] stages) => new RocketSimulation(step, stages, Offline, 0.5, -1.0);
    private static int Main()
    {
        try { PadAndReset(); BurnAndStage(); Impact(); Convergence(30); Convergence(60); Console.WriteLine("Rocket tests passed."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void PadAndReset()
    {
        var rocket = Create(0.02, StageDefinition.DemoStages());
        var initial = rocket.State;
        rocket.Step(); Near(rocket.State.MissionTime, 0, 0, "uncommanded clock");
        rocket.Launch(); Run(rocket, 50);
        var held = rocket.State;
        Near(held.MissionTime, 1, 1e-12, "pad clock");
        Near(held.AltitudeMeters, 0, 1e-6, "pad altitude");
        Near(held.PropellantKg, initial.PropellantKg, 0, "pad fuel");
        if (held.IsLaunched || held.IsLanded) throw new Exception("Zero throttle should hold on pad");
        rocket.Throttle = 1; Run(rocket, 100);
        if (!rocket.State.IsLaunched || rocket.State.AltitudeMeters <= 0) throw new Exception("Offline launch did not lift off");
        rocket.Throttle = 0; double fuel = rocket.State.PropellantKg; Run(rocket, 50);
        Near(rocket.State.PropellantKg, fuel, 0, "zero throttle fuel");
        rocket.Reset(); var reset = rocket.State;
        Near(reset.MissionTime, 0, 0, "reset time"); Near(reset.PropellantKg, initial.PropellantKg, 0, "reset fuel");
        Near(reset.MassKg, initial.MassKg, 0, "reset mass"); Vector(reset.Position, initial.Position, 0, "reset position");
        Vector(reset.Velocity, initial.Velocity, 0, "reset velocity");
        if (reset.StageIndex != 0 || reset.IsLaunched || reset.IsLanded) throw new Exception("Reset flags");
    }
    private static void BurnAndStage()
    {
        var rocket = Create(0.02, TinyStages()); rocket.Throttle = 1; rocket.Launch();
        for (int i = 0; i < 200 && rocket.State.PropellantKg > 0; i++) rocket.Step();
        var before = rocket.State;
        Near(before.PropellantKg, 0, 0, "fuel cutoff");
        if (!before.IsLaunched || before.IsLanded) throw new Exception("Expected airborne burnout");
        Run(rocket, 5); var coast = rocket.State;
        Near(coast.PropellantKg, 0, 0, "fuel after cutoff");
        if (coast.SpeedRelativeGround >= before.SpeedRelativeGround) throw new Exception("Vehicle did not coast after fuel cutoff");
        if (!rocket.Stage()) throw new Exception("Stage separation failed");
        var after = rocket.State;
        Vector(after.Position, coast.Position, 0, "staging position"); Vector(after.Velocity, coast.Velocity, 0, "staging velocity");
        Near(after.MissionTime, coast.MissionTime, 0, "staging time");
        Near(after.MassKg, coast.MassKg - 1000, 1e-9, "discard dry mass once");
        Near(after.PropellantKg, 1, 0, "new stage fuel");
        if (after.StageIndex != 1 || rocket.Stage()) throw new Exception("Terminal stage should not separate");
        Near(rocket.State.MassKg, after.MassKg, 0, "terminal staging mass");
    }
    private static void Impact()
    {
        var rocket = Create(0.02, TinyStages()); rocket.Throttle = 1; rocket.Launch();
        for (int i = 0; i < 10000 && !rocket.State.IsLanded; i++) rocket.Step();
        var impact = rocket.State;
        if (!impact.IsLaunched || !impact.IsLanded || !impact.IsComplete) throw new Exception("Suborbital flight did not impact");
        Near(impact.AltitudeMeters, 0, 1e-5, "impact altitude");
        if (!(impact.MissionTime > 0) || !double.IsFinite(impact.MissionTime)) throw new Exception("Invalid impact time");
        rocket.Step(); Near(rocket.State.MissionTime, impact.MissionTime, 0, "clock after impact");
    }
    private static void Convergence(int seconds)
    {
        var coarse = Create(0.02, StageDefinition.DemoStages()); var fine = Create(0.01, StageDefinition.DemoStages());
        coarse.Throttle = fine.Throttle = 1; coarse.Launch(); fine.Launch();
        Run(coarse, seconds * 50); Run(fine, seconds * 100);
        var a = coarse.State; var b = fine.State;
        Near(a.MissionTime, seconds, 1e-8, "coarse mission time"); Near(b.MissionTime, seconds, 1e-8, "fine mission time");
        Vector(a.Position, b.Position, 0.1, "step convergence position"); Vector(a.Velocity, b.Velocity, 0.02, "step convergence velocity");
        Near(a.PropellantKg, b.PropellantKg, 0.1, "step convergence fuel");
        if (!a.IsLaunched || a.IsLanded || !double.IsFinite(a.DynamicPressurePascals)) throw new Exception("Invalid ascent state");
    }
    private static void Run(RocketSimulation rocket, int steps) { for (int i = 0; i < steps; i++) rocket.Step(); }
    private static void Near(double a, double b, double tolerance, string name)
    { if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a - b) > tolerance) throw new Exception($"{name}: {a:R} vs {b:R}, tolerance {tolerance:R}"); }
    private static void Vector(Vector3d a, Vector3d b, double tolerance, string name)
    { Near(a.X, b.X, tolerance, name + " X"); Near(a.Y, b.Y, tolerance, name + " Y"); Near(a.Z, b.Z, tolerance, name + " Z"); }
}
