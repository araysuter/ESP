using System;
using FlightCore;
using UnityEditor;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    public static class AuroraChecks
    {
        [MenuItem("ESP/Verify aurora")]
        public static void Run()
        {
            var mesh = PolarAurora.BuildCurtains();
            try
            {
                var vertices = mesh.vertices;
                Require(vertices.Length == 3 * 513 * 13 && mesh.triangles.Length == 3 * 512 * 12 * 6, "curtain topology");
                for (int curtain = 0; curtain < 3; curtain++)
                for (int x = 0; x <= 512; x += 64)
                for (int y = 0; y <= 12; y += 12)
                {
                    var v = vertices[(curtain * 513 + x) * 13 + y];
                    var geo = EarthFrames.EcefToGeodetic(new Vector3d(v.x * 1000000, v.z * 1000000, v.y * 1000000));
                    double expected = y == 0 ? PolarAurora.BottomAltitudeMeters : PolarAurora.TopAltitudeMeters;
                    Require(Math.Abs(geo.AltitudeMeters - expected) < 2, "geodetic altitude");
                    Require(geo.LatitudeRadians > 60 * Math.PI / 180, "northern polar oval");
                }
                Require((vertices[0] - vertices[512 * 13]).magnitude < .00001f, "closed longitude seam");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            var clock = new AuroraClock();
            var flight = new object();
            clock.Advance(null, 0, 1, false);
            clock.Advance(flight, 0, 1, false);
            clock.Advance(flight, 10, 1, false);
            Require(Math.Abs(clock.Seconds - 11) < 1e-9, "time warp follows simulated elapsed time");
            clock.Advance(flight, 10, 1, true);
            Require(Math.Abs(clock.Seconds - 11) < 1e-9, "pause freezes flight animation");
            clock.Advance(null, 0, 1, true);
            Require(Math.Abs(clock.Seconds - 11) < 1e-9, "paused preflight freezes animation");
            clock.Advance(new object(), 0, 1, false);
            Require(Math.Abs(clock.Seconds - 11) < 1e-9, "new flight does not jump backwards");
            var shader = Resources.Load<Shader>("PolarAurora");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "aurora shader import");
            Debug.Log("ESP_AURORA_OK heights, oval, seam, warp, pause, reset, shader");
        }
        private static void Require(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException("Aurora: " + reason); }
    }
}
