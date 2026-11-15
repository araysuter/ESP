using System;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    public static class KeyboardCameraChecks
    {
        public static void Validate()
        {
            int checkedViews = 0;
            foreach (double latitude in new[] { -89d, -60, -45, -25, 0, 25, 45, 60, 89 })
            foreach (double longitude in new[] { -179d, -80, 0, 90, 179 })
            foreach (double altitude in new[] { 0d, 1000, 100000 })
            foreach (float distance in new[] { 7.2f, 20, 60, 99 })
            {
                var ecef = EarthFrames.GeodeticToEcef(latitude * Math.PI / 180, longitude * Math.PI / 180, altitude);
                var center = new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
                var radial = center.normalized;
                var tangent = Vector3.Cross(radial, Math.Abs(latitude) > 80 ? Vector3.right : Vector3.up).normalized;
                for (int angle = 0; angle < 360; angle += 10)
                {
                    float radians = angle * Mathf.Deg2Rad;
                    var offset = (radial * Mathf.Cos(radians) + tangent * Mathf.Sin(radians)) * distance;
                    var corrected = GlobeCameraControls.KeepEarthVisible(center, offset);
                    Require(!float.IsNaN(corrected.x) && !float.IsInfinity(corrected.x), "Nonfinite camera offset");
                    Require(Mathf.Abs(corrected.magnitude - distance) < .001f, "Camera correction changed zoom distance");
                    Require(!SolarSystemMap.EarthOccludes(center, center + corrected), "Earth still obscures ship at " + latitude + "/" + longitude + " distance " + distance);
                    checkedViews++;
                }
                var outward = radial * distance;
                Require(GlobeCameraControls.KeepEarthVisible(center, outward) == outward, "Unobstructed view changed");
            }
            // Regression: a radial tangent at 45° can still cut the WGS84 ellipsoid.
            var point = EarthFrames.GeodeticToEcef(Math.PI / 4, 0, 0);
            var p = new Vector3((float)(point.X / 1000000), (float)(point.Z / 1000000), 0);
            var r = p.normalized;
            var inwardOffset = (-.9f * r + Mathf.Sqrt(.19f) * new Vector3(r.y, -r.x, 0)) * 99;
            Require(!SolarSystemMap.EarthOccludes(p, p + GlobeCameraControls.KeepEarthVisible(p, inwardOffset)), "Oblate tangent regression");
            Debug.Log("ESP_KEYBOARD_CAMERA_CHECKS_OK views=" + checkedViews + " WGS84 tangent zoom unobstructed");
        }
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
