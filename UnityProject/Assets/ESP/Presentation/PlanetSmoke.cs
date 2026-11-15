using System;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in player checks; supports -batchmode -nographics -espPlanetSmoke.</summary>
    internal static class PlanetSmoke
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espPlanetSmoke") < 0) return;
            try
            {
                Validate();
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("ESP_PLANET_FAIL " + error);
                Application.Quit(1);
            }
        }
        internal static void Validate()
        {
                CelestialAssets.EnsureLoaded();
                var ephemeris = new LunarEphemeris(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));
                foreach (var body in LunarEphemeris.InnerPlanets)
                {
                    Require(Resources.Load<TextAsset>("SolarSystem/" + body + "Ephemeris") != null, body + " ephemeris resource");
                    Require(CelestialAssets.Surface(body) != null, body + " surface resource");
                    Require(ephemeris.TryPlanet(body, 0, out var position, out _, out _), body + " reference date");
                    Require(position.Magnitude() > 1e9 && !double.IsInfinity(position.Magnitude()), body + " finite position");
                }
                Require(Resources.Load<Texture2D>("SolarSystem/Sun") != null, "Sun texture");
                Require(Resources.Load<Texture2D>("SolarSystem/SaturnRings") != null, "Saturn ring resource");
                Require(Resources.Load<Shader>("SolarSystem/SaturnRingShader") != null, "Saturn ring shader resource");
                var ringMesh = SaturnRingVisual.BuildMesh();
                try
                {
                    Require(ringMesh.vertexCount == (SaturnRingVisual.Segments + 1) * 2, "ring vertex count");
                    Require(ringMesh.triangles.Length == SaturnRingVisual.Segments * 6, "ring triangle count");
                    var vertices = ringMesh.vertices;
                    var uv = ringMesh.uv;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        float radius = i % 2 == 0 ? SaturnRingVisual.InnerRadius : SaturnRingVisual.OuterRadius;
                        Require(Mathf.Abs(vertices[i].magnitude - radius) < .00001f, "annulus radius");
                        Require(uv[i] == new Vector2(i % 2, .5f), "radial strip UV");
                    }
                    Require((vertices[0] - vertices[vertices.Length - 2]).magnitude < .00001f, "closed ring seam");
                    foreach (int index in ringMesh.triangles) Require(index >= 0 && index < vertices.Length, "valid ring index");
                }
                finally { UnityEngine.Object.Destroy(ringMesh); }
                Vector3 camera = new Vector3(1, 2, 3);
                Vector3 near = camera + Vector3.forward * 20;
                FlightEarthBackdrop.CompressPlanetBackdrop(near, camera, 2, out var rendered, out var diameter);
                Require((rendered - near).sqrMagnitude < 1e-8f && Mathf.Abs(diameter - 4) < 1e-6f, "near planet true scale");
                Vector3 distant = camera + new Vector3(3, 4, 0) * 1000000;
                FlightEarthBackdrop.CompressPlanetBackdrop(distant, camera, 2, out rendered, out diameter);
                Require(Mathf.Abs((rendered - camera).magnitude - 500) < .001f, "bounded far distance");
                Require(Vector3.Dot((rendered - camera).normalized, (distant - camera).normalized) > .99999f, "preserved direction");
                float expected = 4 / (distant - camera).magnitude;
                Require(Mathf.Abs(diameter / (rendered - camera).magnitude / expected - 1) < .0001f, "preserved angular diameter");
                Debug.Log("ESP_PLANET_OK resources=" + LunarEphemeris.InnerPlanets.Length + " compressed-backdrop checks passed; rendering not assessed");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
