using System;
using System.IO;
using ESP.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds a collider-free visual prefab from the converted NASA mobile launcher.</summary>
public static class ImportMobileLauncher
{
    private const string DirectoryPath = "Assets/ESP/Art/Sites/NasaMobileLauncher";
    private const string FbxPath = DirectoryPath + "/MobileLauncher.fbx";
    private const string GeneratedPath = DirectoryPath + "/Generated";
    private const string PrefabPath = GeneratedPath + "/MobileLauncher.prefab";
    public const string PrefabName = LaunchEnvironment.ModernLauncherName;
    private const float ExpectedHeight = 121.92f;

    [MenuItem("ESP/Assets/Rebuild NASA mobile launcher")]
    public static void RebuildFromMenu() { Build(); }

    public static GameObject Build()
    {
        if (!File.Exists(FbxPath)) throw new FileNotFoundException("Missing NASA mobile launcher geometry", FbxPath);
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (!importer) throw new InvalidDataException("Unity did not recognize the NASA mobile launcher FBX");
        // The qualified Blender FBX export reaches Unity at 100x its meter
        // coordinates. Correct that import conversion; the bounds below verify it.
        importer.globalScale = .01f;
        importer.useFileScale = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.preserveHierarchy = true;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!source || !shader) throw new InvalidOperationException("Missing NASA FBX or URP Lit shader");
        if (!AssetDatabase.IsValidFolder(GeneratedPath)) AssetDatabase.CreateFolder(DirectoryPath, "Generated");
        var body = CreateMaterial("MobileLauncherBase", shader, false);
        var railings = CreateMaterial("MobileLauncherRailings", shader, true);

        GameObject model = null;
        try
        {
            model = UnityEngine.Object.Instantiate(source);
            model.name = PrefabName;
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            model.AddComponent<MobileLauncherIdentity>();
            int bodyCount = 0, railingCount = 0;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name == "ML_Base") { renderer.sharedMaterial = body; bodyCount++; }
                else if (renderer.name == "ML_Railings") { renderer.sharedMaterial = railings; railingCount++; }
                else throw new InvalidDataException("Unexpected NASA launcher mesh: " + renderer.name);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            if (bodyCount != 1 || railingCount != 1)
                throw new InvalidDataException("Expected one launcher body and one railing mesh");
            if (model.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidDataException("NASA launcher is visual scenery and must not have colliders");
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (Mathf.Abs(bounds.size.y - ExpectedHeight) > .3f || Mathf.Abs(bounds.min.y) > .1f)
                throw new InvalidDataException("Unexpected mobile launcher bounds: " + bounds);
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, PrefabPath);
            if (!prefab) throw new IOException("Could not save NASA mobile launcher prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("ESP_NASA_MOBILE_LAUNCHER_IMPORTED " + bounds);
            return prefab;
        }
        finally
        {
            if (model) UnityEngine.Object.DestroyImmediate(model);
        }
    }

    private static Material CreateMaterial(string name, Shader shader, bool transparent)
    {
        string texturePath = DirectoryPath + "/" + name + ".png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (!texture) throw new FileNotFoundException("Missing NASA launcher texture", texturePath);
        string path = GeneratedPath + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", .3f);
        if (transparent)
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);
        return material;
    }
}
