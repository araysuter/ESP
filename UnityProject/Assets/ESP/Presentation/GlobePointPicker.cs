using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>Right-click selection on the origin-centered WGS84 display ellipsoid.</summary>
    public sealed class GlobePointPicker : MonoBehaviour
    {
        public bool useMissionUI;
        public event Action<double, double> PointSelected;
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public bool HasSelection { get; private set; }

        public void Select(double latitude, double longitude)
        {
            if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude < -90 || latitude > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude));
            if (double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude < -180 || longitude > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude));
            Latitude = latitude;
            Longitude = longitude;
            HasSelection = true;
            PointSelected?.Invoke(latitude, longitude);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (useMissionUI ? MissionUI.IsPointerOverUI(pointer) : (pointer.y < 180 || (pointer.x < 365 && pointer.y > Screen.height - 535) || (pointer.x>Screen.width-355 && pointer.y>Screen.height-350))) return;
            Camera camera = GetComponent<Camera>();
            if (camera == null) camera = Camera.main;
            if (camera != null && TryIntersect(camera.ScreenPointToRay(pointer), out Vector2 latLon))
                Select(latLon.x, latLon.y);
        }

        /// <summary>Returns geodetic latitude and east-positive longitude in degrees.</summary>
        public static bool TryIntersect(Ray ray, out Vector2 latLon)
        {
            latLon = default;
            const double a = 6378137.0 / 1000000.0;
            const double b = 6356752.314245 / 1000000.0;
            double x = ray.origin.x, y = ray.origin.y, z = ray.origin.z;
            double dx = ray.direction.x, dy = ray.direction.y, dz = ray.direction.z;
            if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(dx) || !Finite(dy) || !Finite(dz)) return false;
            double aa = (dx * dx + dz * dz) / (a * a) + dy * dy / (b * b);
            if (!(aa > 0) || !Finite(aa)) return false;
            double bb = 2 * ((x * dx + z * dz) / (a * a) + y * dy / (b * b));
            double cc = (x * x + z * z) / (a * a) + y * y / (b * b) - 1;
            double discriminant = bb * bb - 4 * aa * cc;
            if (!Finite(discriminant) || discriminant < 0) return false;
            double root = Math.Sqrt(discriminant);
            double near = (-bb - root) / (2 * aa);
            double far = (-bb + root) / (2 * aa);
            double t = near >= 0 ? near : far;
            if (!Finite(t) || t < 0) return false;
            x += t * dx; y += t * dy; z += t * dz;
            double latitude = Math.Atan2(y / (b * b), Math.Sqrt(x * x + z * z) / (a * a)) * 180 / Math.PI;
            double longitude = Math.Atan2(z, x) * 180 / Math.PI;
            if (!Finite(latitude) || !Finite(longitude)) return false;
            latLon = new Vector2((float)latitude, (float)longitude);
            return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
