using System;
using FlightCore;

internal static class Program
{
    const double G0 = 9.80665;
    const double Omega = Aerodynamics.EarthAngularVelocityRadiansPerSecond;
    static readonly WeatherAtmosphere Offline = new WeatherAtmosphere(null,
        new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(3));
    static StageDefinition[] Stages(double fuel = 100, double thrust = 100000, double drag = 0) => new[] {
        new StageDefinition(1000, fuel, thrust, thrust, 200, 200, drag, 1),
        new StageDefinition(200, 50, 50000, 60000, 200, 220, drag, 1)
    };
    static RocketSimulation Create(double step = .02, double fuel = 100, double thrust = 100000,
        double drag = 0, SolidBoosterDefinition? booster = null) =>
        new RocketSimulation(step, Stages(fuel, thrust, drag), Offline, 0, 0, 0, booster);
    static SolidBoosterDefinition Booster(double duration = .01, double thrust = 50000) =>
        new SolidBoosterDefinition(100, thrust * duration / (200 * G0), thrust, 200);

    static int Main()
    {
        try
        {
            DefaultsAndStickyReset(); GravityValidation(); GravityScalingAndLiftoff(); ZeroGravityFinite();
            InfiniteCoreAndNoRefill(); InfiniteSolidAndNoRefill(); InfiniteParallelAndHoldDown();
            ImpactToggle(); GentleImpact(); TelemetryForceAndMassConsistency();
            Console.WriteLine("Cheats and authoritative telemetry tests passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void DefaultsAndStickyReset()
    {
        var r = Create(); var initial = r.State;
        if (r.GravityMultiplier != 1 || r.InfiniteFuel || !r.CrashDestructionEnabled || r.HasUsedCheats || r.IsDestroyed)
            throw new Exception("Scientific defaults incorrect");
        r.GravityMultiplier = 1; r.InfiniteFuel = false; r.CrashDestructionEnabled = true;
        if (r.HasUsedCheats) throw new Exception("Default assignments marked mission modified");
        r.GravityMultiplier = .5; r.GravityMultiplier = 1;
        if (!r.HasUsedCheats) throw new Exception("Gravity modification was not sticky");
        r.Reset(); r.InfiniteFuel = true; r.InfiniteFuel = false;
        if (!r.HasUsedCheats) throw new Exception("Infinite fuel modification was not sticky");
        r.Reset(); r.CrashDestructionEnabled = false; r.CrashDestructionEnabled = true;
        if (!r.HasUsedCheats) throw new Exception("Damage modification was not sticky");
        r.GravityMultiplier = 0; r.InfiniteFuel = true; r.CrashDestructionEnabled = false;
        r.Throttle = 1; r.Launch(); Run(r, 10); r.Reset();
        if (r.HasUsedCheats || r.GravityMultiplier != 1 || r.InfiniteFuel || !r.CrashDestructionEnabled || r.IsDestroyed)
            throw new Exception("Reset did not restore default options and clean mission");
        Vector(r.State.Position, initial.Position, 0, "reset position");
        Vector(r.State.Velocity, initial.Velocity, 0, "reset velocity");
        Near(r.State.PropellantKg, initial.PropellantKg, 0, "reset propellant");
        Near(r.ImpactSpeedMetersPerSecond, 0, 0, "reset impact speed");
        // Explicit defaults and fresh implicit defaults must produce identical flight.
        var baseline = Create(); r.Throttle = baseline.Throttle = 1; r.Launch(); baseline.Launch();
        Run(r, 50); Run(baseline, 50);
        Vector(r.State.Position, baseline.State.Position, 0, "default trajectory position");
        Vector(r.State.Velocity, baseline.State.Velocity, 0, "default trajectory velocity");
        Near(r.State.PropellantKg, baseline.State.PropellantKg, 0, "default trajectory fuel");
    }

    static void GravityValidation()
    {
        var r = Create();
        foreach (double value in new[] { -.001, 5.001, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Throws(() => r.GravityMultiplier = value);
        Near(r.GravityMultiplier, 1, 0, "rejected gravity must not mutate option");
        if (r.HasUsedCheats) throw new Exception("Rejected option marked mission modified");
        r.GravityMultiplier = 0; Near(r.Physics.GravityMetersPerSecondSquared, 0, 0, "zero accepted");
        r.GravityMultiplier = 5; Near(r.Physics.GravityMetersPerSecondSquared,
            Gravity.Acceleration(r.State.Position).Magnitude() * 5, 1e-12, "five accepted");
    }

    static void GravityScalingAndLiftoff()
    {
        var normal = Create(thrust: 20000); var heavy = Create(thrust: 20000);
        heavy.GravityMultiplier = 2;
        Near(heavy.Physics.GravityMetersPerSecondSquared, normal.Physics.GravityMetersPerSecondSquared * 2, 1e-12, "scaled gravity telemetry");
        Near(heavy.Physics.NetAccelerationMetersPerSecondSquared, normal.Physics.NetAccelerationMetersPerSecondSquared * 2, 1e-12, "pad free-flight gravity is not hidden");
        normal.Throttle = heavy.Throttle = 1; normal.Launch(); heavy.Launch(); normal.Step(); heavy.Step();
        if (!normal.State.IsLaunched || heavy.State.IsLaunched) throw new Exception("Gravity multiplier must affect liftoff");
        // Compare an airborne ballistic tick at the exact same initial state.
        normal = Create(step: .001); heavy = Create(step: .001);
        normal.Throttle = heavy.Throttle = 1; normal.Launch(); heavy.Launch(); Run(normal, 100); Run(heavy, 100);
        normal.Throttle = heavy.Throttle = 0;
        var expectedDifference = Gravity.Acceleration(normal.State.Position);
        heavy.GravityMultiplier = 2; normal.Step(); heavy.Step();
        var observedDifference = (heavy.State.Velocity - normal.State.Velocity) * 1000;
        Vector(observedDifference, expectedDifference, .001, "gravity multiplier changes actual integrated acceleration");
    }

    static void ZeroGravityFinite()
    {
        var r = Create(thrust: 1); r.GravityMultiplier = 0; r.InfiniteFuel = true;
        r.Throttle = 1; r.Launch(); Run(r, 100);
        if (!r.State.IsLaunched || r.State.IsLanded) throw new Exception("Weak engine should lift in zero gravity");
        var state = r.State; var p = r.Physics;
        Near(p.GravityMetersPerSecondSquared, 0, 0, "zero gravity force");
        foreach (double value in new[] { state.Position.X, state.Position.Y, state.Position.Z,
            state.Velocity.X, state.Velocity.Y, state.Velocity.Z, p.NetAccelerationMetersPerSecondSquared,
            p.PressurePascals, p.DensityKgPerCubicMeter, p.AirSpeedMetersPerSecond })
            if (!double.IsFinite(value)) throw new Exception("Zero gravity produced a nonfinite state");
    }

    static void InfiniteCoreAndNoRefill()
    {
        // Normal core cutoff would happen inside the first .2 s tick.
        var r = Create(step: .2, fuel: 2); var initial = r.State;
        r.InfiniteFuel = true; r.Throttle = 1; r.Launch(); Run(r, 20);
        Near(r.State.MissionTime, 4, 1e-12, "infinite core advances whole steps");
        Near(r.State.PropellantKg, initial.PropellantKg, 0, "infinite core fuel frozen");
        Near(r.State.MassKg, initial.MassKg, 0, "infinite core mass frozen");
        if (!r.State.IsLaunched || r.Physics.ThrustNewtons <= 0) throw new Exception("Infinite core did not continue thrusting");
        r.InfiniteFuel = false; r.Step(); Near(r.State.PropellantKg, 0, 0, "normal core cutoff resumes");
        r.InfiniteFuel = true; r.Step();
        Near(r.State.PropellantKg, 0, 0, "infinite core must not refill empty tank");
        Near(r.Physics.ThrustNewtons, 0, 0, "empty core cannot reignite");
    }

    static void InfiniteSolidAndNoRefill()
    {
        // The .01 s nominal SRB duration must not split/zero an infinite-fuel tick.
        var r = Create(step: .2, booster: Booster()); double initial = r.BoosterPropellantKg; double mass = r.State.MassKg;
        r.InfiniteFuel = true; r.Launch(); Run(r, 20);
        Near(r.State.MissionTime, 4, 1e-12, "infinite solid advances whole steps");
        Near(r.BoosterPropellantKg, initial, 0, "infinite solid fuel frozen");
        Near(r.State.MassKg, mass, 0, "infinite solid mass frozen");
        if (!r.BoosterBurning || r.JettisonBoosters()) throw new Exception("Infinite burning SRB state incorrect");
        r.InfiniteFuel = false; r.Step(); Near(r.BoosterPropellantKg, 0, 0, "normal SRB cutoff resumes");
        r.InfiniteFuel = true; r.Step();
        if (r.BoosterBurning) throw new Exception("Infinite fuel reignited an empty SRB");
        Near(r.Physics.ThrustNewtons, 0, 0, "spent SRB no thrust");
        if (!r.JettisonBoosters()) throw new Exception("Spent SRB separation blocked after disabling cheat");
    }

    static void InfiniteParallelAndHoldDown()
    {
        var r = Create(step: .2, fuel: 2, booster: Booster()); var state = r.State;
        double solid = r.BoosterPropellantKg; double total = r.Physics.TotalPropellantKg;
        r.InfiniteFuel = true; r.Throttle = 1; r.Launch(); Run(r, 10);
        Near(r.State.PropellantKg, state.PropellantKg, 0, "parallel frozen core");
        Near(r.BoosterPropellantKg, solid, 0, "parallel frozen SRB");
        Near(r.Physics.TotalPropellantKg, total, 0, "parallel total propellant frozen");
        Near(r.State.MassKg, state.MassKg, 0, "parallel mass frozen");
        var held = Create(thrust: 1, booster: Booster(duration: 10, thrust: 1));
        var before = held.State; solid = held.BoosterPropellantKg;
        held.InfiniteFuel = true; held.Throttle = 1; held.Launch(); Run(held, 10);
        if (held.State.IsLaunched) throw new Exception("Weak infinite engines should remain held on pad");
        Near(held.State.PropellantKg, before.PropellantKg, 0, "pad frozen core");
        Near(held.BoosterPropellantKg, solid, 0, "pad frozen SRB");
        Near(held.State.MassKg, before.MassKg, 0, "pad frozen mass");
    }

    static RocketSimulation ImpactMission(bool damage, double fuel)
    {
        var stages = new[] { new StageDefinition(1000, fuel, 100000, 100000, 200, 200, 0, 1) };
        var r = new RocketSimulation(.02, stages, Offline, 0, 0);
        r.CrashDestructionEnabled = damage; r.Throttle = 1; r.Launch();
        for (int i = 0; i < 20000 && !r.State.IsLanded; i++) r.Step();
        if (!r.State.IsLanded || !r.State.IsComplete) throw new Exception("Impact mission did not collide");
        return r;
    }

    static void ImpactToggle()
    {
        var damaged = ImpactMission(true, 50); var intact = ImpactMission(false, 50);
        if (!damaged.IsDestroyed || !damaged.State.IsDestroyed || intact.IsDestroyed || intact.State.IsDestroyed)
            throw new Exception("Damage toggle did not change destruction outcome");
        if (damaged.State.Status != "Destroyed: ground impact" || intact.State.Status != "Landed")
            throw new Exception("Impact status does not describe outcome");
        if (damaged.ImpactSpeedMetersPerSecond <= RocketSimulation.DestructiveImpactSpeedMetersPerSecond)
            throw new Exception("Destructive impact test did not exceed gameplay threshold");
        Near(damaged.ImpactSpeedMetersPerSecond, intact.ImpactSpeedMetersPerSecond, 0, "damage toggle preserves impact speed");
        Near(damaged.State.MissionTime, intact.State.MissionTime, 0, "damage toggle preserves collision time");
        Vector(damaged.State.Position, intact.State.Position, 0, "damage toggle preserves collision position");
        Near(intact.State.AltitudeMeters, 0, 1e-5, "disabled damage still collides with surface");
        Near(intact.State.SpeedRelativeGround, 0, 0, "collision stops relative motion");
        Near(damaged.Physics.ThrustNewtons, 0, 0, "destroyed engine is off");
        double time = intact.State.MissionTime; intact.Step(); Near(intact.State.MissionTime, time, 0, "impact remains terminal");
        damaged.Reset();
        if (damaged.IsDestroyed || damaged.State.IsDestroyed) throw new Exception("Reset retained destroyed state");
        Near(damaged.ImpactSpeedMetersPerSecond, 0, 0, "reset clears measured impact speed");
    }

    static void GentleImpact()
    {
        var r = ImpactMission(true, .02);
        if (r.IsDestroyed || r.ImpactSpeedMetersPerSecond > RocketSimulation.DestructiveImpactSpeedMetersPerSecond)
            throw new Exception("Very small hop should remain intact below gameplay damage threshold");
        if (r.HasUsedCheats) throw new Exception("Normal gentle impact marked mission modified");
    }

    static void TelemetryForceAndMassConsistency()
    {
        var r = Create(booster: Booster(duration: 10), drag: .5); var ready = r.Physics;
        r.Throttle = 1;
        Near(r.Physics.ThrustNewtons, 0, 0, "unignited ready engine thrust");
        if (ready.GravityMetersPerSecondSquared <= 9 || ready.NetAccelerationMetersPerSecondSquared <= 9)
            throw new Exception("Pad telemetry invented a zero gravity/support correction");
        r.Launch(); Run(r, 20); r.GravityMultiplier = 1.5;
        var s = r.State; var p = r.Physics;
        var geo = EarthFrames.EcefToGeodetic(EarthFrames.InertialToEcef(s.Position, Omega * s.MissionTime));
        var air = Offline.Sample(Math.Max(StandardAtmosphere.MinimumGeometricAltitudeMeters, geo.AltitudeMeters));
        var wind = Aerodynamics.WindToInertial(air.WindEastNorthUpMetersPerSecond, geo.LatitudeRadians, geo.LongitudeRadians + Omega * s.MissionTime);
        var drag = Aerodynamics.Compute(s.Velocity, Aerodynamics.CorotatingAirVelocity(s.Position, wind), air.DensityKgPerCubicMeter, .5, 1, s.MassKg);
        var gravity = Gravity.Acceleration(s.Position) * 1.5;
        double pitch = r.ActualPitchDegrees * Math.PI / 180, heading = r.ActualHeadingDegrees * Math.PI / 180;
        var direction = Aerodynamics.WindToInertial(new Vector3d(Math.Cos(pitch) * Math.Sin(heading), Math.Cos(pitch) * Math.Cos(heading), Math.Sin(pitch)),
            geo.LatitudeRadians, geo.LongitudeRadians + Omega * s.MissionTime);
        double thrust = 100000 + 50000;
        var expectedAcceleration = gravity + direction * (thrust / s.MassKg) + drag.AccelerationMetersPerSecondSquared;
        Near(p.GravityMetersPerSecondSquared, gravity.Magnitude(), 1e-12, "actual scaled gravity magnitude");
        Near(p.PressurePascals, air.PressurePascals, 0, "actual sampled pressure");
        Near(p.DensityKgPerCubicMeter, air.DensityKgPerCubicMeter, 0, "actual sampled density");
        Near(p.TemperatureKelvin, air.TemperatureKelvin, 0, "actual sampled temperature");
        Near(p.DragForceNewtons, drag.ForceNewtons.Magnitude(), 1e-10, "actual drag force");
        Near(p.ThrustNewtons, thrust, 0, "actual core plus SRB thrust");
        Near(p.NetAccelerationMetersPerSecondSquared, expectedAcceleration.Magnitude(), 1e-12, "vector force sum over actual mass");
        Near(p.AirSpeedMetersPerSecond, drag.AirSpeedMetersPerSecond, 0, "actual relative air speed");
        Near(p.DynamicPressurePascals, drag.DynamicPressurePascals, 0, "actual dynamic pressure");
        Near(p.DynamicPressurePascals, s.DynamicPressurePascals, 0, "snapshot shares dynamic pressure");
        Near(p.TotalPropellantKg, s.PropellantKg + r.BoosterPropellantKg + 50, 1e-10, "active plus attached solid plus future fuel");
        Near(s.MassKg, 1000 + 200 + 100 + p.TotalPropellantKg, 1e-9, "total fuel agrees with total mass");
        if (p.DragForceNewtons <= 0 || p.AirSpeedMetersPerSecond <= 0) throw new Exception("Telemetry force test did not exercise drag");
        if (!r.Stage()) throw new Exception("Telemetry staging test failed to stage");
        Near(r.Physics.TotalPropellantKg, 50 + r.BoosterPropellantKg, 1e-10, "discarded stage fuel excluded");
        Near(r.State.MassKg, 200 + 100 + r.Physics.TotalPropellantKg, 1e-9, "staged total mass and propellant consistent");
    }

    static void Run(RocketSimulation r, int count) { for (int i = 0; i < count; i++) r.Step(); }
    static void Throws(Action action) { try { action(); } catch (ArgumentOutOfRangeException) { return; } throw new Exception("Invalid gravity option was accepted"); }
    static void Near(double a, double b, double tolerance, string name)
    { if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a-b) > tolerance) throw new Exception($"{name}: {a:R} vs {b:R}, tolerance {tolerance:R}"); }
    static void Vector(Vector3d a, Vector3d b, double tolerance, string name)
    { Near(a.X,b.X,tolerance,name+" X"); Near(a.Y,b.Y,tolerance,name+" Y"); Near(a.Z,b.Z,tolerance,name+" Z"); }
}
