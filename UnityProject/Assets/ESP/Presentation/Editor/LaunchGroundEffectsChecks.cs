using System;
using UnityEditor;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    public static class LaunchGroundEffectsChecks
    {
        [MenuItem("ESP/Verify launch dust pressure fronts")]
        public static void Run()
        {
            Require(LaunchGroundEffects.ShockMotion(85, .99, 0, 5, 0) == Vector2.zero,
                "Pressure front reached 85 m before its travel time");
            Require(LaunchGroundEffects.ShockMotion(85, 1, 0, 5, 0).x > 0,
                "Pressure front must push outward on arrival");
            Require(LaunchGroundEffects.ShockMotion(0, 10, -1, -1, 0) == Vector2.zero,
                "No pressure front before ignition");
            Require(LaunchGroundEffects.ShockMotion(85, 30, 0, 5, .5f).magnitude < .001f,
                "Dust oscillation must decay after cutoff");
            // A front emitted before cutoff still reaches more distant dust afterward.
            Require(LaunchGroundEffects.ShockMotion(170, 2, 0, 0, 0).x > 0,
                "Cutoff must not erase an already-travelling front");
            for (int radius = 0; radius <= 600; radius += 15)
            for (int tick = 0; tick <= 600; tick++)
            {
                var offset = LaunchGroundEffects.ShockMotion(radius, tick * .05, 0, 8, .37f);
                Require(!float.IsNaN(offset.x) && !float.IsNaN(offset.y) && offset.magnitude <= 4.3f,
                    "Pressure wake must remain finite and bounded");
            }
            Debug.Log("ESP_LAUNCH_DUST_CHECKS_OK travel time, outward push, cutoff, decay, bounds");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
