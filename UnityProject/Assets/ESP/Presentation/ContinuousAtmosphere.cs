using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>
    /// Original visual atmosphere approximation: fixed eight-sample exponential
    /// density integral along an ellipsoid-clipped ray. Not flight weather or a
    /// radiometrically calibrated scattering model. One full-screen draw, no LUTs.
    /// </summary>
    public sealed class ContinuousAtmosphere : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;

        public static ContinuousAtmosphere Create(Camera camera, Shader shader, int layer)
        {
            var root = new GameObject("Continuous atmospheric scattering approximation");
            root.layer = layer;
            root.transform.SetParent(camera.transform, false);
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
            effect.material = new Material(shader) { name = "Continuous Earth air (visual approximation)" };
            renderer.sharedMaterial = effect.material;
            return effect;
        }

        public void UpdateView(Camera camera, Transform planet, Vector3 towardSun)
        {
            Quaternion inverse = Quaternion.Inverse(planet.rotation);
            material.SetVector("_CameraPlanet", inverse * (camera.transform.position - planet.position));
            material.SetVector("_ForwardPlanet", inverse * camera.transform.forward);
            material.SetVector("_RightPlanet", inverse * camera.transform.right);
            material.SetVector("_UpPlanet", inverse * camera.transform.up);
            material.SetVector("_SunPlanet", inverse * towardSun.normalized);
            material.SetVector("_ViewParams", new Vector4(Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad), camera.aspect, 0, 0));
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
