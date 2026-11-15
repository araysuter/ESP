using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Imports the normalized NASA gantry export as a static, collider-free visual prefab.</summary>
public static class ImportLaunchStructures
{
    private const string SourceDirectory = "Assets/ESP/Art/Sites/NasaGantry";
    private const string GeneratedDirectory = SourceDirectory + "/Generated";
    private const string ObjPath = GeneratedDirectory + "/NasaGantry.obj";
    private const string MeshPath = GeneratedDirectory + "/NasaGantry-Mesh.asset";
    private const string PrefabPath = GeneratedDirectory + "/NasaGantry.prefab";
    private const long MaximumObjBytes = 32L * 1024 * 1024;
    private const float ExpectedHeightMeters = 105.7f;
    private const float HeightToleranceMeters = .2f;
    private const float BaseToleranceMeters = .02f;

    [Serializable] private sealed class Manifest
    {
        public string compressedSha256, objSha256, sourceSha256, sourceUrl, attribution, upAxis;
        public long objBytes;
        public float heightMeters, baseY;
        public string[] materialNames;
        public MaterialRecord[] materials;
        public BoundsRecord bounds;
    }
    [Serializable] private sealed class MaterialRecord
    {
        public string name, sourceName;
        public float[] baseColor;
        public float metallic, roughness;
    }
    [Serializable] private sealed class BoundsRecord { public float[] minimum, maximum; }

    [MenuItem("ESP/Assets/Rebuild NASA launch gantry")]
    public static void RebuildFromMenu() { Build(); }

