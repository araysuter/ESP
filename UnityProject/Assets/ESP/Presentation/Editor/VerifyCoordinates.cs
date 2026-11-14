using System;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    // Invocable with Unity's -executeMethod ESP.Presentation.Editor.VerifyCoordinates.Run.
    public static class VerifyCoordinates
    {
        public static void Run()
        {
            Check("origin", CameraRelativeCoordinates.FromEcef(1, 2, 3, 1, 2, 3, 0, 0), 0, 0, 0);
            Check("east", CameraRelativeCoordinates.FromEcef(0, 1, 0, 0, 0, 0, 0, 0), 1, 0, 0);
            Check("north", CameraRelativeCoordinates.FromEcef(0, 0, 1, 0, 0, 0, 0, 0), 0, 0, 1);
            Check("up", CameraRelativeCoordinates.FromEcef(1, 0, 0, 0, 0, 0, 0, 0), 0, 1, 0);
            const double radius = 6378137.0;
            Check("millimeter", CameraRelativeCoordinates.FromEcef(radius + 0.001, 0, 0, radius, 0, 0, 0, 0), 0, 0.001f, 0, 1e-7f);
            Check("pole east", CameraRelativeCoordinates.FromEcef(0, 1, 0, 0, 0, 0, Math.PI / 2, 0), 1, 0, 0);
            Check("pole north", CameraRelativeCoordinates.FromEcef(-1, 0, 0, 0, 0, 0, Math.PI / 2, 0), 0, 0, 1);
            Check("pole up", CameraRelativeCoordinates.FromEcef(0, 0, 1, 0, 0, 0, Math.PI / 2, 0), 0, 1, 0);
            bool rejected = false;
            try { CameraRelativeCoordinates.FromEcef(double.NaN, 0, 0, 0, 0, 0, 0, 0); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new Exception("NaN was not rejected.");
            Debug.Log("Camera-relative coordinate verification passed.");
        }

        private static void Check(string name, Vector3 actual, float x, float y, float z, float tolerance = 1e-6f)
        {
            if (Math.Abs(actual.x - x) > tolerance || Math.Abs(actual.y - y) > tolerance || Math.Abs(actual.z - z) > tolerance)
                throw new Exception(name + " failed: " + actual);
        }
    }
}
