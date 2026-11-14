using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ESP.Presentation;

/// <summary>Creates runtime-ready prefabs from the authored Blender OBJ exports.</summary>
public static class ImportDetailedVehicles
{
    private const string DirectoryPath = "Assets/ESP/Art/Vehicles";
    [MenuItem("ESP/Assets/Rebuild downloaded vehicle prefabs")]
    public static void Build()
    {
        BuildSls();
        BuildStarship();
    }
    public static void BuildSls() { BuildSingle("SLS/SLS-Block1.obj", "ESP-SLS.prefab", false); }
    public static void BuildStarship() { BuildSingle("Starship/ESP-Starship.obj", "ESP-Starship.prefab", true); }
    private static void BuildSingle(string source, string destination, bool starship)
    {
        AssetDatabase.Refresh();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ESP/Art/Plume.shader");
        if (!shader) throw new Exception("Missing plume shader");
        BuildOne(source, destination, starship, shader);
        AssetDatabase.SaveAssets();
        Debug.Log("ESP_DETAILED_VEHICLE_IMPORTED " + destination);
    }
    private static void BuildOne(string source, string destination, bool starship, Shader shader)
    {
        GameObject root;
        if (starship)
        {
            var files = LoadStarshipManifest().model_files;
            if (files == null || files.Length < 1 || files.Length > 32)
                throw new Exception("Starship manifest must contain 1–32 model parts");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in files)
                if (file == null || !System.Text.RegularExpressions.Regex.IsMatch(file, @"\AESP-Starship-Part-[0-9]{3}\.obj\z") || !seen.Add(file))
                    throw new Exception("Unexpected or duplicate Starship model filename");
            root = new GameObject("ESP Starship visual");
            foreach (string file in files)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(DirectoryPath + "/Starship/" + file);
                if (!model) throw new Exception("Missing Starship stage model " + file);
                var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                child.transform.SetParent(root.transform, false);
            }
        }
        else
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DirectoryPath + "/" + source);
            if (!model) throw new Exception("Missing model " + source);
            root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        root.name = starship ? "ESP Starship visual" : "ESP SLS visual";
        var booster = new GameObject("BoosterAssembly").transform;
        booster.SetParent(root.transform, false);
        var upper = new GameObject("UpperAssembly").transform;
        upper.SetParent(root.transform, false);
        var leftSrb = new GameObject("LeftSRB").transform; leftSrb.SetParent(root.transform, false);
        var rightSrb = new GameObject("RightSRB").transform; rightSrb.SetParent(root.transform, false);
        int lowerCount = 0, upperCount = 0, leftCount = 0, rightCount = 0;
        Bounds bounds = default; bool first = true;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            string name = renderer.gameObject.name;
            bool isLeft = !starship && name.StartsWith("LeftSRB");
            bool isRight = !starship && name.StartsWith("RightSRB");
            bool isLower = starship ? name.StartsWith("Booster") : name.StartsWith("CoreAssembly");
            bool isUpper = starship ? name.StartsWith("Ship") : name.StartsWith("UpperAssembly");
            if (!isLower && !isUpper && !isLeft && !isRight) throw new Exception("Unrecognized model group: " + name);
            renderer.transform.SetParent(isLeft ? leftSrb : isRight ? rightSrb : isLower ? booster : upper, true);
            if (isLower) lowerCount++; else if (isLeft) leftCount++; else if (isRight) rightCount++; else upperCount++;
            if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = Palette(materials[i], starship);
            renderer.sharedMaterials = materials;
        }
        float expected = starship ? LoadStarshipManifest().height_m : 98.3f;
        if (float.IsNaN(expected) || float.IsInfinity(expected) || expected <= 0) throw new Exception("Invalid manifest height");
        if (lowerCount == 0 || upperCount == 0 || (!starship && (leftCount == 0 || rightCount == 0)) || first || Mathf.Abs(bounds.size.y - expected) > 3f)
            throw new Exception("Model grouping or bounds invalid: " + source + " " + bounds.size + " groups " + lowerCount + "/" + upperCount);
        CombineStage(booster, starship ? "Starship-booster" : "SLS-booster");
        CombineStage(upper, starship ? "Starship-upper" : "SLS-upper");
        if (!starship) { CombineStage(leftSrb, "SLS-left-srb"); CombineStage(rightSrb, "SLS-right-srb"); }
        else { UnityEngine.Object.DestroyImmediate(leftSrb.gameObject); UnityEngine.Object.DestroyImmediate(rightSrb.gameObject); }
        var view = root.AddComponent<RocketPresentation>(); view.vehicle = root.transform; view.booster = booster;
        if (starship) { StarshipPlumes(booster, upper, shader); }
        else { SlsPlumes(booster, upper, leftSrb, rightSrb, shader); }
        string path = DirectoryPath + "/" + destination;
        if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new Exception("Could not save " + path);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log("ESP_VEHICLE_IMPORT_OK " + path + " " + bounds.size);
    }
    private static void CombineStage(Transform stage, string assetPrefix)
    {
        var grouped = new Dictionary<Material, List<CombineInstance>>();
        var oldObjects = new List<GameObject>();
        foreach (var filter in stage.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer || !filter.sharedMesh) continue;
            var materials = renderer.sharedMaterials;
            for (int sub = 0; sub < filter.sharedMesh.subMeshCount && sub < materials.Length; sub++)
            {
                var material = materials[sub];
                if (!material) throw new Exception("Missing mapped material on " + filter.name);
                if (!grouped.TryGetValue(material, out var list)) grouped[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = stage.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            oldObjects.Add(filter.gameObject);
        }
        int index = 0;
        foreach (var entry in grouped)
        {
            var combined = new Mesh { name = assetPrefix + "-" + index, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.CombineMeshes(entry.Value.ToArray(), true, true, false);
            string path = DirectoryPath + "/" + assetPrefix + "-" + index + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(combined, existing); UnityEngine.Object.DestroyImmediate(combined); combined = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(combined, path);
            var child = new GameObject(entry.Key.name); child.transform.SetParent(stage, false);
            child.AddComponent<MeshFilter>().sharedMesh = combined; child.AddComponent<MeshRenderer>().sharedMaterial = entry.Key;
            index++;
        }
        foreach (var old in oldObjects) UnityEngine.Object.DestroyImmediate(old);
    }
    private static Material Palette(Material source, bool starship)
    {
        if (!source) throw new Exception("Source material missing");
        string name = source.name.Replace('_', ' ');
        string safe = source.name.Replace('/', '-').Replace('\\', '-');
        string path = DirectoryPath + "/" + (starship ? "Starship-source-" : "SLS-source-") + safe + ".mat";
        var output = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!output) { output = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(output, path); }
        if (starship) return ApplyStarshipPbr(output, source);
        output.color = source.HasProperty("_Color") ? source.color : Color.white;
        output.mainTexture = source.mainTexture;
        output.mainTextureScale = source.mainTextureScale;
        output.mainTextureOffset = source.mainTextureOffset;
        if (source.HasProperty("_Metallic")) output.SetFloat("_Metallic", source.GetFloat("_Metallic"));
        if (source.HasProperty("_Glossiness")) output.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
        if (!starship)
        {
            float metallic = name.IndexOf("Engine graphite", StringComparison.OrdinalIgnoreCase) >= 0 ? .55f :
                name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 ? .72f : 0f;
            float roughness = name.IndexOf("Core foam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("SLS Livery", StringComparison.OrdinalIgnoreCase) >= 0 ? .60f :
                name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 ? .38f : .48f;
            output.SetFloat("_Metallic", metallic); output.SetFloat("_Smoothness", 1 - roughness);
            if (name.IndexOf("SLS Livery", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!output.mainTexture)
                    output.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DirectoryPath + "/SLS/SLS-Livery-Review-Atlas.png");
                if (!output.mainTexture) throw new Exception("SLS livery atlas texture missing");
                output.SetFloat("_AlphaClip", 1); output.SetFloat("_Cutoff", .5f);
                output.EnableKeyword("_ALPHATEST_ON");
                output.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                output.SetOverrideTag("RenderType", "TransparentCutout");
            }
        }
        EditorUtility.SetDirty(output);
        return output;
    }
    private static Material ApplyStarshipPbr(Material output, Material source)
    {
        var manifest = LoadStarshipManifest();
        if (manifest.materials == null) throw new Exception("Starship PBR materials missing from manifest");
        MaterialRecord match = null;
        foreach (var record in manifest.materials)
            if (record.name == source.name || record.name.Replace(' ', '_') == source.name.Replace(' ', '_')) { match = record; break; }
        if (match == null) throw new Exception("Starship material missing from manifest: " + source.name);
        var albedo = LoadTexture(match.base_color_texture, false, false);
        var metallic = LoadTexture(match.metallic_smoothness_texture, true, false);
        var normal = LoadTexture(match.normal_texture, true, true);
        // The downloaded artist mesh uses thin and inward-facing surfaces; match Blender two-sided opaque visibility.
        output.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        output.SetFloat("_Surface", 0); output.SetFloat("_AlphaClip", 0); output.SetFloat("_ZWrite", 1);
        output.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        output.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        output.DisableKeyword("_ALPHATEST_ON"); output.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        output.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry; output.SetOverrideTag("RenderType", "Opaque");
        output.SetColor("_BaseColor", match.base_color != null && match.base_color.Length == 4 ? new Color(match.base_color[0], match.base_color[1], match.base_color[2], match.base_color[3]) : Color.white);
        output.SetTexture("_BaseMap", albedo);
        output.SetTexture("_MetallicGlossMap", metallic); output.EnableKeyword("_METALLICSPECGLOSSMAP");
        output.SetTexture("_BumpMap", normal); output.EnableKeyword("_NORMALMAP");
        output.SetFloat("_Metallic", match.metallic); output.SetFloat("_Smoothness", match.smoothness);
        EditorUtility.SetDirty(output); return output;
    }
    private static Texture2D LoadTexture(string relative, bool linear, bool normal)
    {
        if (string.IsNullOrEmpty(relative) || relative.Contains("..") || System.IO.Path.IsPathRooted(relative)) throw new Exception("Invalid Starship texture path");
        string path = DirectoryPath + "/Starship/" + relative.Replace('\\', '/');
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new Exception("Missing Starship texture " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture == linear)
        {
            importer.textureType = type; importer.sRGBTexture = !linear; importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture) throw new Exception("Could not load Starship texture " + path);
        return texture;
    }
    private static Transform Nozzle(Transform parent, string name, float x, float height, float z)
    {
        var result = new GameObject(name).transform; result.SetParent(parent, false);
        result.localPosition = new Vector3(x, height, z); return result;
    }
    private static RocketPlume AddPlume(Transform parent, Shader shader, int stage, RocketPlume.PlumeStyle style, List<Transform> anchors, float radius, float length)
    {
        var plume = parent.gameObject.AddComponent<RocketPlume>(); plume.plumeShader = shader; plume.stageIndex = stage;
        plume.style = style; plume.nozzles = anchors.ToArray(); plume.nozzleRadius = radius; plume.plumeLength = length; return plume;
    }
    [Serializable] private sealed class AnchorManifest { public float height_m; public Anchor[] nozzle_anchors; public MaterialRecord[] materials; public string[] model_files; }
    [Serializable] private sealed class MaterialRecord { public string name, base_color_texture, metallic_smoothness_texture, normal_texture; public float[] base_color; public float metallic, smoothness; }
    [Serializable] private sealed class Anchor { public string name; public string stage; public float[] unity_position_m, unity_exhaust_direction; public float opening_radius_m; }
    private static AnchorManifest LoadStarshipManifest()
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(DirectoryPath + "/Starship/ESP-Starship-import.json");
        if (!text) throw new Exception("Missing Starship nozzle manifest");
        var manifest = JsonUtility.FromJson<AnchorManifest>(text.text);
        if (manifest == null || manifest.nozzle_anchors == null) throw new Exception("Invalid Starship nozzle manifest");
        return manifest;
    }
    private static void StarshipPlumes(Transform booster, Transform upper, Shader shader)
    {
        var manifest = LoadStarshipManifest();
        int lowerCount = 0, topCount = 0;
        foreach (var anchor in manifest.nozzle_anchors)
        {
            if (anchor == null || anchor.unity_position_m == null || anchor.unity_position_m.Length != 3 || anchor.opening_radius_m <= 0)
                throw new Exception("Invalid Starship nozzle anchor");
            bool isLower = anchor.stage == "Booster";
            if (!isLower && anchor.stage != "Ship") throw new Exception("Unknown Starship nozzle stage");
            var point = anchor.unity_position_m;
            var parent = isLower ? booster : upper;
            var node = Nozzle(parent, anchor.name, point[0], point[1], point[2]);
            var direction = anchor.unity_exhaust_direction;
            if (direction == null || direction.Length != 3 ||
                float.IsNaN(direction[0]) || float.IsInfinity(direction[0]) ||
                float.IsNaN(direction[1]) || float.IsInfinity(direction[1]) ||
                float.IsNaN(direction[2]) || float.IsInfinity(direction[2]))
                throw new Exception("Missing or invalid Starship exhaust direction: " + anchor.name);
            var axis = new Vector3(direction[0], direction[1], direction[2]);
            if (Mathf.Abs(axis.magnitude - 1f) > .01f) throw new Exception("Non-unit Starship exhaust direction: " + anchor.name);
            node.localRotation = Quaternion.FromToRotation(Vector3.down, axis.normalized);
            // Separate components retain each nozzle's actual opening radius.
            var plumeObject = new GameObject(anchor.name + " plume"); plumeObject.transform.SetParent(parent, false);
            AddPlume(plumeObject.transform, shader, isLower ? 0 : 1, RocketPlume.PlumeStyle.Methane,
                new List<Transform> { node }, anchor.opening_radius_m, isLower ? 13 : 12);
            if (isLower) lowerCount++; else topCount++;
        }
        if (lowerCount == 0 || topCount == 0) throw new Exception("Missing Starship nozzle anchors " + lowerCount + "/" + topCount);
    }
    private static void SlsPlumes(Transform booster, Transform upper, Transform left, Transform right, Shader shader)
    {
        var core = new List<Transform> {
            Nozzle(booster,"Core nozzle",-2.348f,0,-1.949f), Nozzle(booster,"Core nozzle",2.302f,0,-1.949f),
            Nozzle(booster,"Core nozzle",-2.348f,0,1.938f), Nozzle(booster,"Core nozzle",2.302f,0,1.938f) };
        AddPlume(booster,shader,0,RocketPlume.PlumeStyle.Hydrolox,core,1.25f,15);
        var leftPlume = AddPlume(left,shader,0,RocketPlume.PlumeStyle.Solid,new List<Transform> { Nozzle(left,"Left SRB nozzle",-6.995f,.0178f,.1754f) },1.45f,18);
        var rightPlume = AddPlume(right,shader,0,RocketPlume.PlumeStyle.Solid,new List<Transform> { Nozzle(right,"Right SRB nozzle",6.995f,.0178f,.1754f) },1.45f,18);
        leftPlume.solidBooster = true; rightPlume.solidBooster = true;
        // Source lacks an upper nozzle; this is a provisional cosmetic anchor.
        AddPlume(upper,shader,1,RocketPlume.PlumeStyle.Hydrolox,new List<Transform> { Nozzle(upper,"Upper provisional nozzle",0,64.996f,0) },.7f,10);
    }
}
