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
            if (moon == null || sun == null) { Debug.LogWarning("JPL ephemeris unavailable; using approximate celestial positions."); return; }
            try { LunarEphemeris.ConfigureReferenceData(moon.bytes, sun.bytes);
                foreach (var body in LunarEphemeris.InnerPlanets) { var data=Resources.Load<TextAsset>("SolarSystem/"+body+"Ephemeris"); if(data != null) LunarEphemeris.ConfigurePlanet(body,data.bytes); else Debug.LogWarning("Missing ephemeris: "+body); }
            }
            catch (System.Exception e) { Debug.LogWarning("JPL ephemeris invalid; using approximate celestial positions: " + e.Message); }
        }
    }
}
