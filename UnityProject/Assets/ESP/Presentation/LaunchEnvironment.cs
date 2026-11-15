using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Procedural visual pad in local meters. This is not terrain or flight physics.</summary>
    public static class LaunchEnvironment
    {
        /// <summary>Rocket base-origin height placing the placeholder engine bottoms on the mount.</summary>
        public const float RocketBaseHeightMeters = 2.4f;

        public static GameObject Build(LaunchSite site, Material groundMaterial)
        {
            var root = new GameObject("Launch environment — " + site.Name);
            var owned = root.AddComponent<LaunchEnvironmentResources>();
            Material ground = groundMaterial != null ? groundMaterial : MakeMaterial("Ground", new Color(0.25f, 0.32f, 0.29f));
            Material concrete = MakeMaterial("Concrete", new Color(0.42f, 0.44f, 0.43f));
            Material dark = MakeMaterial("Road and structure", new Color(0.13f, 0.17f, 0.19f));
            Material white = MakeMaterial("Warm white", new Color(0.83f, 0.81f, 0.74f));
            Material cyan = MakeMaterial("Cyan accent", new Color(0.15f, 0.62f, 0.68f));
            Material orange = MakeMaterial("Orange accent", new Color(0.92f, 0.38f, 0.12f));

            if (groundMaterial == null) owned.Materials.Add(ground);
            owned.Materials.AddRange(new[] { concrete, dark, white, cyan, orange });
            // The regional request covers +/-0.1 degrees. Crop its center to a
            // 2 km square, accounting for longitude scale at the selected latitude.
            float vHalf = (float)(1000.0 / 111320.0 / 0.2);
            float cosLatitude = Mathf.Max(0.01f, Mathf.Abs(Mathf.Cos((float)site.Latitude * Mathf.Deg2Rad)));
            float uHalf = Mathf.Min(0.5f, vHalf / cosLatitude);
            var mesh = new Mesh { name = "Visual ground mesh" };
            mesh.vertices = new[] { new Vector3(-1000, 0, -1000), new Vector3(-1000, 0, 1000),
                new Vector3(1000, 0, 1000), new Vector3(1000, 0, -1000) };
            mesh.uv = new[] { new Vector2(0.5f-uHalf, 0.5f-vHalf), new Vector2(0.5f-uHalf, 0.5f+vHalf),
                new Vector2(0.5f+uHalf, 0.5f+vHalf), new Vector2(0.5f+uHalf, 0.5f-vHalf) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            // URP Lit needs a tangent basis; missing tangents rendered the sunlit imagery black.
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            owned.GroundMesh = mesh;
            var plane = new GameObject("Visual ground (2 km)");
            plane.transform.SetParent(root.transform, false);
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            plane.AddComponent<MeshRenderer>().sharedMaterial = ground;
            Box(root, "Approach road", new Vector3(0, 0.025f, -300), new Vector3(14, 0.05f, 600), dark);
            Box(root, "Pad deck", new Vector3(0, 0.5f, 0), new Vector3(35, 1, 35), concrete);
            Box(root, "Launch mount", new Vector3(0, 1.1f, 0), new Vector3(8, 0.2f, 8), dark);
            // Open tower skeleton, deliberately offset from the rocket's 4 m body.
            foreach (float x in new[] { 12f, 18f })
                foreach (float z in new[] { -3f, 3f })
                    Box(root, "Tower column", new Vector3(x, 24, z), new Vector3(0.8f, 48, 0.8f), dark);
            for (int y = 8; y <= 48; y += 8)
            {
                Box(root, "Tower level", new Vector3(15, y, 0), new Vector3(7, 0.5f, 7), white);
                Box(root, "Tower stripe", new Vector3(15, y + 0.35f, -3.4f), new Vector3(7, 0.2f, 0.2f), cyan);
            }
            Box(root, "Access arm", new Vector3(9, 31, 0), new Vector3(7, 0.6f, 1.4f), orange);
            Box(root, "Service building", new Vector3(-65, 4, -42), new Vector3(24, 8, 16), white);
            Box(root, "Service building roof", new Vector3(-65, 8.2f, -42), new Vector3(25, 0.4f, 17), dark);
            for (int i = 0; i < 3; i++)
            {
                var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tank.name = "Storage tank"; tank.transform.SetParent(root.transform, false);
                tank.transform.localPosition = new Vector3(-55 + i * 15, 4, 48);
                tank.transform.localScale = new Vector3(10, 4, 10);
                tank.GetComponent<Renderer>().sharedMaterial = white;
                Object.Destroy(tank.GetComponent<Collider>());
            }
            return root;
        }

        private static void Box(GameObject root, string name, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(root.transform, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(obj.GetComponent<Collider>());
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }
    }
}
