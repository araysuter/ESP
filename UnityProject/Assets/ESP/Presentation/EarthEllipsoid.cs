using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>
    /// Display-only WGS84 ellipsoid. Its scale is for a map view and is not
    /// a physics scale. Unity axes map ECEF X, Z, Y to Unity X, Y, Z; this
    /// permutation reverses handedness. Seam and pole vertices are duplicated
    /// for equirectangular UVs; pole triangles have one degenerate edge.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class EarthEllipsoid : MonoBehaviour
    {
        [SerializeField] private double metersPerUnit = 1000000.0;

        private void Awake()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null)
                filter.sharedMesh = BuildMesh(metersPerUnit);
        }

        /// <summary>Builds a display mesh, with 512 longitude and 256 latitude segments.</summary>
        public static Mesh BuildMesh(double metersPerUnit = 1000000.0)
        {
            if (double.IsNaN(metersPerUnit) || double.IsInfinity(metersPerUnit) || metersPerUnit <= 0)
                throw new ArgumentOutOfRangeException(nameof(metersPerUnit));
            const int longitudeSegments = 512;
            const int latitudeSegments = 256;
            const double a = 6378137.0;
            const double b = 6356752.314245;
            double eccentricitySquared = 1.0 - b * b / (a * a);
            int columns = longitudeSegments + 1;
            var vertices = new Vector3[columns * (latitudeSegments + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (int latIndex = 0; latIndex <= latitudeSegments; latIndex++)
            {
                double latitude = -Math.PI / 2 + Math.PI * latIndex / latitudeSegments;
                double sinLat = Math.Sin(latitude);
                double cosLat = Math.Cos(latitude);
                double n = a / Math.Sqrt(1 - eccentricitySquared * sinLat * sinLat);
                for (int lonIndex = 0; lonIndex <= longitudeSegments; lonIndex++)
                {
                    double longitude = -Math.PI + 2 * Math.PI * lonIndex / longitudeSegments;
                    double cosLon = Math.Cos(longitude);
                    double sinLon = Math.Sin(longitude);
                    int index = latIndex * columns + lonIndex;
                    vertices[index] = new Vector3((float)(n * cosLat * cosLon / metersPerUnit),
                        (float)(n * (1 - eccentricitySquared) * sinLat / metersPerUnit),
                        (float)(n * cosLat * sinLon / metersPerUnit));
                    normals[index] = new Vector3((float)(cosLat * cosLon), (float)sinLat, (float)(cosLat * sinLon));
                    uv[index] = new Vector2((float)lonIndex / longitudeSegments, (float)latIndex / latitudeSegments);
                }
            }
            var triangles = new int[longitudeSegments * latitudeSegments * 6];
            int cursor = 0;
            for (int latIndex = 0; latIndex < latitudeSegments; latIndex++)
            for (int lonIndex = 0; lonIndex < longitudeSegments; lonIndex++)
            {
                int lower = latIndex * columns + lonIndex;
                int upper = lower + columns;
                // In the swapped Unity axes, latitude x longitude points outward.
                triangles[cursor++] = lower;
                triangles[cursor++] = upper;
                triangles[cursor++] = lower + 1;
                triangles[cursor++] = lower + 1;
                triangles[cursor++] = upper;
                triangles[cursor++] = upper + 1;
            }
            var mesh = new Mesh { name = "WGS84 Display Ellipsoid", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
