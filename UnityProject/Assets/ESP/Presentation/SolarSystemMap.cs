using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Visual-only Sun/Earth context in the map's million-meter ECEF units.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class SolarSystemMap : MonoBehaviour
    {
        public const float AstronomicalUnit = 149597.8707f;
        public const float SunRadius = 695.7f;
        private const int Segments = 256;
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private MissionDirector director;
        private MissionSunlight sunlight;
        private LineRenderer orbit;
        private Transform sunObject, moonObject;
        public Vector3 SunPosition => sunObject != null ? sunObject.position : Vector3.zero;
        public Vector3 MoonPosition => moonObject != null ? moonObject.position : Vector3.zero;
        public Vector3? RoadsterPosition { get; private set; }
        private LineRenderer moonOrbit;
        private Material moonMaterial;
        private readonly System.Collections.Generic.Dictionary<CelestialPrimary, Transform> planets = new System.Collections.Generic.Dictionary<CelestialPrimary, Transform>();
        private readonly System.Collections.Generic.Dictionary<CelestialPrimary, LineRenderer> planetOrbits = new System.Collections.Generic.Dictionary<CelestialPrimary, LineRenderer>();
        private readonly System.Collections.Generic.List<Material> planetMaterials = new System.Collections.Generic.List<Material>();
        private float nextPlanetOrbit;
        public int AvailablePlanetCount { get; private set; }
        public bool TryPlanetPosition(CelestialPrimary body, out Vector3 position) { if(planets.TryGetValue(body,out var planet) && planet.gameObject.activeInHierarchy) { position=planet.position; return true; } position=default; return false; }
        private Material material, sunMaterial;

        public void Initialize(MissionDirector owner)
        {
            CelestialAssets.EnsureLoaded();
            director = owner;
            sunlight = owner.GetComponent<MissionSunlight>();
            if (owner.mapRoot == null || owner.orbitMaterial == null) return;
            var lineObject = new GameObject("Approximate Earth orbit around Sun");
            lineObject.layer = MissionInsetView.MapLayer;
            lineObject.transform.SetParent(owner.mapRoot.transform, false);
            orbit = lineObject.AddComponent<LineRenderer>();
            orbit.useWorldSpace = true;
            orbit.positionCount = points.Length;
            orbit.alignment = LineAlignment.View;
            orbit.shadowCastingMode = ShadowCastingMode.Off;
            orbit.receiveShadows = false;
            material = new Material(owner.orbitMaterial);
            material.mainTexture = null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.32f, .55f, .66f, 1));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.32f, .55f, .66f, 1));
            orbit.sharedMaterial = material;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sun (visual only)";
            sphere.layer = MissionInsetView.MapLayer;
            sphere.transform.SetParent(owner.mapRoot.transform, false);
            sphere.transform.localScale = Vector3.one * (2 * SunRadius);
            var collider = sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            sunMaterial = new Material(owner.orbitMaterial);
            sunMaterial.mainTexture = Resources.Load<Texture2D>("SolarSystem/Sun");
            if (sunMaterial.HasProperty("_BaseColor")) sunMaterial.SetColor("_BaseColor", new Color(16, 15.6f, 14.72f));
            if (sunMaterial.HasProperty("_Color")) sunMaterial.SetColor("_Color", new Color(16, 15.6f, 14.72f));
            renderer.sharedMaterial = sunMaterial;
            sunObject = sphere.transform;
            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon"; moon.layer = MissionInsetView.MapLayer;
            moon.transform.SetParent(owner.mapRoot.transform, false);
            moon.transform.localScale = Vector3.one * (2f * (float)(LunarEphemeris.MoonRadius / 1000000));
            var moonCollider = moon.GetComponent<Collider>(); if (moonCollider != null) Destroy(moonCollider);
            var moonRenderer = moon.GetComponent<MeshRenderer>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            moonMaterial = lit != null ? new Material(lit) : new Material(owner.orbitMaterial);
            moonMaterial.mainTexture = Resources.Load<Texture2D>("MoonSurface");
            moonRenderer.sharedMaterial = moonMaterial;
            moonObject = moon.transform;
            foreach(var body in LunarEphemeris.InnerPlanets)
            {
                var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere); planet.name=body.ToString(); planet.layer=MissionInsetView.MapLayer;
                planet.transform.SetParent(owner.mapRoot.transform,false); planet.transform.localScale=Vector3.one*(float)(2*LunarEphemeris.Radius(body)/1000000);
                var pc=planet.GetComponent<Collider>(); if(pc!=null) Destroy(pc);
                var pm=lit!=null ? new Material(lit) : new Material(owner.orbitMaterial); pm.mainTexture=CelestialAssets.Surface(body);
                planet.GetComponent<Renderer>().sharedMaterial=pm; planetMaterials.Add(pm); planets.Add(body,planet.transform);
                if (body == CelestialPrimary.Saturn) SaturnRingVisual.Attach(planet.transform);
                var o=new GameObject(body+" orbit"); o.layer=MissionInsetView.MapLayer; o.transform.SetParent(owner.mapRoot.transform,false);
                var line=o.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.positionCount=129; line.alignment=LineAlignment.View; line.sharedMaterial=material; line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false; planetOrbits.Add(body,line);
            }
            var lunarLine = new GameObject("Approximate Moon orbit");
            lunarLine.layer = MissionInsetView.MapLayer; lunarLine.transform.SetParent(owner.mapRoot.transform, false);
            moonOrbit = lunarLine.AddComponent<LineRenderer>(); moonOrbit.useWorldSpace = true;
            moonOrbit.positionCount = 97; moonOrbit.alignment = LineAlignment.View;
            moonOrbit.shadowCastingMode = ShadowCastingMode.Off; moonOrbit.receiveShadows = false;
            moonOrbit.sharedMaterial = material;
        }

        public static Vector3 EclipticPole(DateTime utc)
        {
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double obliquity = (23.439 - .00000036 * (utc - epoch).TotalDays) * Math.PI / 180;
            var pole = EarthFrames.InertialToEcef(new Vector3d(0, -Math.Sin(obliquity), Math.Cos(obliquity)),
                CelestialFrames.GreenwichSiderealAngle(utc));
            return new Vector3((float)pole.X, (float)pole.Z, (float)pole.Y).normalized;
        }

        public static bool EarthOccludes(Vector3 point, Vector3 camera)
        {
            // Promote before subtracting/multiplying: float quadratic terms lose
            // enough precision to report false occlusion near a surface target.
            double x = camera.x, y = camera.y, z = camera.z;
            double dx = (double)point.x - x, dy = (double)point.y - y, dz = (double)point.z - z;
            const double a = 6.378137, b = 6.356752314245;
            double aa = (dx * dx + dz * dz) / (a * a) + dy * dy / (b * b);
            double bb = 2 * ((x * dx + z * dz) / (a * a) + y * dy / (b * b));
            double cc = (x * x + z * z) / (a * a) + y * y / (b * b) - 1;
            if (cc < 0) return true;
            double discriminant = bb * bb - 4 * aa * cc;
            if (aa <= 0 || discriminant < 0) return false;
            double t = (-bb - Math.Sqrt(discriminant)) / (2 * aa);
            return t >= 0 && t < .99999;
        }

        private void LateUpdate()
        {
            if (orbit == null || director == null || director.mapCamera == null || sunlight == null) return;
            var controls = director.mapCamera.GetComponent<GlobeCameraControls>();
            Vector3 towardSun = new Vector3((float)sunlight.SunEcef.X, (float)sunlight.SunEcef.Z, (float)sunlight.SunEcef.Y).normalized;
            var driver = director.FlightDriver as RocketFlightDriver;
            var simulation = driver != null ? driver.Simulation : null;
            var ephemeris = simulation != null ? simulation.Celestial : new LunarEphemeris(sunlight.CurrentUtc == default ? DateTime.UtcNow : sunlight.CurrentUtc);
            double missionTime = simulation != null ? simulation.State.MissionTime : 0;
            double angle = Aerodynamics.EarthAngularVelocityRadiansPerSecond * missionTime;
            Vector3 sunPosition = ephemeris != null ? ToMap(EarthFrames.InertialToEcef(ephemeris.SunPosition(missionTime), angle)) : towardSun * AstronomicalUnit;
            AvailablePlanetCount = 0;
            foreach(var pair in planets)
            {
                Vector3d planetPosition = default;
                bool available=ephemeris!=null && ephemeris.TryPlanet(pair.Key,missionTime,out planetPosition,out _,out _);
                pair.Value.gameObject.SetActive(available);
                if (available) AvailablePlanetCount++;
                if(available) pair.Value.position=ToMap(EarthFrames.InertialToEcef(planetPosition,angle));
                var pl=planetOrbits[pair.Key]; pl.enabled=available && director.IsMapView && controls!=null && controls.Distance>=1000;
                if(pl.enabled && Time.unscaledTime>=nextPlanetOrbit)
                {
                    double period=pair.Key==CelestialPrimary.Mercury?87.969:pair.Key==CelestialPrimary.Venus?224.701:pair.Key==CelestialPrimary.Mars?686.98:pair.Key==CelestialPrimary.Ceres?1680.0:pair.Key==CelestialPrimary.Jupiter?4332.59:pair.Key==CelestialPrimary.Saturn?10759.22:pair.Key==CelestialPrimary.Uranus?30688.5:pair.Key==CelestialPrimary.Neptune?60182:pair.Key==CelestialPrimary.Pluto?90560:pair.Key==CelestialPrimary.Eris?203646:pair.Key==CelestialPrimary.Haumea?104025:112327;
                    // The offline dataset spans 17 years. Draw the available arc;
                    // do not join missing samples to the origin or imply a full orbit.
                    period=Math.Min(period,3652.5);
                    var arc=new System.Collections.Generic.List<Vector3>();
                    for(int j=0;j<=128;j++) { double t=missionTime+(j-64)*period*86400/128; if(ephemeris.TryPlanet(pair.Key,t,out var future,out _,out _)) arc.Add(sunPosition+ToMap(EarthFrames.InertialToEcef(future-ephemeris.SunPosition(t),angle))); }
                    pl.positionCount=arc.Count; if(arc.Count>0) pl.SetPositions(arc.ToArray());
                }
                if(pl.enabled) pl.widthMultiplier=Mathf.Max(.1f,controls.Distance*.0014f);
            }
            if(Time.unscaledTime>=nextPlanetOrbit) nextPlanetOrbit=Time.unscaledTime+2;
            if (moonObject != null && ephemeris != null)
            {
                moonObject.position = ToMap(EarthFrames.InertialToEcef(ephemeris.MoonPosition(missionTime), angle));
                moonObject.LookAt(Vector3.zero, Vector3.up);
            }
            if (moonOrbit != null && ephemeris != null)
            {
                moonOrbit.enabled = director.IsMapView && controls != null && controls.Distance < 10000;
                if (moonOrbit.enabled)
                {
                    for (int k=0; k<=96; k++) moonOrbit.SetPosition(k, ToMap(EarthFrames.InertialToEcef(ephemeris.MoonPosition(missionTime+(k-48)*27.321661*86400/96),angle)));
                    moonOrbit.widthMultiplier = Mathf.Max(.015f, controls.Distance * .001f);
                }
            }
            if (sunObject != null) sunObject.position = sunPosition;
            orbit.enabled = director.IsMapView && controls != null && controls.Distance >= 1000 && sunlight.CurrentUtc != default;
            RoadsterPosition = orbit.enabled
                ? sunPosition + ToMap(CelestialFrames.J2000ToEcef(RoadsterFromSun(sunlight.CurrentUtc), sunlight.CurrentUtc))
                : (Vector3?)null;
            if (!orbit.enabled) return;
            Vector3 radial = -towardSun;
            Vector3 tangent = Vector3.Cross(EclipticPole(sunlight.CurrentUtc), radial).normalized;
            for (int i = 0; i <= Segments; i++)
            {
                if (ephemeris != null)
                    points[i] = sunPosition - ToMap(EarthFrames.InertialToEcef(ephemeris.SunPosition(missionTime + i * 365.256363004 * 86400 / Segments), angle));
                else
                {
                    float orbitAngle = 2 * Mathf.PI * i / Segments;
                    points[i] = sunPosition + AstronomicalUnit * (radial * Mathf.Cos(orbitAngle) + tangent * Mathf.Sin(orbitAngle));
                }
            }
            orbit.SetPositions(points);
            orbit.widthMultiplier = Mathf.Max(.1f, controls.Distance * .0014f);
        }

        private static Vector3 ToMap(Vector3d p) => new Vector3((float)(p.X/1000000),(float)(p.Z/1000000),(float)(p.Y/1000000));

        // JPL Horizons -143205, Sun-centered J2000 ecliptic elements, JD 2461359.5.
        // A fixed two-body display approximation with UTC used in place of TDB.
        internal static Vector3d RoadsterFromSun(DateTime utc)
        {
            const double rad = Math.PI / 180;
            const double eccentricity = .2559597937;
            const double axisMeters = 1.3252644534 * 149597870700.0;
            const double inclination = 1.0747801201 * rad;
            const double node = 316.8723890132 * rad;
            const double perihelion = 177.7888708036 * rad;
            const double obliquity = 23.4392911111 * rad;
            double days = (utc - new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
            double meanAnomaly = (271.3832440704 + .6460263762 * days) % 360 * rad;
            double anomaly = meanAnomaly;
            for (int iteration = 0; iteration < 6; iteration++)
                anomaly -= (anomaly - eccentricity * Math.Sin(anomaly) - meanAnomaly) /
                    (1 - eccentricity * Math.Cos(anomaly));

            double x = axisMeters * (Math.Cos(anomaly) - eccentricity);
            double y = axisMeters * Math.Sqrt(1 - eccentricity * eccentricity) * Math.Sin(anomaly);
            double alongNode = x * Math.Cos(perihelion) - y * Math.Sin(perihelion);
            double acrossNode = x * Math.Sin(perihelion) + y * Math.Cos(perihelion);
            double eclipticX = alongNode * Math.Cos(node) - acrossNode * Math.Cos(inclination) * Math.Sin(node);
            double eclipticY = alongNode * Math.Sin(node) + acrossNode * Math.Cos(inclination) * Math.Cos(node);
            double eclipticZ = acrossNode * Math.Sin(inclination);
            return new Vector3d(eclipticX, eclipticY * Math.Cos(obliquity) - eclipticZ * Math.Sin(obliquity),
                eclipticY * Math.Sin(obliquity) + eclipticZ * Math.Cos(obliquity));
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (sunMaterial != null) Destroy(sunMaterial);
            if (moonMaterial != null) Destroy(moonMaterial);
            foreach(var pm in planetMaterials) if(pm!=null) Destroy(pm);
        }
    }
}
