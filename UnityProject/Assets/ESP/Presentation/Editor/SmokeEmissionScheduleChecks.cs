using System;
using UnityEditor;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    // Pure scheduling invariants. Run explicitly in the owning VM editor; no scene mutation.
    public static class SmokeEmissionScheduleChecks
    {
        [MenuItem("ESP/Verify smoke emission scheduling")]
        public static void Run()
        {
            var whole = new SmokeEmissionSchedule();
            var split = new SmokeEmissionSchedule();
            int expected = whole.Advance(1, 4, 32).Count, actual = 0;
            for (int i = 0; i < 60; ++i) actual += split.Advance(1.0 / 60, 4, 32).Count;
            Require(expected == 4 && actual == expected, "fractional emission must be frame-partition independent");
            var stagger = new SmokeEmissionSchedule().Advance(1, 4, 32);
            Require(Math.Abs(stagger.AgeSeconds(0) - .75) < 1e-9 && stagger.AgeSeconds(3) < 1e-9,
                "sub-frame births must span the interval");
            var bounded = new SmokeEmissionSchedule();
            var batch = bounded.Advance(100, 4, 32);
            Require(batch.Count == 32 && Math.Abs(batch.OldestAgeSeconds - 7.75) < 1e-9,
                "hitches must retain only the newest bounded emissions");
            Require(bounded.Advance(.01, 4, 32).Count == 0, "discarded emissions must not become a backlog");
            bounded.Advance(.1, 4, 32); bounded.Clear();
            Require(bounded.Advance(.2, 4, 32).Count == 0, "reset must discard emission phase");
            Require(bounded.Advance(double.NaN, 4, 32).Count == 0 && bounded.Advance(1, double.PositiveInfinity, 32).Count == 0,
                "non-finite inputs must not emit");
            Debug.Log("ESP_SMOKE_SCHEDULE_OK cadence, sub-frame age, budget, reset, finite inputs");
        }
        private static void Require(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason); }
    }
}
