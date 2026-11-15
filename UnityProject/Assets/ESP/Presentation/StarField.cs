using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>
    /// Decorative, deterministic stars. Directions are fictional, not a star catalog.
    /// The shell follows camera position but not orientation, so it has no parallax.
    /// The supplied material must use ESP/Star Field and be serialized into the scene.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class StarField : MonoBehaviour
    {
        private const int StarCount = 2400;
        private Camera targetCamera;
        private Mesh mesh;

        /// <summary>Create a separate field for each camera, on a layer visible to that camera.</summary>
        public static StarField Create(Camera camera, Material material, int layer)
        {
            if (!camera || !material) return null;
            var existing = camera.GetComponentInChildren<StarField>(true);
            if (existing) return existing;
            var root = new GameObject("Procedural star field");
            root.transform.SetParent(camera.transform, false);
            root.layer = layer;
            var field = root.AddComponent<StarField>();
            field.targetCamera = camera;
            field.mesh = MakeMesh(Mathf.Max(camera.nearClipPlane * 2f, camera.farClipPlane * .8f));
            root.AddComponent<MeshFilter>().sharedMesh = field.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            root.transform.SetPositionAndRotation(camera.transform.position, Quaternion.identity);
            Debug.Log("ESP star field: " + StarCount + " stars, " + (StarCount * 2) + " triangles (one mesh per camera)");
            return field;
        }

        private void LateUpdate()
        {
            if (targetCamera) transform.SetPositionAndRotation(targetCamera.transform.position, Quaternion.identity);
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        private static Mesh MakeMesh(float radius)
        {
            var vertices = new Vector3[StarCount * 4];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[StarCount * 6];
            var random = new System.Random(836471);
            for (int i = 0; i < StarCount; i++)
            {
                // Uniform distribution on the unit sphere, independent of Unity's global random state.
                double y = 2.0 * random.NextDouble() - 1.0;
                double azimuth = 2.0 * Math.PI * random.NextDouble();
                double horizontal = Math.Sqrt(1.0 - y * y);
                var direction = new Vector3((float)(horizontal * Math.Cos(azimuth)), (float)y,
                    (float)(horizontal * Math.Sin(azimuth)));
                var right = Vector3.Cross(direction, Mathf.Abs(direction.y) > .95f ? Vector3.right : Vector3.up).normalized;
                var up = Vector3.Cross(right, direction).normalized;
                float brightnessRoll = (float)random.NextDouble();
                float brightness = .18f + .58f * brightnessRoll * brightnessRoll * brightnessRoll;
                float angularSize = Mathf.Lerp(.00075f, .0023f, brightnessRoll * brightnessRoll);
                float size = radius * angularSize;
                float tint = (float)random.NextDouble();
                Color color = Color.Lerp(new Color(.69f, .79f, 1f), new Color(1f, .87f, .71f), tint) * brightness;
                color.a = 1f;
                Vector3 center = direction * radius;
                int v = i * 4;
                vertices[v] = center - right * size - up * size;
                vertices[v + 1] = center + right * size - up * size;
                vertices[v + 2] = center + right * size + up * size;
                vertices[v + 3] = center - right * size + up * size;
                uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
                uv[v + 2] = new Vector2(1, 1); uv[v + 3] = new Vector2(0, 1);
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
                int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var result = new Mesh { name = "Procedural star directions" };
            result.vertices = vertices;
            result.uv = uv;
            result.colors = colors;
            result.triangles = triangles;
            result.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.1f);
            return result;
        }
    }
}
