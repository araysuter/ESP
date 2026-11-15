using UnityEngine;

namespace ESP.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class StarFieldSetup : MonoBehaviour
    {
        public Shader shader;
        public Texture2D sky;
        private Material material;
        private void Start()
        {
            if (!shader) return;
            material = new Material(shader);
            StarField.Create(GetComponent<Camera>(), material, 31, sky);
        }
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
