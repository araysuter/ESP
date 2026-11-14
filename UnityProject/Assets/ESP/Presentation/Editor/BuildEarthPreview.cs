using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using ESP.Presentation;
public static class BuildEarthPreview
{
    public static void Build()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Earth/BlueMarble.jpg");
        if(!tex||tex.width!=2048||tex.height!=1024)throw new Exception("Expected bundled NASA 2048x1024 image");
        var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/ESP/Art/Earth/BlueMarble.jpg");
        importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var mesh=EarthEllipsoid.BuildMesh();
        if(Math.Abs(mesh.bounds.size.x-12.756274f)>.00001f||Math.Abs(mesh.bounds.size.y-12.713505f)>.00001f)throw new Exception("WGS84 bounds mismatch");
        // Check winding against outward normals away from the degenerate pole triangles.
        var v=mesh.vertices;var tr=mesh.triangles;var norms=mesh.normals;
        for(int i=0;i<tr.Length;i+=3){var n=Vector3.Cross(v[tr[i+1]]-v[tr[i]],v[tr[i+2]]-v[tr[i]]);if(n.sqrMagnitude>1e-12f&&Vector3.Dot(n,norms[tr[i]])<=0)throw new Exception("Inward globe triangle");}
        Debug.Log("ESP_EARTH_GEOMETRY_OK");
        var globe=new GameObject("WGS84 Earth - map scale");globe.AddComponent<MeshFilter>().sharedMesh=mesh;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.mainTexture=tex;material.SetFloat("_Smoothness",.05f);
        var matPath="Assets/ESP/Art/Earth/Earth.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(old){EditorUtility.CopySerialized(material,old);UnityEngine.Object.DestroyImmediate(material);material=old;}else AssetDatabase.CreateAsset(material,matPath);
        globe.AddComponent<MeshRenderer>().sharedMaterial=material;globe.AddComponent<EarthEllipsoid>();
        // Serialize the generated mesh into an asset so the scene works in editor and player.
        var meshPath="Assets/ESP/Art/Earth/EarthMesh.asset";var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(oldMesh){EditorUtility.CopySerialized(mesh,oldMesh);globe.GetComponent<MeshFilter>().sharedMesh=oldMesh;UnityEngine.Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,meshPath);
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.LookRotation(new Vector3(-1,-.25f,.5f));
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.1f,.12f,.16f);
        var camera=new GameObject("Globe Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(3,8,-18);camera.transform.LookAt(Vector3.zero);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=45;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.002f,.005f,.014f);camera.gameObject.AddComponent<EarthPreviewLabel>();
        camera.gameObject.AddComponent<GlobeCameraControls>();
        var layers=camera.gameObject.AddComponent<NasaMapLayers>();layers.earth=globe.GetComponent<Renderer>();layers.offlineTexture=tex;layers.cloudLegend=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ESP/Art/Earth/CloudFractionLegend.png");
        camera.gameObject.AddComponent<NasaEventFeed>();
        camera.gameObject.AddComponent<NasaSpaceWeather>();
        camera.gameObject.AddComponent<NasaEventPanel>();
        camera.gameObject.AddComponent<GlobePointPicker>();
        camera.gameObject.AddComponent<PointWeather>();
        camera.gameObject.AddComponent<NasaRegionalPreview>();
        camera.gameObject.AddComponent<LocationInspector>();
        if(!EditorSceneManager.SaveScene(scene,"Assets/ESP/Art/Earth/EarthPreview.unity"))throw new Exception("Scene save failed");AssetDatabase.SaveAssets();
        PlayerSettings.productName="ESP Earth Preview";
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/ESP/Art/Earth/EarthPreview.unity"},locationPathName=Environment.GetEnvironmentVariable("ESP_BUILD_PATH"),target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Earth build failed");
        Debug.Log("ESP_EARTH_BUILD_OK");
    }
}
