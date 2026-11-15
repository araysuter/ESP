using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>Original annulus; illustrative 27-degree tilt, not a measured Saturn pole.</summary>
    public sealed class SaturnRingVisual : MonoBehaviour
    {
        internal const int Segments = 128;
        internal const float InnerRadius = .6f, OuterRadius = 1.15f;
        private Mesh mesh;
        private Material material;

        public static void Attach(Transform planet)
        {
            var texture = Resources.Load<Texture2D>("SolarSystem/SaturnRings");
            var shader = Resources.Load<Shader>("SolarSystem/SaturnRingShader");
            if (texture == null || shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Saturn rings unavailable; retaining planet sphere.");
                return;
            }
            var rings = new GameObject("Saturn rings (illustrative pole tilt)");
            rings.layer = planet.gameObject.layer;
            rings.transform.SetParent(planet, false);
            rings.transform.localRotation = Quaternion.Euler(0, 0, 27);
            var owner = rings.AddComponent<SaturnRingVisual>();
            owner.mesh = BuildMesh();
            owner.material = new Material(shader) { name = "Saturn radial rings (runtime)", mainTexture = texture };
            rings.AddComponent<MeshFilter>().sharedMesh = owner.mesh;
            var renderer = rings.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = owner.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Parent Unity sphere has radius .5. Ring radii are 1.2 and 2.3 times
        // that radius, so child geometry inherits either true or compressed scale.
        internal static Mesh BuildMesh()
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * 2 * Mathf.PI / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[i * 2] = direction * InnerRadius;
                vertices[i * 2 + 1] = direction * OuterRadius;
                uv[i * 2] = new Vector2(0, .5f);
                uv[i * 2 + 1] = new Vector2(1, .5f);
                if (i == Segments) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Original Saturn annulus", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
