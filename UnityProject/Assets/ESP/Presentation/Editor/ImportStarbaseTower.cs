using System;
using System.IO;
using ESP.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class ImportStarbaseTower
{
    private const string GeneratedDirectory = "Assets/ESP/Art/Sites/GeneratedStarbaseTower";
    private const int LevelCount = 23;
    private const float LevelSpacingMeters = 5.67231f;
    private static readonly string[] Modules =
        { "Foundation", "LaunchMount", "TowerLevel", "TowerTop", "CatchArms", "QuickDisconnect" };

    public static GameObject Build()
    {
        string sourceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../AssetsSource/StarbaseTower"));
        Directory.CreateDirectory(GeneratedDirectory);
        foreach (string module in Modules)
        {
            foreach (string extension in new[] { ".obj", ".mtl" })
                File.Copy(Path.Combine(sourceDirectory, module + extension),
                    GeneratedDirectory + "/" + module + extension, true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("Starbase tower requires the URP Lit shader.");
        Material steel = MaterialFor("Tower steel", shader, new Color(.36f, .39f, .42f), .35f);
        Material concrete = MaterialFor("Tower foundation", shader, new Color(.44f, .44f, .41f), 0f);

        var assembly = new GameObject(LaunchEnvironment.StarbaseTowerName);
        try
        {
            foreach (string module in Modules)
            {
                string modelPath = GeneratedDirectory + "/" + module + ".obj";
                if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer))
                    throw new InvalidDataException("Unity could not import Starbase module " + module);
                importer.globalScale = 1;
                importer.useFileScale = false;
                importer.addCollider = false;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.indexFormat = ModelImporterIndexFormat.UInt32;
                importer.SaveAndReimport();

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (!model) throw new InvalidDataException("Missing imported Starbase module " + module);
                bool isLevel = module == "TowerLevel";
                for (int i = 0; i < (isLevel ? LevelCount : 1); i++)
                {
                    // The OBJ vertices already share the mount's ground-level origin.
                    // All level instances reference the same source mesh.
                    var instance = Object.Instantiate(model, assembly.transform, false);
                    instance.name = isLevel ? $"Tower level {i + 1:00}" : module;
                    instance.transform.localPosition = Vector3.up * (i * LevelSpacingMeters);
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length == 0) throw new InvalidDataException("Empty Starbase module " + module);
                    foreach (var renderer in renderers)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh)
                            throw new InvalidDataException("Missing mesh for Starbase module " + module);
                        var slots = new Material[filter.sharedMesh.subMeshCount];
                        for (int slot = 0; slot < slots.Length; slot++)
                            slots[slot] = module == "Foundation" ? concrete : steel;
                        renderer.sharedMaterials = slots;
                    }
                }
            }

            var renderersInAssembly = assembly.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderersInAssembly[0].bounds;
            foreach (var renderer in renderersInAssembly) bounds.Encapsulate(renderer.bounds);
            if (Mathf.Abs(bounds.min.y + 2f) > .1f || Mathf.Abs(bounds.max.y - 136.75f) > .1f)
                throw new InvalidDataException("Starbase tower import changed the expected vertical bounds: " + bounds);
            var prefab = PrefabUtility.SaveAsPrefabAsset(assembly, GeneratedDirectory + "/StarbaseTower.prefab");
            if (!prefab) throw new IOException("Could not save the Starbase tower prefab.");
            return prefab;
        }
        finally { Object.DestroyImmediate(assembly); }
    }

    private static Material MaterialFor(string name, Shader shader, Color color, float metallic)
    {
        string path = GeneratedDirectory + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", .3f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }
}
