using System;
using UnityEngine;

namespace ESP.Presentation.Editor
{
    public static class VerifyGlobePicking
    {
        public static void Run()
        {
            Check("equator", new Ray(new Vector3(10, 0, 0), Vector3.left), 0, 0);
            Check("east", new Ray(new Vector3(0, 0, 10), Vector3.back), 0, 90);
            Check("north pole", new Ray(new Vector3(0, 10, 0), Vector3.down), 90, 0);
            Check("south pole", new Ray(new Vector3(0, -10, 0), Vector3.up), -90, 0);
            Check("inside", new Ray(Vector3.zero, Vector3.right), 0, 0);
            if (GlobePointPicker.TryIntersect(new Ray(new Vector3(10, 10, 0), Vector3.right), out _))
                throw new Exception("Miss unexpectedly intersected.");
            Debug.Log("Globe picking verification passed.");
        }

        private static void Check(string name, Ray ray, float latitude, float longitude)
        {
            if (!GlobePointPicker.TryIntersect(ray, out Vector2 result) ||
                Math.Abs(result.x - latitude) > 0.001f || Math.Abs(result.y - longitude) > 0.001f)
                throw new Exception(name + " failed: " + result);
        }
    }
}
