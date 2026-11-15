using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ESP.Presentation
{
    /// <summary>One continuous WGS84 backdrop in every flight state. Presentation only.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class FlightEarthBackdrop : MonoBehaviour
    {
        public RocketFlightDriver driver;
        public MissionDirector director;
        public Mesh globeMesh;
        public Material globeMaterial;
        public Shader starShader;
        public Texture2D celestialSky;
        public Shader atmosphereShader;
        public Light earthSun;
        public MissionSunlight sunlight;
        [Range(0, 31)] public int backdropLayer = 31;
        // Retained only so old scene builders deserialize; there is no altitude gate.
        [HideInInspector] public double minimumAltitudeMeters;
        public Camera BackgroundCamera => background;
        public Light BackgroundSun => backgroundSun;
        public bool IsRendering => attached && background != null && background.enabled;
        public Vector3d RenderOriginEcef { get; private set; }
        public double FrameLatitudeRadians { get; private set; }
        public double FrameLongitudeRadians { get; private set; }
        private Camera launch, background;
        private GameObject backdropRoot, globe, moon;
        private Material moonMaterial;
        private readonly System.Collections.Generic.Dictionary<CelestialPrimary, GameObject> planetBackdrops = new System.Collections.Generic.Dictionary<CelestialPrimary, GameObject>();
        private readonly System.Collections.Generic.List<Material> planetMaterials = new System.Collections.Generic.List<Material>();
        private CloudShell cloudShell;
        private ContinuousAtmosphere atmosphere;
        private Material starMaterial;
        private Renderer globeRenderer;
        private Light backgroundSun;
        private UniversalAdditionalCameraData launchData, backgroundData;
        private int originalMask;
        private CameraRenderType originalRenderType;
        private CameraClearFlags originalClearFlags;
        private Color originalBackground;
        private bool attached;

        private void LateUpdate() { Apply(driver, director); }

        // Also called synchronously after reset and view changes: never leave an
        // enabled Overlay orphaned until a later frame or deferred Destroy callback.
        public void Apply(RocketFlightDriver flight, MissionDirector mission)
        {
            Camera renderCamera = mission != null ? mission.FlightRenderCamera : null;
            bool show = isActiveAndEnabled && mission != null &&
                renderCamera != null && renderCamera.isActiveAndEnabled &&
                globeMesh != null && globeMaterial != null;
            if (!show) { Suspend(); return; }
            if (launch != null && launch != renderCamera)
            {
                Suspend();
                launch = renderCamera;
                launchData = launch.GetUniversalAdditionalCameraData();
            }
            if (background == null && !Create(renderCamera)) return;

            var site = LaunchSites.All[mission.SelectedSiteIndex];
            FrameLatitudeRadians = site.Latitude * Math.PI / 180;
            FrameLongitudeRadians = site.Longitude * Math.PI / 180;
            double lat = FrameLatitudeRadians, lon = FrameLongitudeRadians;
            // The driver centers real-meter presentation on the spacecraft only
            // while a simulation exists. Reset returns to the selected site's pad.
            RenderOriginEcef = EarthFrames.GeodeticToEcef(lat, lon, site.ElevationMeters);
            if (flight != null && flight.HasPosition)
            {
                var state = flight.Simulation.State;
                RenderOriginEcef = EarthFrames.InertialToEcef(state.Position,
                    Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            }
            Vector3d origin = RenderOriginEcef;
            globe.transform.position = CameraRelativeCoordinates.FromEcef(0, 0, 0,
                origin.X, origin.Y, origin.Z, lat, lon) / 1000000f;
            Vector3 sourceY = new Vector3(0, (float)Math.Sin(lat), (float)Math.Cos(lat));
            Vector3 sourceZ = new Vector3((float)Math.Cos(lon), (float)(Math.Cos(lat) * Math.Sin(lon)),
                (float)(-Math.Sin(lat) * Math.Sin(lon)));
            globe.transform.rotation = Quaternion.LookRotation(sourceZ, sourceY);

            if (moon != null)
            {
                var celestial = flight != null && flight.Simulation != null ? flight.Simulation.Celestial : null;
                double missionTime = flight != null && flight.Simulation != null ? flight.Simulation.State.MissionTime : 0;
                if (celestial == null) celestial = new LunarEphemeris(sunlight != null && sunlight.CurrentUtc != default ? sunlight.CurrentUtc : DateTime.UtcNow);
                var moonEcef = EarthFrames.InertialToEcef(celestial.MoonPosition(missionTime),
                    Aerodynamics.EarthAngularVelocityRadiansPerSecond * missionTime);
                moon.transform.position = CameraRelativeCoordinates.FromEcef(moonEcef.X, moonEcef.Y, moonEcef.Z,
                    origin.X, origin.Y, origin.Z, lat, lon) / 1000000f;
                moon.transform.LookAt(globe.transform.position, Vector3.up);
                foreach(var pair in planetBackdrops)
                {
                    bool available=celestial.TryPlanet(pair.Key,missionTime,out var planetPosition,out _,out _);
                    pair.Value.SetActive(available);
                    if(available)
                    {
                        var fixedPosition=EarthFrames.InertialToEcef(planetPosition,Aerodynamics.EarthAngularVelocityRadiansPerSecond*missionTime);
                        // Rotate/subtract in doubles before casting, in million-meter units.
                        Vector3 relative = CameraRelativeCoordinates.FromEcef(fixedPosition.X/1000000, fixedPosition.Y/1000000, fixedPosition.Z/1000000,
                            origin.X/1000000, origin.Y/1000000, origin.Z/1000000, lat, lon);
                        CompressPlanetBackdrop(relative, launch.transform.position/1000000f,
                            (float)(LunarEphemeris.Radius(pair.Key)/1000000), out var position, out var diameter);
                        pair.Value.transform.position = position;
                        pair.Value.transform.localScale = Vector3.one * diameter;
                    }
                }
            }
            var selectedMaterial = mission.mapLayers != null && mission.mapLayers.earth != null
                ? mission.mapLayers.earth.sharedMaterial : globeMaterial;
            if (selectedMaterial != null && globeRenderer.sharedMaterial != selectedMaterial)
                globeRenderer.sharedMaterial = selectedMaterial;
            Vector3 towardSun = sunlight != null ? sunlight.LocalSunDirection :
                earthSun != null ? -earthSun.transform.forward : Vector3.up;
            if (towardSun.sqrMagnitude < .5f) towardSun = Vector3.up;
            if (backgroundSun != null)
            {
                // Day/night owner supplies fixed-pad ENU. Do not rotate it a second time.
                MissionSunlight.Aim(backgroundSun, towardSun);
                backgroundSun.intensity = sunlight != null ? sunlight.daylightIntensity : 1.5f;
                if (earthSun != null) backgroundSun.color = earthSun.color;
            }
            background.transform.SetPositionAndRotation(launch.transform.position / 1000000f, launch.transform.rotation);
            background.fieldOfView = launch.fieldOfView;
            background.aspect = launch.aspect;
            background.rect = launch.rect;
            background.targetTexture = launch.targetTexture;
            background.targetDisplay = launch.targetDisplay;
            if (atmosphere != null) atmosphere.UpdateView(background, globe.transform, towardSun);
            if (mission.mapLayers != null && mission.mapLayers.CloudsVisible)
                cloudShell.Show(mission.mapLayers.CloudDensityTexture, mission.mapLayers.CloudShader, .35f);
            else cloudShell.Hide();
            Resume();
        }

        // Distant planets occupy a bounded camera-relative shell. Scaling their
        // radius by the same factor preserves angular size and the existing depth
        // range. Nearby bodies remain at their true presentation distance.
        internal static void CompressPlanetBackdrop(Vector3 position, Vector3 camera, float radius,
            out Vector3 renderedPosition, out float renderedDiameter)
        {
            Vector3 offset = position - camera;
            float distance = offset.magnitude;
            float scale = distance > 500 ? 500 / distance : 1;
            renderedPosition = camera + offset * scale;
            renderedDiameter = 2 * radius * scale;
        }

        private bool Create(Camera camera)
        {
            launch = camera;
            launchData = launch.GetUniversalAdditionalCameraData();
            backdropRoot = new GameObject("Continuous Earth presentation");
            var cameraObject = new GameObject("Scaled Earth background camera");
            cameraObject.transform.SetParent(backdropRoot.transform, false);
            background = cameraObject.AddComponent<Camera>();
            background.enabled = false;
            background.CopyFrom(launch);
            background.enabled = false;
            background.cullingMask = 1 << backdropLayer;
            // A tenth of a meter in scaled space: no near-surface hole at liftoff.
            background.nearClipPlane = .0000001f;
            background.farClipPlane = 1000;
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = new Color(.0005f, .001f, .002f);
            backgroundData = background.GetUniversalAdditionalCameraData();
            backgroundData.renderType = CameraRenderType.Base;
            backgroundData.renderPostProcessing = false;
            if (backgroundData.cameraStack == null)
            {
                Debug.LogError("ESP continuous Earth requires the URP Universal Renderer with camera stacking.");
                Release(); return false;
            }
            if (starShader)
            {
                starMaterial = new Material(starShader);
                StarField.Create(background, starMaterial, backdropLayer, celestialSky);
            }
            globe = new GameObject("Continuous WGS84 Earth");
            globe.transform.SetParent(backdropRoot.transform, false);
            globe.layer = backdropLayer;
            globe.AddComponent<MeshFilter>().sharedMesh = globeMesh;
            globeRenderer = globe.AddComponent<MeshRenderer>();
            globeRenderer.sharedMaterial = globeMaterial;
            globeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            globeRenderer.receiveShadows = false;
            moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.name = "Moon backdrop"; moon.layer = backdropLayer;
            moon.transform.SetParent(backdropRoot.transform, false);
            moon.transform.localScale = Vector3.one * (float)(2*LunarEphemeris.MoonRadius/1000000);
            var moonCollider = moon.GetComponent<Collider>(); if (moonCollider != null) Destroy(moonCollider);
            var moonShader = Shader.Find("Universal Render Pipeline/Lit");
            moonMaterial = moonShader != null ? new Material(moonShader) : new Material(globeMaterial);
            moonMaterial.mainTexture = Resources.Load<Texture2D>("MoonSurface");
            moon.GetComponent<Renderer>().sharedMaterial = moonMaterial;
            CelestialAssets.EnsureLoaded();
            foreach(var body in LunarEphemeris.InnerPlanets)
            {
                var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere); planet.name=body+" flight backdrop"; planet.layer=backdropLayer;
                planet.transform.SetParent(backdropRoot.transform,false); planet.transform.localScale=Vector3.one*(float)(2*LunarEphemeris.Radius(body)/1000000);
                var collider=planet.GetComponent<Collider>(); if(collider!=null) Destroy(collider);
                var pm=moonShader!=null ? new Material(moonShader) : new Material(globeMaterial);
                pm.mainTexture=CelestialAssets.Surface(body); planet.GetComponent<Renderer>().sharedMaterial=pm;
                planetMaterials.Add(pm); planetBackdrops.Add(body,planet);
                if (body == CelestialPrimary.Saturn) SaturnRingVisual.Attach(planet.transform);
            }
            var sunObject = new GameObject("Earth backdrop sunlight");
            sunObject.transform.SetParent(backdropRoot.transform, false);
            backgroundSun = sunObject.AddComponent<Light>();
            backgroundSun.type = LightType.Directional;
            backgroundSun.cullingMask = 1 << backdropLayer;
            backgroundSun.shadows = LightShadows.None;
            backgroundSun.enabled = false;
            AuroraCurtains.Create(globe.transform);
            cloudShell = globe.AddComponent<CloudShell>();
            if (atmosphereShader != null && atmosphereShader.isSupported)
                atmosphere = ContinuousAtmosphere.Create(background, atmosphereShader, backdropLayer);
            else Debug.LogWarning("ESP continuous Earth: atmosphere shader unavailable; globe retained.");
            return true;
        }

        private void Resume()
        {
            if (!attached)
            {
                originalMask = launch.cullingMask;
                originalRenderType = launchData.renderType;
                originalClearFlags = launch.clearFlags;
                originalBackground = launch.backgroundColor;
                launchData.renderType = CameraRenderType.Overlay;
                // The scene builder validates URP's read-only clearDepth setting.
                // Real meters and scaled space require separate depth ranges.
                launch.cullingMask = originalMask & ~(1 << backdropLayer);
                launch.clearFlags = CameraClearFlags.Nothing;
                // Own exactly one stack entry, including after repeated synchronous Apply calls.
                while (backgroundData.cameraStack.Remove(launch)) { }
                backgroundData.cameraStack.Add(launch);
                attached = true;
            }
            backdropRoot.SetActive(true);
            backgroundSun.enabled = true;
            background.enabled = true;
        }

        private void Suspend()
        {
            // Disable the Base BEFORE restoring the launch camera to Base, so a
            // reset/map transition cannot draw two independent Bases in one frame.
            if (background != null) background.enabled = false;
            if (backgroundSun != null) backgroundSun.enabled = false;
            if (attached)
            {
                if (backgroundData != null && backgroundData.cameraStack != null)
                    while (backgroundData.cameraStack.Remove(launch)) { }
                if (launch != null && launchData != null)
                {
                    launchData.renderType = originalRenderType;
                    launch.cullingMask = originalMask;
                    launch.clearFlags = originalClearFlags;
                    launch.backgroundColor = originalBackground;
                }
                attached = false;
            }
            if (backdropRoot != null) backdropRoot.SetActive(false);
        }

        private void Release()
        {
            Suspend();
            if (backdropRoot != null) Destroy(backdropRoot);
            if (starMaterial != null) Destroy(starMaterial);
            if (moonMaterial != null) Destroy(moonMaterial);
            foreach(var pm in planetMaterials) if(pm!=null) Destroy(pm); planetMaterials.Clear(); planetBackdrops.Clear();
            backdropRoot = null; background = null; globe = null; moon = null; moonMaterial = null; cloudShell = null;
            atmosphere = null; globeRenderer = null; backgroundSun = null; starMaterial = null;
            launch = null; launchData = null; backgroundData = null;
        }
        private void OnDisable() { Suspend(); }
        private void OnDestroy() { Release(); }
    }
}
