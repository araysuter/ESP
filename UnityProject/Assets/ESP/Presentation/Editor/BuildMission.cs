using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using ESP.Presentation;

public static class BuildMission
{
    public static void Build()
    {
        string buildPath = Environment.GetEnvironmentVariable("ESP_BUILD_PATH");
        if (string.IsNullOrEmpty(buildPath)) throw new Exception("ESP_BUILD_PATH is required");
        EditorSettings.serializationMode = SerializationMode.ForceText;
        ImportDetailedVehicles.Build();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ESP/Art/Earth/EarthMesh.asset");
        var earthMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ESP/Art/Earth/Earth.mat");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Earth/BlueMarble.jpg");
        var rocket = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESP/Art/ESP-Rocket.prefab");
        if (!mesh || !earthMaterial || !texture || !rocket) throw new Exception("Missing Earth mesh, material, texture, or rocket prefab");
        var globe = new GameObject("Earth map");
        globe.AddComponent<MeshFilter>().sharedMesh = mesh;
        globe.AddComponent<MeshRenderer>().sharedMaterial = earthMaterial;
        var map = new GameObject("Map camera").AddComponent<Camera>();
        map.tag = "MainCamera"; map.transform.position = new Vector3(3, 8, -18); map.transform.LookAt(Vector3.zero);
        map.nearClipPlane = .01f; map.farClipPlane = 100; map.fieldOfView = 45;
        map.clearFlags = CameraClearFlags.SolidColor; map.backgroundColor = new Color(.002f, .005f, .014f);
        var controls = map.gameObject.AddComponent<GlobeCameraControls>(); controls.useMissionUI = true;
        var launch = new GameObject("Launch camera").AddComponent<Camera>();
        launch.transform.position = new Vector3(75, 38, -90); launch.transform.LookAt(new Vector3(0, 18, 0));
        launch.nearClipPlane = .1f; launch.farClipPlane = 4000; launch.fieldOfView = 45;
        launch.clearFlags = CameraClearFlags.SolidColor; launch.backgroundColor = new Color(.11f, .20f, .29f);
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f;
        sun.transform.rotation = Quaternion.Euler(35, -35, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.25f, .29f, .32f);
        var manager = new GameObject("Mission");
        var layers = manager.AddComponent<NasaMapLayers>(); layers.earth = globe.GetComponent<Renderer>(); layers.offlineTexture = texture; layers.showLegacyUI = false;
        var weather = manager.AddComponent<LaunchWeatherProfile>();
        var nasaWeather = manager.AddComponent<NasaWeatherProfile>();
        var pointWeather = manager.AddComponent<PointWeather>();
        var regional = manager.AddComponent<NasaRegionalPreview>();
        var events = manager.AddComponent<NasaEventFeed>();
        var spaceWeather = manager.AddComponent<NasaSpaceWeather>();
        var picker = map.gameObject.AddComponent<GlobePointPicker>(); picker.useMissionUI = true;
        var director = manager.AddComponent<MissionDirector>(); director.mapCamera = map; director.launchCamera = launch; director.mapRoot = globe;
        director.vehiclePrefabs = new[] { rocket, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESP/Art/Vehicles/ESP-Starship.prefab"), AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESP/Art/Vehicles/ESP-SLS.prefab") };
        foreach (var vehicle in director.vehiclePrefabs) if (!vehicle) throw new Exception("Missing vehicle prefab");
        director.mapLayers = layers; director.weather = weather; director.nasaWeather = nasaWeather; director.rocketPrefab = rocket;
        manager.AddComponent<MissionFlightSmoke>();
        var flight = manager.AddComponent<RocketFlightDriver>(); flight.director = director; flight.nasaWeather = nasaWeather;
        var backdrop = manager.AddComponent<FlightEarthBackdrop>();
        backdrop.driver = flight; backdrop.director = director; backdrop.globeMesh = mesh; backdrop.globeMaterial = earthMaterial;
        director.picker = picker; director.pointWeather = pointWeather; director.regional = regional;
        director.events = events; director.spaceWeather = spaceWeather;
        var groundPath = "Assets/ESP/Art/Sites/Ground.mat";
        var ground = AssetDatabase.LoadAssetAtPath<Material>(groundPath);
        if (!ground) { ground = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(ground, groundPath); }
        director.siteGroundTemplate = ground;
        director.siteGroundTextures = new Texture[] { AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Sites/starbase.jpg"), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Sites/cape.jpg") };
        var panelPath = "Assets/ESP/Art/MissionPanelSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(settings, panelPath);
        }
        settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/ESP/Art/MissionTheme.tss");
        if (!settings.themeStyleSheet) throw new Exception("Missing runtime theme");
        var uiObject = new GameObject("Mission UI"); var document = uiObject.AddComponent<UIDocument>(); document.panelSettings = settings;
        uiObject.AddComponent<MissionUI>().director = director;
        const string scenePath = "Assets/ESP/Art/Mission.unity";
        if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new Exception("Could not save mission scene");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets(); PlayerSettings.productName = "ESP";
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720; PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, locationPathName = buildPath, target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Mission build failed");
        Debug.Log("ESP_MISSION_BUILD_OK");
    }
}
