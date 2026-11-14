using System.Collections.Generic;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Owns only runtime-created resources, never the caller's ground material.</summary>
    public sealed class LaunchEnvironmentResources : MonoBehaviour
    {
        public readonly List<Material> Materials = new List<Material>();
        public Mesh GroundMesh;

        private void OnDestroy()
        {
            if (GroundMesh != null) Destroy(GroundMesh);
            foreach (var material in Materials)
                if (material != null) Destroy(material);
        }
    }
}
