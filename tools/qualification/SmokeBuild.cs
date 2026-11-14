using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SmokeBuild
{
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Smoke Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 2, -7);
        camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.06f, 0.12f);
        camera.gameObject.tag = "MainCamera";
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Rotating Qualification Cube";
        cube.AddComponent<SmokeMotion>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.Exception("URP Lit shader missing");
        var material = new Material(shader);
        material.color = new Color(0.15f, 0.8f, 0.65f);
        AssetDatabase.CreateAsset(material, "Assets/SmokeMaterial.mat");
        cube.GetComponent<Renderer>().sharedMaterial = material;
        EditorSceneManager.SaveScene(scene, "Assets/Smoke.unity");
        PlayerSettings.companyName = "ESP";
        PlayerSettings.productName = "ESP Qualification";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var output = System.Environment.GetEnvironmentVariable("ESP_SMOKE_BUILD_PATH");
        if (string.IsNullOrEmpty(output)) throw new System.Exception("Set ESP_SMOKE_BUILD_PATH");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Smoke.unity" }, locationPathName = output,
            target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
        });
        Debug.Log($"ESP_SMOKE_BUILD result={report.summary.result} bytes={report.summary.totalSize}");
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Smoke build failed");
    }
}
