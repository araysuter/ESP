using System;
using System.Collections.Generic;
using FlightCore;

static class Program
{
    static void Main()
    {
        // Slowly rising vehicle: the first centimeter of motion is retained even
        // before a committed time/distance sample, and its endpoint stays exact.
        var trail = new FlightTrail();
        var pad = new Vector3d(EarthFrames.SemiMajorAxisMeters, 0, 0);
        trail.Record(0, pad);
        var liftoff = pad + new Vector3d(.01, 0, 0);
        trail.Record(.02, liftoff);
        Check(trail.Count == 2, "slow liftoff has a visible segment");
        Near(trail[0].InertialPositionMeters, pad, 0, "initial pad retained");
        Near(trail[trail.Count - 1].InertialPositionMeters, liftoff, 0, "slow endpoint exact");
        for (int i = 0; i < 100; i++) trail.Record(.02, liftoff);
        Check(trail.Count == 2, "pause does not accumulate duplicate history");

        // A multi-hour high-speed observed path forces repeated downsampling.
        // Every retained point must still be an actual input, in time order;
        // memory remains bounded while preserving the full mission's first point.
        trail.Reset();
        var observations = new Dictionary<double, Vector3d>();
        for (int i = 0; i <= 50000; i++)
        {
            double t = i * .5;
            var point = new Vector3d(7e6 * Math.Cos(t / 900), 7e6 * Math.Sin(t / 900), 10000 * Math.Sin(t / 80));
            observations[t] = point;
            trail.Record(t, point);
            Check(trail.Count <= FlightTrail.MaximumSamples, "bounded long-flight history");
            Near(trail[trail.Count - 1].InertialPositionMeters, point, 0, "long-flight endpoint exact");
        }
        Near(trail[0].InertialPositionMeters, observations[0], 0, "full-span launch anchor");
        Check(trail[trail.Count - 1].MissionTimeSeconds == 25000, "latest mission time retained");
        for (int i = 0; i < trail.Count; i++)
        {
            var sample = trail[i];
            Check(observations.ContainsKey(sample.MissionTimeSeconds), "retained time was observed");
            Near(sample.InertialPositionMeters, observations[sample.MissionTimeSeconds], 0, "retained position was observed");
            if (i > 0) Check(sample.MissionTimeSeconds > trail[i - 1].MissionTimeSeconds, "history chronological after downsampling");
        }

        // One shared current epoch rotates the entire path; sample timestamps must
        // not turn this display into a ground track. At epoch pi/2, X becomes -Y.
        trail.Reset();
        var x = new Vector3d(7e6, 0, 0);
        trail.Record(0, x);
        trail.Record(100, x);
        for (int i = 0; i < trail.Count; i++)
            Near(trail.EcefPosition(i, Math.PI / 2), new Vector3d(0, -7e6, 0), 1e-8, "shared current Earth epoch");
        Near(trail.EcefPosition(0, 0), x, 0, "frame conversion never mutates inertial source");

        // Reset or a rewind starts a new mission without joining separate flights.
        trail.Record(1, liftoff);
        Check(trail.Count == 1 && trail[0].MissionTimeSeconds == 1, "rewind resets old path");
        trail.Reset();
        Check(trail.Count == 0, "explicit reset empties path");
        trail.Record(0, pad);
        Check(trail.Count == 1, "new mission single starting point");
        foreach (double t in new[] { -1.0, double.NaN, double.PositiveInfinity })
        {
            bool rejected = false;
            try { trail.Record(t, pad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && trail.Count == 1, "invalid observation leaves history intact");
        }
        Console.WriteLine("PASS flight trail: slow liftoff, pause, bounded multi-hour retention, observed-only samples, shared epoch, reset/rewind");
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    static void Near(Vector3d value, Vector3d expected, double tolerance, string name) => Check((value - expected).Magnitude() <= tolerance, name);
}
