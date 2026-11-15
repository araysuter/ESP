using UnityEngine;

namespace ESP.Presentation
{
    // Visual only: authoritative atmosphere and velocity are supplied by the caller.
    public sealed class ReentryPlasma : MonoBehaviour
    {
        Material shellMaterial, wakeMaterial;
        Transform wake;
        float intensity;

        public static ReentryPlasma Create(Transform rocketRoot, Vector3 localCenter, float radius, float length, Shader shader)
        {
            if (!shader || !rocketRoot || radius <= 0 || length <= 0) return null;
            var root = new GameObject("Reentry plasma");
            root.transform.SetParent(rocketRoot, false);
            var effect = root.AddComponent<ReentryPlasma>();
            effect.shellMaterial = new Material(shader);
            effect.wakeMaterial = new Material(shader);
            effect.wakeMaterial.SetFloat("_Wake", 1);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Windward shock layer";
            shell.transform.SetParent(root.transform, false);
            shell.transform.localPosition = localCenter;
            shell.transform.localScale = new Vector3(radius * 2.25f, length * 1.04f, radius * 2.25f);
            Destroy(shell.GetComponent<Collider>());
            shell.GetComponent<MeshRenderer>().sharedMaterial = effect.shellMaterial;
            var tail = new GameObject("Plasma wake");
            tail.transform.SetParent(root.transform, false);
            tail.transform.localPosition = localCenter;
            effect.wake = tail.transform;
            var filter = tail.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildWake(radius, length);
            tail.AddComponent<MeshRenderer>().sharedMaterial = effect.wakeMaterial;
            return effect;
        }

        // velocityLocal points in the direction of travel in rocketRoot coordinates.
        // Fade in/out smoothly; speeds and densities are SI. This is artistic, not a heat model.
        public void SetConditions(float airspeedMps, float densityKgM3, Vector3 velocityLocal, bool active)
        {
            float speed = Mathf.Max(0, airspeedMps), density = Mathf.Max(0, densityKgM3);
            float target = active ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1200, 5000, speed)) *
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.00001f, .003f, density)) : 0;
            intensity = Mathf.MoveTowards(intensity, target, Time.deltaTime * .6f);
            Vector3 forward = velocityLocal.sqrMagnitude > .01f ? velocityLocal.normalized : Vector3.up;
            Vector3 worldFlow = transform.parent.TransformDirection(forward);
            shellMaterial.SetVector("_Flow", new Vector4(worldFlow.x, worldFlow.y, worldFlow.z, 0));
            shellMaterial.SetFloat("_Intensity", intensity);
            wakeMaterial.SetFloat("_Intensity", intensity);
            wake.localRotation = Quaternion.FromToRotation(Vector3.up, -forward);
        }

        static Mesh BuildWake(float radius, float length)
        {
            const int sides = 32, rings = 12;
            var vertices = new Vector3[(sides + 1) * (rings + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * rings * 6];
            for (int y = 0; y <= rings; y++) for (int x = 0; x <= sides; x++)
            {
                float t = (float)y / rings, a = x * Mathf.PI * 2 / sides;
                float r = radius * (1.1f + t * 1.4f);
                int i = y * (sides + 1) + x;
                vertices[i] = new Vector3(Mathf.Cos(a) * r, t * length * 2, Mathf.Sin(a) * r);
                normals[i] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                uv[i] = new Vector2((float)x / sides, t);
                if (x == sides || y == rings) continue;
                int q = (y * sides + x) * 6, j = i + sides + 1;
                triangles[q] = i; triangles[q+1] = j; triangles[q+2] = i+1;
                triangles[q+3] = i+1; triangles[q+4] = j; triangles[q+5] = j+1;
            }
            var mesh = new Mesh { name = "Reentry wake" };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
        void OnDestroy()
        {
            if (wake) Destroy(wake.GetComponent<MeshFilter>().sharedMesh);
            if (shellMaterial) Destroy(shellMaterial);
            if (wakeMaterial) Destroy(wakeMaterial);
        }
    }
}
