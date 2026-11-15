using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace ESP.Presentation
{
    /// <summary>NASA J2000 sky, sampled once per pixel with no camera-position parallax.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class StarField : MonoBehaviour
    {
        private Camera targetCamera;
        private Mesh mesh;
        private Material material;
        private MissionSunlight sunlight;
        private FlightEarthBackdrop backdrop;
        private DateTime lastUtc;
        private Vector3d axisX, axisY, axisZ;

        public static StarField Create(Camera camera, Material material, int layer, Texture sky = null)
        {
            if (!camera || !material || !sky) return null;
            var existing = camera.GetComponentInChildren<StarField>(true);
            if (existing) return existing;
            var root = new GameObject("NASA J2000 celestial sky");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = Vector3.forward * .01f;
            root.layer = layer;
            var field = root.AddComponent<StarField>();
            field.targetCamera = camera;
            field.material = material;
            material.SetTexture("_Sky", sky);
            field.sunlight = FindFirstObjectByType<MissionSunlight>();
            field.backdrop = FindFirstObjectByType<FlightEarthBackdrop>();
            field.mesh = new Mesh { name = "Celestial sky screen quad" };
            field.mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            field.mesh.triangles = new[] { 0,1,2,0,2,3 };
            field.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100);
            root.AddComponent<MeshFilter>().sharedMesh = field.mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            Debug.Log("ESP_CELESTIAL_SKY " + sky.width + "x" + sky.height + " format=" + (sky as Texture2D)?.format +
                " bytes=" + UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(sky) + " BC6H=" + SystemInfo.SupportsTextureFormat(TextureFormat.BC6H));
            return field;
        }

        private Vector3 ToWorld(Vector3d ecef)
        {
            if (backdrop && targetCamera == backdrop.BackgroundCamera)
                return CameraRelativeCoordinates.FromEcef(ecef.X, ecef.Y, ecef.Z, 0, 0, 0,
                    backdrop.FrameLatitudeRadians, backdrop.FrameLongitudeRadians);
            return new Vector3((float)ecef.X, (float)ecef.Z, (float)ecef.Y);
        }

        private Vector3 ToJ2000(Vector3 world)
        {
            return new Vector3(Vector3.Dot(world, ToWorld(axisX)), Vector3.Dot(world, ToWorld(axisY)), Vector3.Dot(world, ToWorld(axisZ)));
        }

        private void LateUpdate()
        {
            if (!targetCamera) { Destroy(gameObject); return; }
            DateTime utc = sunlight && sunlight.CurrentUtc != default ? sunlight.CurrentUtc : DateTime.UtcNow;
            if (utc != lastUtc)
            {
                axisX = CelestialFrames.J2000ToEcef(new Vector3d(1,0,0), utc);
                axisY = CelestialFrames.J2000ToEcef(new Vector3d(0,1,0), utc);
                axisZ = CelestialFrames.J2000ToEcef(new Vector3d(0,0,1), utc);
                lastUtc = utc;
            }
            material.SetVector("_ForwardSky", ToJ2000(targetCamera.transform.forward));
            material.SetVector("_RightSky", ToJ2000(targetCamera.transform.right));
            material.SetVector("_UpSky", ToJ2000(targetCamera.transform.up));
            material.SetVector("_ViewParams", new Vector4(Mathf.Tan(targetCamera.fieldOfView * .5f * Mathf.Deg2Rad), targetCamera.aspect, 0, 0));
            var settings = MissionSession.Instance;
            material.SetVector("_SkyAdjust", new Vector4(settings != null ? settings.MilkyWayBrightness / 100 : 1,
                settings != null ? settings.MilkyWaySaturation / 100 : 1, 0, 0));
            if (sunlight != null && sunlight.CurrentUtc != default)
            {
                Vector3 towardSun = ToWorld(sunlight.SunEcef).normalized;
                float distance = SolarSystemMap.AstronomicalUnit;
                var director = sunlight.director;
                bool isMap = director != null && (targetCamera == director.mapCamera ||
                    (director.Inset != null && targetCamera == director.Inset.MapPreviewCamera));
                if (isMap)
                {
                    Vector3 fromCamera = towardSun * SolarSystemMap.AstronomicalUnit - targetCamera.transform.position;
                    distance = Mathf.Max(SolarSystemMap.SunRadius, fromCamera.magnitude);
                    towardSun = fromCamera.normalized;
                }
                Vector3 sunSky = ToJ2000(towardSun).normalized;
                material.SetVector("_SunSky", new Vector4(sunSky.x, sunSky.y, sunSky.z,
                    Mathf.Clamp(SolarSystemMap.SunRadius / distance, .00001f, .95f)));
            }
            else material.SetVector("_SunSky", Vector4.zero);
        }

        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}

