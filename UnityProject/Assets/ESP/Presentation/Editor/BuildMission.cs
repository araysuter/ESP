using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;
using ESP.Presentation;

public static class BuildMission
{
    public static void Build()
    {
        string buildPath = Argument("-espBuildPath") ?? Environment.GetEnvironmentVariable("ESP_BUILD_PATH");
        bool release = Array.IndexOf(Environment.GetCommandLineArgs(), "-espRelease") >= 0 || Environment.GetEnvironmentVariable("ESP_RELEASE_BUILD") == "1";
        string version = Argument("-espVersion");
        if (version != null) PlayerSettings.bundleVersion = version;
        if (!string.IsNullOrEmpty(buildPath) && !Path.IsPathRooted(buildPath))
            buildPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", buildPath));
        if (string.IsNullOrEmpty(buildPath)) throw new Exception("ESP_BUILD_PATH is required");
        // Keep the existing macOS development default, while allowing a native
        // Windows editor to produce an .exe. CI supplies its active target.
        var target = release ? EditorUserBuildSettings.activeBuildTarget : DevelopmentTarget();
        if (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneWindows64)
            throw new Exception("Mission builds support macOS and Windows x64.");
        string extension = target == BuildTarget.StandaloneWindows64 ? ".exe" : ".app";
        if (!buildPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new Exception("The " + target + " output path must end in " + extension);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new Exception("Unity build support is not installed for " + target);
        ESP.Presentation.Editor.CloudBlendChecks.Validate();
        ESP.Presentation.Editor.NasaImageryTimeChecks.Validate();
        ESP.Presentation.Editor.KeyboardCameraChecks.Validate();
        WeatherHeightChecks.Validate();
        ESP.Presentation.Editor.SmokeEmissionScheduleChecks.Run();
        ESP.Presentation.Editor.LaunchGroundEffectsChecks.Run();
        EditorSettings.serializationMode = SerializationMode.ForceText;
        ImportDetailedVehicles.Build();
        var starbaseTower = ImportStarbaseTower.Build();
        var nasaGantry = ImportLaunchStructures.Build();
        var nasaMobileLauncher = ImportMobileLauncher.Build();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ESP/Art/Earth/EarthMesh.asset");
        // Regenerate the display ellipsoid at the qualified orbital-view tessellation.
        var detailedMesh = EarthEllipsoid.BuildMesh();
        if (mesh) { EditorUtility.CopySerialized(detailedMesh, mesh); UnityEngine.Object.DestroyImmediate(detailedMesh); }
        else { mesh = detailedMesh; AssetDatabase.CreateAsset(mesh, "Assets/ESP/Art/Earth/EarthMesh.asset"); }
        EditorUtility.SetDirty(mesh);
        var earthMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ESP/Art/Earth/Earth.mat");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Earth/BlueMarble.jpg");
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/ESP/Art/Earth/BlueMarble.jpg");
        textureImporter.maxTextureSize = 8192; textureImporter.anisoLevel = 8;
        textureImporter.SaveAndReimport();
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Earth/BlueMarble.jpg");
        if (texture.width != 8192 || texture.height != 4096) throw new Exception("Expected NASA 8192x4096 base texture");
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
        var starShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Presentation/StarField.shader");
        if (!starShader) throw new Exception("Missing star field shader");
        // Serialized scene reference preserves the shader without runtime lookup.
        const string skyPath = "Assets/ESP/Art/Sky/CelestialSky.exr";
        var skyImporter = (TextureImporter)AssetImporter.GetAtPath(skyPath);
        if (!skyImporter) throw new Exception("Missing offline NASA sky texture");
        skyImporter.textureShape = TextureImporterShape.Texture2D;
        skyImporter.sRGBTexture = false; skyImporter.mipmapEnabled = true; skyImporter.isReadable = false;
        skyImporter.maxTextureSize = 8192; skyImporter.alphaSource = TextureImporterAlphaSource.None;
        skyImporter.wrapModeU = TextureWrapMode.Repeat; skyImporter.wrapModeV = TextureWrapMode.Clamp;
        skyImporter.filterMode = FilterMode.Trilinear; skyImporter.anisoLevel = 1;
        var skyPlatform = skyImporter.GetPlatformTextureSettings("Standalone");
        skyPlatform.overridden = true; skyPlatform.maxTextureSize = 8192;
        skyPlatform.format = TextureImporterFormat.BC6H;
        skyImporter.SetPlatformTextureSettings(skyPlatform); skyImporter.SaveAndReimport();
        var sky = AssetDatabase.LoadAssetAtPath<Texture2D>(skyPath);
        if (!sky || sky.width != 8192 || sky.height != 4096) throw new Exception("Expected 8192x4096 NASA sky");
        var stars = map.gameObject.AddComponent<StarFieldSetup>(); stars.shader = starShader; stars.sky = sky;
        var launch = new GameObject("Launch camera").AddComponent<Camera>();
        launch.transform.position = new Vector3(75, 38, -90); launch.transform.LookAt(new Vector3(0, 18, 0));
        launch.nearClipPlane = .1f; launch.farClipPlane = 300000; launch.fieldOfView = 45;
        if (!launch.GetUniversalAdditionalCameraData().clearDepth) throw new Exception("Flight overlay must clear its depth buffer");
        launch.clearFlags = CameraClearFlags.SolidColor; launch.backgroundColor = new Color(.11f, .20f, .29f);
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f;
        sun.cullingMask = ~(1 << 31);
        sun.transform.rotation = Quaternion.Euler(35, -35, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.25f, .29f, .32f);
        var manager = new GameObject("Mission");
        var layers = manager.AddComponent<NasaMapLayers>(); layers.earth = globe.GetComponent<Renderer>(); layers.offlineTexture = texture; layers.showLegacyUI = false;
        layers.cloudShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Art/CloudShell.shader");
        if (!layers.cloudShader) throw new Exception("Missing observed cloud shader");
        var weather = manager.AddComponent<LaunchWeatherProfile>();
        var nasaWeather = manager.AddComponent<NasaWeatherProfile>();
        var pointWeather = manager.AddComponent<PointWeather>();
        var regional = manager.AddComponent<NasaRegionalPreview>();
        var events = manager.AddComponent<NasaEventFeed>();
        var spaceWeather = manager.AddComponent<NasaSpaceWeather>();
        var picker = map.gameObject.AddComponent<GlobePointPicker>(); picker.useMissionUI = true;
        var director = manager.AddComponent<MissionDirector>(); director.mapCamera = map; director.launchCamera = launch; director.mapRoot = globe;
        // Historical Apollo/Shuttle geometry is an explicit asset-review option,
        // never represented as the modern SLS mobile launcher in a normal build.
        bool historicalGantryReview = Environment.GetEnvironmentVariable("ESP_HISTORICAL_GANTRY_REVIEW") == "1";
        director.siteStructurePrefabs = new GameObject[] { starbaseTower, historicalGantryReview ? nasaGantry : nasaMobileLauncher };
        director.vehiclePrefabs = new[] { rocket, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESP/Art/Vehicles/ESP-Starship.prefab"), AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESP/Art/Vehicles/ESP-SLS.prefab") };
        foreach (var vehicle in director.vehiclePrefabs) if (!vehicle) throw new Exception("Missing vehicle prefab");
        director.mapLayers = layers; director.weather = weather; director.nasaWeather = nasaWeather; director.rocketPrefab = rocket;
        manager.AddComponent<MissionFlightSmoke>();
        var flight = manager.AddComponent<RocketFlightDriver>(); flight.director = director; flight.nasaWeather = nasaWeather;
        var explosion = manager.AddComponent<RocketExplosion>();
        explosion.driver = flight; explosion.director = director;
        explosion.effectShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Presentation/RocketExplosion.shader");
        if (!explosion.effectShader) throw new Exception("Missing impact effect shader");
        manager.AddComponent<RocketExplosionSmoke>();
        var smoke = manager.AddComponent<RocketSmokeTrail>();
        smoke.driver = flight; smoke.director = director;
        smoke.smokeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Presentation/RocketSmokeTrail.shader");
        if (!smoke.smokeShader) throw new Exception("Missing SRB smoke shader");
        var groundCloud = manager.AddComponent<LaunchGroundEffects>();
        groundCloud.driver = flight; groundCloud.director = director; groundCloud.smokeShader = smoke.smokeShader;
        var backdrop = manager.AddComponent<FlightEarthBackdrop>();
        backdrop.driver = flight; backdrop.director = director; backdrop.globeMesh = mesh; backdrop.globeMaterial = earthMaterial;
        backdrop.atmosphereShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Art/ContinuousAtmosphere.shader");
        backdrop.starShader = starShader; backdrop.celestialSky = sky; backdrop.earthSun = sun;
        var sunlight = manager.AddComponent<MissionSunlight>();
        sunlight.director = director; sunlight.driver = flight; sunlight.sun = sun;
        smoke.sunlight = sunlight;
        backdrop.sunlight = sunlight;
        var surface = manager.AddComponent<LocalSurfaceTiles>();
        surface.driver = flight; surface.director = director; surface.surfaceSun = sun;
        surface.surfaceShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Presentation/LocalSurface.shader");
        ESP.Presentation.Editor.NightLightsAssets.Configure(earthMaterial, sunlight, surface);
        if (!surface.surfaceShader || !backdrop.atmosphereShader) throw new Exception("Missing continuous Earth shaders");
        director.picker = picker; director.pointWeather = pointWeather; director.regional = regional;
        director.events = events; director.spaceWeather = spaceWeather;
        var groundPath = "Assets/ESP/Art/Sites/Ground.mat";
        var ground = AssetDatabase.LoadAssetAtPath<Material>(groundPath);
        if (!ground) { ground = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(ground, groundPath); }
        director.siteGroundTemplate = ground;
        // This serialized material reference keeps the unlit shader in the player.
        // The orbit renderer clones it and supplies its own color, without terrain textures.
        director.orbitMaterial = ground;
        director.siteGroundTextures = new Texture[] { AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Sites/starbase.jpg"), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Sites/cape.jpg") };
        var panelPath = "Assets/ESP/Art/MissionPanelSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(settings, panelPath);
        }
        // Scale to the rendered viewport, not the host display's reported physical DPI.
        // Expand keeps the complete cockpit available at different aspect ratios.
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1280, 720);
        settings.screenMatchMode = PanelScreenMatchMode.Expand;
        settings.scale = 1;
        EditorUtility.SetDirty(settings);
        settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/ESP/Art/MissionTheme.tss");
        if (!settings.themeStyleSheet) throw new Exception("Missing runtime theme");
        var uiObject = new GameObject("Mission UI"); var document = uiObject.AddComponent<UIDocument>(); document.panelSettings = settings;
        uiObject.AddComponent<MissionUI>().director = director;
        const string scenePath = "Assets/ESP/Art/Mission.unity";
        if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new Exception("Could not save mission scene");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets(); PlayerSettings.productName = "ESP";
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720; PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        if (release)
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            if (target == BuildTarget.StandaloneOSX)
                EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", "x64ARM64");
        }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, locationPathName = buildPath, target = target, options = release ? BuildOptions.None : BuildOptions.Development });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Mission build failed");
        // GameCI validates this exact marker after the custom build method returns.
        Debug.Log("Build succeeded!");
        Debug.Log("ESP_MISSION_BUILD_OK version=" + PlayerSettings.bundleVersion + " target=" + target + " release=" + release);
    }
    private static BuildTarget DevelopmentTarget()
    {
#if UNITY_EDITOR_WIN
        return BuildTarget.StandaloneWindows64;
#else
        return BuildTarget.StandaloneOSX;
#endif
    }
    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-")) throw new ArgumentException("Missing value for " + name);
        return args[index + 1];
    }

}
