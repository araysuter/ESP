using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Adapter contract for an actual flight core. No dynamics are supplied by the presentation.</summary>
    public interface IMissionFlightDriver
    {
        bool CanLaunch { get; }
        string Status { get; }
        double AltitudeMeters { get; }
        double SpeedMetersPerSecond { get; }
        double MissionTimeSeconds { get; }
        float Throttle { get; }
        void Launch(LaunchSite site, WeatherSnapshot weather);
        void ResetMission();
        void Stage();
        void SetThrottle(float value);
    }

    public sealed class MissionDirector : MonoBehaviour
    {
        public Camera mapCamera, launchCamera;
        public GameObject mapRoot;
        public MissionInsetView Inset { get; private set; }
        public Camera MapRenderCamera => IsMapView ? mapCamera : Inset != null ? Inset.MapPreviewCamera : null;
        public Camera FlightRenderCamera => IsMapView ? (Inset != null ? Inset.FlightPreviewCamera : null) : launchCamera;
        public NasaMapLayers mapLayers;
        public LaunchWeatherProfile weather;
        public NasaWeatherProfile nasaWeather;
        public Transform EnvironmentRoot => environment != null ? environment.transform : null;
        public GlobePointPicker picker;
        public PointWeather pointWeather;
        public NasaRegionalPreview regional;
        public NasaEventFeed events;
        public NasaSpaceWeather spaceWeather;
        public event Action PointSelected;
        public GameObject rocketPrefab;
        public GameObject[] vehiclePrefabs;
        public int VehicleIndex { get; private set; }
        public string VehicleLabel => VehicleIndex == 1 ? "STARSHIP · ESTIMATED SPECS" : VehicleIndex == 2 ? "SLS · ESTIMATED SPECS" : "ESP DEMO ROCKET";
        public void SelectVehicle(int index)
        {
            if (vehiclePrefabs == null || index < 0 || index >= vehiclePrefabs.Length || vehiclePrefabs[index] == null || !CanLaunch) return;
            VehicleIndex = index; rocketPrefab = vehiclePrefabs[index];
            FlightDriver?.ResetMission();
            ReleaseEnvironment();
            if (Inset != null || !IsMapView)
            {
                bool wasMap = IsMapView;
                GoToLaunch(SelectedSiteIndex);
                if (wasMap) ShowMap();
            }
        }
        public Texture[] siteGroundTextures;
        public GameObject[] siteStructurePrefabs;
        public Material siteGroundTemplate;
        public IMissionFlightDriver FlightDriver { get; set; }
        public Transform RocketVisual { get; private set; }
        public bool IsMapView { get; private set; } = true;
        public int SelectedSiteIndex { get; private set; }
        public string SiteName => LaunchSites.All[SelectedSiteIndex].Name;
        public string FlightStatus => FlightDriver == null ? "Flight simulation unavailable" : FlightDriver.Status;
        public string WeatherSummary
        {
            get
            {
                if (pointWeather == null) return "Modeled weather unavailable.";
                bool picked = picker != null && picker.HasSelection;
                var site = LaunchSites.All[SelectedSiteIndex];
                double latitude = picked ? picker.Latitude : site.Latitude;
                double longitude = picked ? picker.Longitude : site.Longitude;
                string location = picked ? "SELECTED POINT" : site.Name;
                return string.Format("{0}  ·  {1:F2}°, {2:F2}°\n{3}\n{4}\n{5}",
                    location, latitude, longitude, pointWeather.Status, pointWeather.validtime, pointWeather.Details);
            }
        }
        public void RefreshPointWeather(bool forceRefresh = false)
        {
            if (pointWeather == null) return;
            var site = LaunchSites.All[SelectedSiteIndex];
            bool picked = picker != null && picker.HasSelection;
            pointWeather.Fetch(picked ? picker.Latitude : site.Latitude,
                picked ? picker.Longitude : site.Longitude, forceRefresh);
        }
        public Texture RegionalImage => regional != null ? regional.Image : null;
        public string MapCredit => mapLayers != null ? "NASA GIBS / Blue Marble • " + mapLayers.DisplayedDescription + " • " + mapLayers.Status : "NASA Blue Marble";
        public string NasaStatus => "NASA EONET: " + (events != null ? events.Status : "unavailable") + "\nNASA DONKI: " + (spaceWeather != null ? spaceWeather.Status : "unavailable");
        public double AltitudeMeters => FlightDriver?.AltitudeMeters ?? 0;
        public double SpeedMetersPerSecond => FlightDriver?.SpeedMetersPerSecond ?? 0;
        public double MissionTimeSeconds => FlightDriver?.MissionTimeSeconds ?? 0;
        public float Throttle => FlightDriver?.Throttle ?? 0;
        public bool CanLaunch => FlightDriver != null && FlightDriver.CanLaunch;
        private GameObject environment;
        private Material groundMaterial;
        public Material orbitMaterial;
        public MapOrbitVisual OrbitVisual { get; private set; }
        private Vector3 launchCameraHomePosition;
        private Quaternion launchCameraHomeRotation;
        private float launchCameraHomeFieldOfView;
        private bool hasLaunchCameraHome;

        private void Start()
        {
            CaptureLaunchCameraHome();
            // Driver registers in Awake; choose the real model before any preflight view is built.
            if (vehiclePrefabs != null && vehiclePrefabs.Length > 1 && vehiclePrefabs[1] != null && CanLaunch)
                SelectVehicle(1);
            if (mapRoot != null)
            {
                OrbitVisual = mapRoot.AddComponent<MapOrbitVisual>();
                OrbitVisual.orbitMaterial = orbitMaterial;
                OrbitVisual.Initialize(this);
            }
            if (picker != null) picker.PointSelected += OnPointSelected;
            if (mapLayers != null) mapLayers.LayerChanged += RefreshRegion;
            Inset = gameObject.AddComponent<MissionInsetView>();
            Inset.Initialize(this);
            GoToLaunch(SelectedSiteIndex);
            ShowMap();
            RequestWeather();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-espMissionCapture") >= 0) StartCoroutine(Capture());
        }

        public void ShowMap()
        {
            IsMapView = true;
            if (mapRoot) mapRoot.SetActive(true);
            if (mapCamera) mapCamera.gameObject.SetActive(true);
            if (launchCamera) launchCamera.gameObject.SetActive(false);
            if (environment) environment.SetActive(true);
            Inset?.SynchronizeView();
            RefreshFlightBackdrop();
        }

        public void GoToLaunch(int siteIndex)
        {
            if (siteIndex < 0 || siteIndex >= LaunchSites.All.Length) throw new ArgumentOutOfRangeException(nameof(siteIndex));
            bool siteChanged = SelectedSiteIndex != siteIndex;
            SelectedSiteIndex = siteIndex;
            if (siteChanged) { FlightDriver?.ResetMission(); RequestWeather(); if (picker == null || !picker.HasSelection) RefreshPointWeather(); }
            if (environment && !siteChanged)
            {
                environment.SetActive(true);
                SetLaunchView();
                return;
            }
            ReleaseEnvironment();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            // Use fresh Lit defaults: the unlit orbit template carries incompatible shader properties.
            groundMaterial = new Material(shader);
            groundMaterial.color = Color.white;
            groundMaterial.SetFloat("_Smoothness", 0);
            if (siteGroundTextures != null && siteIndex < siteGroundTextures.Length && siteGroundTextures[siteIndex]) { groundMaterial.mainTexture = siteGroundTextures[siteIndex]; }
            else groundMaterial.color = new Color(.22f, .29f, .24f);
            var structure = siteStructurePrefabs != null && siteIndex < siteStructurePrefabs.Length ? siteStructurePrefabs[siteIndex] : null;
            environment = LaunchEnvironment.Build(LaunchSites.All[siteIndex], groundMaterial, structure);
            if (rocketPrefab)
            {
                GameObject rocket = Instantiate(rocketPrefab, environment.transform);
                rocket.transform.localPosition = Vector3.up * LaunchEnvironment.RocketBaseHeightMeters;
                RocketVisual = rocket.transform;
            }
            ResetLaunchFraming();
            SetLaunchView();
        }
        private void SetLaunchView()
        {
            IsMapView = false;
            if (mapRoot) mapRoot.SetActive(true);
            if (mapCamera) mapCamera.gameObject.SetActive(false);
            if (launchCamera) launchCamera.gameObject.SetActive(true);
            if (launchCamera)
            {
                var controls = launchCamera.GetComponent<FlightCameraControls>();
                if (!controls) controls = launchCamera.gameObject.AddComponent<FlightCameraControls>();
                controls.director = this;
            }
            Inset?.SynchronizeView();
            RefreshFlightBackdrop();
        }
        private void CaptureLaunchCameraHome()
        {
            if (hasLaunchCameraHome || launchCamera == null) return;
            launchCameraHomePosition = launchCamera.transform.position;
            launchCameraHomeRotation = launchCamera.transform.rotation;
            launchCameraHomeFieldOfView = launchCamera.fieldOfView;
            hasLaunchCameraHome = true;
        }
        public void ResetLaunchFraming()
        {
            CaptureLaunchCameraHome();
            if (launchCamera == null) return;
            launchCamera.transform.SetPositionAndRotation(launchCameraHomePosition, launchCameraHomeRotation);
            launchCamera.fieldOfView = launchCameraHomeFieldOfView;
            launchCamera.GetComponent<FlightCameraControls>()?.ResetFraming();
            Inset?.ResetFraming();
        }
        public void RefreshFlightBackdrop()
        {
            GetComponent<FlightEarthBackdrop>()?.Apply(FlightDriver as RocketFlightDriver, this);
        }
        private void ReleaseEnvironment()
        {
            // Destroy is deferred: hide the previous site immediately so it cannot render
            // over the replacement or keep obsolete bounds alive for the camera.
            if (environment) { environment.SetActive(false); Destroy(environment); }
            environment = null; RocketVisual = null;
            if (groundMaterial) Destroy(groundMaterial);
            groundMaterial = null;
        }
        private void OnPointSelected(double lat, double lon)
        {
            if (pointWeather != null) pointWeather.Fetch(lat, lon);
            RefreshRegion();
            PointSelected?.Invoke();
        }
        private void RefreshRegion()
        {
            if (picker != null && picker.HasSelection && regional != null && mapLayers != null)
                regional.Fetch(picker.Latitude, picker.Longitude, mapLayers.DisplayedLayerId, mapLayers.DisplayedDate);
        }
        private void RequestWeather()
        {
            var site = LaunchSites.All[SelectedSiteIndex];
            if (weather != null) weather.Fetch(site.Latitude, site.Longitude);
            if (nasaWeather != null) nasaWeather.Fetch(site.Latitude, site.Longitude);
        }
        public void Launch()
        {
            if (!CanLaunch) return;
            // A null snapshot explicitly requests the driver's documented offline atmosphere.
            var snapshot = weather != null ? weather.Snapshot : null;
            if (snapshot != null && !snapshot.IsCurrent(DateTime.UtcNow)) snapshot = null;
            FlightDriver.Launch(LaunchSites.All[SelectedSiteIndex], snapshot);
        }
        public void ResetMission() { FlightDriver?.ResetMission(); if (!IsMapView) SetLaunchView(); }
        public void Stage() { FlightDriver?.Stage(); }
        public void SetThrottle(float value) { FlightDriver?.SetThrottle(Mathf.Clamp01(value)); }
        public void SelectLayer(int index) { if (mapLayers != null && index >= 0 && index <= 2) mapLayers.Select(index - 1); }
        public string GetSiteName(int index) => LaunchSites.All[index].Name;
        // Project the actual inertial state onto the scaled map; reject globe-occluded points.
        public bool TryGetRocketScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = default;
            if (!IsMapView || mapCamera == null || !(FlightDriver is RocketFlightDriver driver) || driver.Simulation == null) return false;
            var state = driver.Simulation.State;
            if (!state.IsLaunched) return false;
            var ecef = FlightCore.EarthFrames.InertialToEcef(state.Position, FlightCore.Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            Vector3 point = new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
            Vector3 cameraPosition = mapCamera.transform.position;
            Vector3 delta = point - cameraPosition;
            const double a = 6.378137, b = 6.356752314245;
            double qa = (delta.x * delta.x + delta.z * delta.z) / (a * a) + delta.y * delta.y / (b * b);
            double qb = 2 * ((cameraPosition.x * delta.x + cameraPosition.z * delta.z) / (a * a) + cameraPosition.y * delta.y / (b * b));
            double qc = (cameraPosition.x * cameraPosition.x + cameraPosition.z * cameraPosition.z) / (a * a) + cameraPosition.y * cameraPosition.y / (b * b) - 1;
            double discriminant = qb * qb - 4 * qa * qc;
            if (qa > 0 && discriminant >= 0)
            {
                double t = (-qb - Math.Sqrt(discriminant)) / (2 * qa);
                if (t >= 0 && t < .999999) return false;
            }
            Vector3 screen = mapCamera.WorldToScreenPoint(point);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return false;
            screenPosition = screen; return true;
        }

        public bool TryGetSiteScreenPosition(int index, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (!IsMapView || mapCamera == null || index < 0 || index >= LaunchSites.All.Length) return false;
            var site = LaunchSites.All[index];
            double lat = site.Latitude * Math.PI / 180, lon = site.Longitude * Math.PI / 180;
            const double a = 6.378137, b = 6.356752314245;
            double e2 = 1 - b * b / (a * a), n = a / Math.Sqrt(1 - e2 * Math.Sin(lat) * Math.Sin(lat));
            var p = new Vector3((float)(n * Math.Cos(lat) * Math.Cos(lon)), (float)(n * (1 - e2) * Math.Sin(lat)), (float)(n * Math.Cos(lat) * Math.Sin(lon)));
            var normal = new Vector3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon)));
            if (Vector3.Dot(normal, mapCamera.transform.position - p) <= 0) return false;
            Vector3 screen = mapCamera.WorldToScreenPoint(p);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return false;
            screenPosition = screen; return true;
        }
        private IEnumerator Capture()
        {
            yield return new WaitForSeconds(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "mission-map.png"));
            yield return new WaitForSeconds(1); GoToLaunch(0);
            yield return new WaitForSeconds(2); ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "mission-starbase.png"));
            yield return new WaitForSeconds(1); GoToLaunch(1);
            yield return new WaitForSeconds(2); ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "mission-kennedy.png"));
            yield return new WaitForSeconds(1); ShowMap();
            picker.Select(LaunchSites.All[1].Latitude, LaunchSites.All[1].Longitude);
            yield return new WaitForSeconds(25);
            Debug.Log("ESP_MISSION_WEATHER " + WeatherSummary);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "mission-weather.png"));
            yield return new WaitForSeconds(1); Application.Quit();
        }
        private void OnDestroy() { if (picker != null) picker.PointSelected -= OnPointSelected; if (mapLayers != null) mapLayers.LayerChanged -= RefreshRegion; if (environment) Destroy(environment); if (groundMaterial) Destroy(groundMaterial); }
    }
}
