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
        public NasaMapLayers mapLayers;
        public LaunchWeatherProfile weather;
        public GlobePointPicker picker;
        public PointWeather pointWeather;
        public NasaRegionalPreview regional;
        public NasaEventFeed events;
        public NasaSpaceWeather spaceWeather;
        public event Action PointSelected;
        public GameObject rocketPrefab;
        public Texture[] siteGroundTextures;
        public Material siteGroundTemplate;
        public IMissionFlightDriver FlightDriver { get; set; }
        public Transform RocketVisual { get; private set; }
        public bool IsMapView { get; private set; } = true;
        public int SelectedSiteIndex { get; private set; }
        public string SiteName => LaunchSites.All[SelectedSiteIndex].Name;
        public string FlightStatus => FlightDriver == null ? "Flight simulation unavailable" : FlightDriver.Status;
        public string WeatherSummary => picker != null && picker.HasSelection && pointWeather != null
            ? string.Format("{0:F2}°, {1:F2}°\n{2}\n{3}\n{4}\n{5}", picker.Latitude, picker.Longitude, pointWeather.Status, pointWeather.validtime, pointWeather.Details, regional != null ? regional.Status : "")
            : (weather != null ? weather.Status : "Weather unavailable");
        public Texture RegionalImage => regional != null ? regional.Image : null;
        public string MapCredit => mapLayers != null ? "NASA GIBS / Blue Marble • " + mapLayers.DisplayedLayerId + " • " + (string.IsNullOrEmpty(mapLayers.DisplayedDate) ? "static composite" : mapLayers.DisplayedDate) + " • " + mapLayers.Status : "NASA Blue Marble";
        public string NasaStatus => "NASA EONET: " + (events != null ? events.Status : "unavailable") + "\nNASA DONKI: " + (spaceWeather != null ? spaceWeather.Status : "unavailable");
        public double AltitudeMeters => FlightDriver?.AltitudeMeters ?? 0;
        public double SpeedMetersPerSecond => FlightDriver?.SpeedMetersPerSecond ?? 0;
        public double MissionTimeSeconds => FlightDriver?.MissionTimeSeconds ?? 0;
        public float Throttle => FlightDriver?.Throttle ?? 0;
        public bool CanLaunch => FlightDriver != null && FlightDriver.CanLaunch;
        private GameObject environment;
        private Material groundMaterial;

        private void Start()
        {
            if (picker != null) picker.PointSelected += OnPointSelected;
            if (mapLayers != null) mapLayers.LayerChanged += RefreshRegion;
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
            if (environment) environment.SetActive(false);
        }

        public void GoToLaunch(int siteIndex)
        {
            if (siteIndex < 0 || siteIndex >= LaunchSites.All.Length) throw new ArgumentOutOfRangeException(nameof(siteIndex));
            bool siteChanged = SelectedSiteIndex != siteIndex;
            SelectedSiteIndex = siteIndex;
            if (siteChanged) { FlightDriver?.ResetMission(); RequestWeather(); }
            if (environment && !siteChanged)
            {
                environment.SetActive(true);
                SetLaunchView();
                return;
            }
            if (environment) Destroy(environment);
            if (groundMaterial) Destroy(groundMaterial);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            groundMaterial = siteGroundTemplate != null
                ? new Material(siteGroundTemplate) : new Material(shader);
            groundMaterial.color = Color.white;
            groundMaterial.SetFloat("_Smoothness", 0);
            if (siteGroundTextures != null && siteIndex < siteGroundTextures.Length && siteGroundTextures[siteIndex]) { groundMaterial.mainTexture = siteGroundTextures[siteIndex]; }
            else groundMaterial.color = new Color(.22f, .29f, .24f);
            environment = LaunchEnvironment.Build(LaunchSites.All[siteIndex], groundMaterial);
            if (rocketPrefab)
            {
                GameObject rocket = Instantiate(rocketPrefab, environment.transform);
                rocket.transform.localPosition = Vector3.up * LaunchEnvironment.RocketBaseHeightMeters;
                RocketVisual = rocket.transform;
            }
            SetLaunchView();
        }
        private void SetLaunchView()
        {
            IsMapView = false;
            if (mapRoot) mapRoot.SetActive(false);
            if (mapCamera) mapCamera.gameObject.SetActive(false);
            if (launchCamera) launchCamera.gameObject.SetActive(true);
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
            if (weather != null) { var site = LaunchSites.All[SelectedSiteIndex]; weather.Fetch(site.Latitude, site.Longitude); }
        }
        public void Launch()
        {
            if (!CanLaunch) return;
            // A null snapshot explicitly requests the driver's documented offline atmosphere.
            var snapshot = weather != null ? weather.Snapshot : null;
            if (snapshot != null && !snapshot.IsCurrent(DateTime.UtcNow)) snapshot = null;
            FlightDriver.Launch(LaunchSites.All[SelectedSiteIndex], snapshot);
        }
        public void ResetMission() { FlightDriver?.ResetMission(); }
        public void Stage() { FlightDriver?.Stage(); }
        public void SetThrottle(float value) { FlightDriver?.SetThrottle(Mathf.Clamp01(value)); }
        public void SelectLayer(int index) { if (mapLayers != null && index >= 0 && index <= 2) mapLayers.Select(index - 1); }
        public string GetSiteName(int index) => LaunchSites.All[index].Name;
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
            picker.Select(28.608422, -80.604133);
            yield return new WaitForSeconds(25);
            Debug.Log("ESP_MISSION_WEATHER " + WeatherSummary);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "mission-weather.png"));
            yield return new WaitForSeconds(1); Application.Quit();
        }
        private void OnDestroy() { if (picker != null) picker.PointSelected -= OnPointSelected; if (mapLayers != null) mapLayers.LayerChanged -= RefreshRegion; if (environment) Destroy(environment); if (groundMaterial) Destroy(groundMaterial); }
    }
}