    /// <summary>Returns the saved prefab asset. Does not attach it to a scene or add physics components.</summary>
    public static GameObject Build()
    {
        var manifest = ReadManifest();
        string compressedPath = SourceDirectory + "/NasaGantry.obj.gz";
        if (!File.Exists(compressedPath)) throw new FileNotFoundException("NASA gantry compressed OBJ is missing", compressedPath);
        long compressedBytes = new FileInfo(compressedPath).Length;
        if (compressedBytes <= 0 || compressedBytes > MaximumObjBytes)
            throw new InvalidDataException("NASA gantry gzip must be nonempty and no larger than 32 MiB");
        VerifyHash(compressedPath, manifest.compressedSha256, "compressed OBJ");
        EnsureGeneratedDirectory();
        ExtractAndValidate(compressedPath, manifest);

        AssetDatabase.ImportAsset(ObjPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(ObjPath) as ModelImporter;
        if (importer == null) throw new InvalidDataException("Unity did not recognize the generated NASA gantry OBJ");
        importer.globalScale = 1;
        importer.useFileScale = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.preserveHierarchy = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = true;
        importer.indexFormat = ModelImporterIndexFormat.UInt32;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ObjPath);
        if (!source) throw new InvalidDataException("Unity could not load the generated NASA gantry OBJ");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP Lit shader is required for the NASA gantry");

        GameObject instance = null, output = null;
        var temporaryMeshes = new List<Mesh>();
        try
        {
            instance = UnityEngine.Object.Instantiate(source);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var groups = CollectGroups(instance, manifest);
            Bounds importedBounds = RendererBounds(instance);
            ValidateBounds(importedBounds, "Unity imported model");

            var materials = new List<Material>();
            var combine = new List<CombineInstance>();
            for (int i = 0; i < manifest.materialNames.Length; i++)
            {
                if (groups[i].Count == 0) throw new InvalidDataException("NASA gantry material group is missing: " + manifest.materialNames[i]);
                var group = new Mesh { name = manifest.materialNames[i], indexFormat = IndexFormat.UInt32 };
                temporaryMeshes.Add(group);
                group.CombineMeshes(groups[i].ToArray(), true, true, false);
                combine.Add(new CombineInstance { mesh = group, subMeshIndex = 0, transform = Matrix4x4.identity });
                materials.Add(CreateMaterial(manifest.materials[i], shader));
            }
            var combined = new Mesh { name = "NASA Gantry — source geometry", indexFormat = IndexFormat.UInt32 };
            temporaryMeshes.Add(combined);
            combined.CombineMeshes(combine.ToArray(), false, false, false);
            combined.RecalculateBounds();
            if (!combined.HasVertexAttribute(VertexAttribute.Normal)) combined.RecalculateNormals();
            ValidateBounds(combined.bounds, "combined prefab mesh");
            if (combined.vertexCount == 0 || combined.subMeshCount != materials.Count)
                throw new InvalidDataException("NASA gantry combined mesh is empty or lost a source material group");
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (savedMesh)
            {
                EditorUtility.CopySerialized(combined, savedMesh);
                EditorUtility.SetDirty(savedMesh);
            }
            else
            {
                AssetDatabase.CreateAsset(combined, MeshPath);
                savedMesh = combined;
                temporaryMeshes.Remove(combined);
            }

            string credit = manifest.attribution.Replace('\n', ' ').Replace('\r', ' ');
            output = new GameObject("NASA Gantry — " + credit);
            output.AddComponent<MeshFilter>().sharedMesh = savedMesh;
            var renderer = output.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials.ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (output.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("NASA gantry visual must not contain runtime colliders");
            var prefab = PrefabUtility.SaveAsPrefabAsset(output, PrefabPath);
            if (!prefab) throw new IOException("Could not save NASA gantry prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("ESP_NASA_GANTRY_IMPORTED " + PrefabPath + " height=" + combined.bounds.size.y.ToString("F3", CultureInfo.InvariantCulture) +
                "m vertices=" + savedMesh.vertexCount + " submeshes=" + savedMesh.subMeshCount + " source=" + manifest.sourceUrl);
            return prefab;
        }
        finally
        {
            if (instance) UnityEngine.Object.DestroyImmediate(instance);
            if (output) UnityEngine.Object.DestroyImmediate(output);
            foreach (var mesh in temporaryMeshes) if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static Manifest ReadManifest()
    {
        string path = SourceDirectory + "/NasaGantry.json";
        if (!File.Exists(path)) throw new FileNotFoundException("NASA gantry manifest is missing", path);
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("NASA gantry manifest exceeds 1 MiB");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
        if (manifest == null || manifest.upAxis != "Y" || !Finite(manifest.heightMeters) ||
            Mathf.Abs(manifest.heightMeters - ExpectedHeightMeters) > HeightToleranceMeters ||
            !Finite(manifest.baseY) || Mathf.Abs(manifest.baseY) > BaseToleranceMeters)
            throw new InvalidDataException("NASA gantry manifest must declare Y-up, base zero and height 105.7 m");
        if (manifest.objBytes <= 0 || manifest.objBytes > MaximumObjBytes)
            throw new InvalidDataException("NASA gantry manifest OBJ size must be between 1 byte and 32 MiB");
        if (string.IsNullOrWhiteSpace(manifest.attribution) || string.IsNullOrWhiteSpace(manifest.sourceUrl))
            throw new InvalidDataException("NASA gantry source attribution is missing");
        if (manifest.materialNames == null || manifest.materials == null || manifest.materialNames.Length != 3 || manifest.materials.Length != 3)
            throw new InvalidDataException("NASA gantry manifest must contain three source material groups");
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < manifest.materialNames.Length; i++)
        {
            string name = manifest.materialNames[i];
            if (string.IsNullOrEmpty(name) || !names.Add(name)) throw new InvalidDataException("NASA gantry material names must be unique");
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') throw new InvalidDataException("Unsafe NASA gantry material name");
            var material = manifest.materials[i];
            if (material == null || material.name != name || material.baseColor == null || material.baseColor.Length != 4 ||
                !UnitInterval(material.metallic) || !UnitInterval(material.roughness))
                throw new InvalidDataException("Invalid NASA gantry material record: " + name);
            foreach (float value in material.baseColor)
                if (!UnitInterval(value)) throw new InvalidDataException("Invalid NASA gantry material color: " + name);
        }
        if (manifest.bounds != null)
        {
            if (manifest.bounds.minimum == null || manifest.bounds.maximum == null || manifest.bounds.minimum.Length != 3 || manifest.bounds.maximum.Length != 3)
                throw new InvalidDataException("NASA gantry manifest bounds must contain three coordinates");
            for (int i = 0; i < 3; i++)
                if (!Finite(manifest.bounds.minimum[i]) || !Finite(manifest.bounds.maximum[i]) || manifest.bounds.minimum[i] > manifest.bounds.maximum[i])
                    throw new InvalidDataException("NASA gantry manifest bounds are invalid");
        }
        return manifest;
    }

    private static void EnsureGeneratedDirectory()
    {
        if (!AssetDatabase.IsValidFolder(SourceDirectory)) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!AssetDatabase.IsValidFolder(SourceDirectory)) throw new DirectoryNotFoundException(SourceDirectory);
        if (!AssetDatabase.IsValidFolder(GeneratedDirectory)) AssetDatabase.CreateFolder(SourceDirectory, "Generated");
        if (!Directory.Exists(GeneratedDirectory)) throw new IOException("Could not create NASA gantry generated directory");
    }

    private static void ExtractAndValidate(string compressedPath, Manifest manifest)
    {
        // A private temporary name prevents a failed decompression/hash/bounds check replacing a good import.
        string temporary = GeneratedDirectory + "/.NasaGantry-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            long total = 0;
            using (var source = File.OpenRead(compressedPath))
            using (var gzip = new GZipStream(source, CompressionMode.Decompress))
            using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[65536];
                int count;
                while ((count = gzip.Read(buffer, 0, buffer.Length)) != 0)
                {
                    total += count;
                    if (total > MaximumObjBytes || total > manifest.objBytes)
                        throw new InvalidDataException("NASA gantry decompression exceeded its declared size or the 32 MiB limit");
                    destination.Write(buffer, 0, count);
                }
            }
            if (total != manifest.objBytes) throw new InvalidDataException("NASA gantry decompressed size differs from manifest");
            VerifyHash(temporary, manifest.objSha256, "uncompressed OBJ");
            ValidateObjBounds(temporary, manifest);
            if (File.Exists(ObjPath) && string.Equals(Sha256(ObjPath), Sha256(temporary), StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(ObjPath)) File.Replace(temporary, ObjPath, null);
            else File.Move(temporary, ObjPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateObjBounds(string path, Manifest manifest)
    {
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        int vertices = 0;
        using (var reader = new StreamReader(path))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.TrimStart();
                if (!trimmed.StartsWith("v ", StringComparison.Ordinal) && !trimmed.StartsWith("v\t", StringComparison.Ordinal)) continue;
                string[] values = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 4) throw new InvalidDataException("NASA gantry OBJ has an incomplete vertex");
                var point = new Vector3(ParseCoordinate(values[1]), ParseCoordinate(values[2]), ParseCoordinate(values[3]));
                minimum = Vector3.Min(minimum, point); maximum = Vector3.Max(maximum, point);
                vertices++;
            }
        }
        if (vertices == 0) throw new InvalidDataException("NASA gantry OBJ contains no vertices");
        var bounds = new Bounds((minimum + maximum) * .5f, maximum - minimum);
        ValidateBounds(bounds, "source OBJ");
        if (manifest.bounds != null)
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(minimum[i] - manifest.bounds.minimum[i]) > BaseToleranceMeters ||
                    Mathf.Abs(maximum[i] - manifest.bounds.maximum[i]) > BaseToleranceMeters)
                    throw new InvalidDataException("NASA gantry OBJ bounds disagree with the manifest");
    }

    private static List<CombineInstance>[] CollectGroups(GameObject instance, Manifest manifest)
    {
        var groups = new List<CombineInstance>[manifest.materialNames.Length];
        for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (!mesh || mesh.vertexCount == 0) continue;
            int index = -1;
            for (Transform node = filter.transform; node != null && index < 0; node = node.parent)
                index = Array.IndexOf(manifest.materialNames, node.name);
            if (index < 0) index = Array.IndexOf(manifest.materialNames, mesh.name);
            if (index < 0) throw new InvalidDataException("Unrecognized NASA gantry OBJ group: " + filter.name + "; expected the manifest material names");
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidDataException("NASA gantry must contain triangulated surfaces");
                groups[index].Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
        }
        return groups;
    }

    private static Bounds RendererBounds(GameObject instance)
    {
        Bounds result = default; bool found = false;
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!found) { result = renderer.bounds; found = true; }
            else result.Encapsulate(renderer.bounds);
        }
        if (!found) throw new InvalidDataException("NASA gantry has no renderable geometry");
        return result;
    }

    private static Material CreateMaterial(MaterialRecord record, Shader shader)
    {
        string path = GeneratedDirectory + "/" + record.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.name = record.name; material.shader = shader;
        material.SetColor("_BaseColor", new Color(record.baseColor[0], record.baseColor[1], record.baseColor[2], record.baseColor[3]));
        material.SetTexture("_BaseMap", null);
        material.SetFloat("_Metallic", record.metallic);
        material.SetFloat("_Smoothness", 1 - record.roughness);
        material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0); material.SetFloat("_ZWrite", 1);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ValidateBounds(Bounds bounds, string phase)
    {
        if (!Finite(bounds.min.x) || !Finite(bounds.min.y) || !Finite(bounds.min.z) ||
            !Finite(bounds.max.x) || !Finite(bounds.max.y) || !Finite(bounds.max.z) ||
            Mathf.Abs(bounds.size.y - ExpectedHeightMeters) > HeightToleranceMeters ||
            Mathf.Abs(bounds.min.y) > BaseToleranceMeters)
            throw new InvalidDataException("NASA gantry " + phase + " must be Y-up, base zero and 105.7 m high; got " + bounds);
    }
    private static float ParseCoordinate(string value)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) || !Finite(result) || Mathf.Abs(result) > 10000)
            throw new InvalidDataException("NASA gantry OBJ contains an invalid coordinate");
        return result;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool UnitInterval(float value) => Finite(value) && value >= 0 && value <= 1;
    private static void VerifyHash(string path, string expected, string label)
    {
        if (string.IsNullOrEmpty(expected)) { Debug.LogWarning("NASA gantry manifest has no SHA-256 for " + label); return; }
        if (expected.Length != 64) throw new InvalidDataException("Malformed NASA gantry SHA-256 for " + label);
        foreach (char c in expected)
            if (!Uri.IsHexDigit(c)) throw new InvalidDataException("Malformed NASA gantry SHA-256 for " + label);
        if (!string.Equals(Sha256(path), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("NASA gantry SHA-256 mismatch for " + label);
    }
    private static string Sha256(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
