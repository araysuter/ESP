using UnityEngine;

namespace ESP.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class StarFieldSetup : MonoBehaviour
    {
        public Shader shader;
        private Material material;
        private void Start()
        {
            if (!shader) return;
            material = new Material(shader);
            StarField.Create(GetComponent<Camera>(), material, 31);
        }
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
