using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>
    /// Visual single-scattering approximation with eight view samples and six
    /// sunlight samples per illuminated point, clipped to a WGS84 ellipsoid.
    /// Not flight weather or calibrated radiometry. One full-screen draw, no LUTs.
    /// </summary>
    public sealed class ContinuousAtmosphere : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private bool reportedView;

        public static ContinuousAtmosphere Create(Camera camera, Shader shader, int layer)
        {
            var root = new GameObject("Continuous atmospheric scattering approximation");
            root.layer = layer;
            root.transform.SetParent(camera.transform, false);
            // Keep the draw in front of the camera even though its vertex shader
            // emits clip positions. This also avoids camera-plane renderer sorting.
            root.transform.localPosition = Vector3.forward * .01f;
            var effect = root.AddComponent<ContinuousAtmosphere>();
            effect.mesh = new Mesh { name = "Atmosphere screen quad" };
            effect.mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0),
                new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            effect.mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            // Shader positions the quad directly in clip space; keep CPU culling
            // bounds around the camera and large enough to intersect every view.
            effect.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            root.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            effect.material = new Material(shader) { name = "Continuous Earth air (visual approximation)" };
            renderer.sharedMaterial = effect.material;
            return effect;
        }

        public void UpdateView(Camera camera, Transform planet, Vector3 towardSun)
        {
            Quaternion inverse = Quaternion.Inverse(planet.rotation);
            Vector3 origin = inverse * (camera.transform.position - planet.position);
            // The global display mesh and camera use floats. At pad altitude their
            // subtraction can place a ray origin just under the analytic ellipsoid.
            // Clamp only this visual integration origin, never flight/ground state.
            Vector3 normalized = new Vector3(origin.x / 6.378137f, origin.y / 6.3567523f, origin.z / 6.378137f);
            float radius = normalized.magnitude;
            float displayAltitude = (radius - 1) * 6371000f;
            if (radius > 0 && radius < 1.0000003f) origin *= 1.0000003f / radius;
            material.SetVector("_CameraPlanet", origin);
            material.SetVector("_ForwardPlanet", inverse * camera.transform.forward);
            material.SetVector("_RightPlanet", inverse * camera.transform.right);
            material.SetVector("_UpPlanet", inverse * camera.transform.up);
            material.SetVector("_SunPlanet", inverse * towardSun.normalized);
            material.SetVector("_ViewParams", new Vector4(Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad), camera.aspect, 0, 0));
            if (!reportedView)
            {
                reportedView = true;
                Debug.Log("ESP_ATMOSPHERE_VIEW shader=" + material.shader.name + " supported=" + material.shader.isSupported +
                    " origin=" + origin.ToString("F7") + " altitude=" + displayAltitude.ToString("F1") +
                    " sun=" + (inverse * towardSun.normalized).ToString("F4") + " queue=" + material.renderQueue);
            }
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
