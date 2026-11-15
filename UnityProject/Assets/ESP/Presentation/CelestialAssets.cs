using FlightCore;
using UnityEngine;
namespace ESP.Presentation
{
    internal static class CelestialAssets
    {
        static bool initialized;
        public static void EnsureLoaded()
        {
            if (initialized) return;
            initialized = true;
            var moon = Resources.Load<TextAsset>("MoonEphemeris");
            var sun = Resources.Load<TextAsset>("SunEphemeris");
            if (moon == null || sun == null)
                Debug.LogWarning("JPL Moon/Sun ephemeris unavailable; using approximate celestial positions.");
            else
            {
                try { LunarEphemeris.ConfigureReferenceData(moon.bytes, sun.bytes); }
                catch (System.Exception e) { Debug.LogWarning("JPL Moon/Sun ephemeris invalid; using approximate celestial positions: " + e.Message); }
            }
            // Each offline table is independent. One missing or invalid resource must
            // not prevent the remaining planets from appearing.
            foreach (var body in LunarEphemeris.InnerPlanets)
            {
                var data = Resources.Load<TextAsset>("SolarSystem/" + body + "Ephemeris");
                if (data == null) { Debug.LogWarning("Missing ephemeris: " + body); continue; }
                try { LunarEphemeris.ConfigurePlanet(body, data.bytes); }
                catch (System.Exception e) { Debug.LogWarning("Invalid ephemeris for " + body + ": " + e.Message); }
            }
        }

        public static Texture2D Surface(CelestialPrimary body)
        {
            // Visible-light Venus is cloud covered; keep the bundled surface map
            // available as a fallback rather than displaying exposed terrain.
            var clouds = body == CelestialPrimary.Venus ? Resources.Load<Texture2D>("SolarSystem/VenusClouds") : null;
            return clouds != null ? clouds : Resources.Load<Texture2D>("SolarSystem/" + body);
        }
    }
}
