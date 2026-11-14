using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using ESP.Presentation;
public static class BuildAssetPreview
{
    static Material Mat(string name, Color color, float metallic=0)
    {
        var path="Assets/ESP/Art/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Smoothness",.35f);return m;
    }
    static GameObject Block(string name, Vector3 pos,Vector3 size,Material mat)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.position=pos;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=mat;return o;
    }
    public static void Build()
    {
        if(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ESP_BUILD_PATH")))throw new System.Exception("ESP_BUILD_PATH is required");
        AssetDatabase.Refresh();
        EditorSettings.serializationMode=SerializationMode.ForceText;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var white=Mat("Ceramic",new Color(.86f,.9f,.92f),.3f);
        var dark=Mat("Carbon",new Color(.035f,.055f,.075f));
        var concrete=Mat("Concrete",new Color(.28f,.31f,.32f));
        var teal=Mat("PadMarkings",new Color(.03f,.6f,.65f));
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESP/Art/ESP-Rocket.obj");
        if (!prefab) throw new System.Exception("Rocket import missing");
        var rocket=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(rocket,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);rocket.name="ESP Rocket";rocket.transform.position=new Vector3(0,2,0);
        foreach(var r in rocket.GetComponentsInChildren<Renderer>())r.sharedMaterial=(r.name.Contains("Engine")||r.name.Contains("Interstage")||r.name.Contains("Fin"))?dark:white;
        var bounds=new Bounds();bool first=true;
        foreach(var r in rocket.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        if(bounds.size.y<35||bounds.size.y>42||bounds.size.x>10)throw new System.Exception("Unexpected imported rocket bounds "+bounds.size);
        Debug.Log("ESP_IMPORT_BOUNDS "+bounds.size);
        var view=rocket.AddComponent<RocketPresentation>();view.vehicle=rocket.transform;
        foreach(var t in rocket.GetComponentsInChildren<Transform>())if(t.name.StartsWith("Booster"))view.booster=t;
        foreach(var t in rocket.GetComponentsInChildren<Transform>())Debug.Log("ESP_MESH "+t.name);
        if(view.booster==null)throw new System.Exception("Missing named booster mesh");
        var group=new GameObject("BoosterAssembly").transform;group.SetParent(rocket.transform,false);
        foreach(var t in rocket.GetComponentsInChildren<Transform>())if(t!=group&&(t.name.StartsWith("Booster")||t.name.StartsWith("Engine_")||t.name.StartsWith("Fin_")))t.SetParent(group,true);
        view.booster=group;
        if(group.childCount!=10)throw new System.Exception("Booster assembly must contain 10 meshes, got "+group.childCount);
        PrefabUtility.SaveAsPrefabAsset(rocket,"Assets/ESP/Art/ESP-Rocket.prefab");
        Block("Launch deck",new Vector3(0,0,0),new Vector3(24,1,24),concrete);
        Block("Ground",new Vector3(0,-1,0),new Vector3(700,1,700),Mat("Ground",new Color(.14f,.19f,.18f)));
        Block("Access tower",new Vector3(9,18,4),new Vector3(2,36,2),dark);
        for(int i=0;i<6;i++)Block("Tower platform",new Vector3(9,i*6+2,4),new Vector3(5,.3f,5),teal);
        Block("Pad stripe",new Vector3(0,.52f,-10),new Vector3(20,.03f,.4f),teal);
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.transform.rotation=Quaternion.Euler(35,-35,0);
        RenderSettings.ambientLight=new Color(.35f,.4f,.5f);
        var c=new GameObject("Preview camera").AddComponent<Camera>();c.tag="MainCamera";c.transform.position=new Vector3(55,22,-65);c.transform.LookAt(new Vector3(0,18,0));c.farClipPlane=2000;c.fieldOfView=45;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.04f,.09f,.15f);c.gameObject.AddComponent<PreviewLabel>();
        if(!EditorSceneManager.SaveScene(scene,"Assets/ESP/Art/AssetPreview.unity"))throw new System.Exception("Scene save failed");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/ESP/Art/AssetPreview.unity",true)};
        EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
        AssetDatabase.SaveAssets();
        PlayerSettings.companyName="ESP";PlayerSettings.productName="ESP Asset Preview";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/ESP/Art/AssetPreview.unity"},locationPathName=System.Environment.GetEnvironmentVariable("ESP_BUILD_PATH"),target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Build failed");
        Debug.Log("ESP_ASSET_BUILD_OK");
    }
}
