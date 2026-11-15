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
        public float SolarAngularDiameterDegrees { get; private set; }
        public float SolarFlareVisibility { get; private set; }
        public float StellarZenithVisibility { get; private set; } = 1;
        public const float SolarDiscPeakDisplayRadiance = 32f;
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
            float brightness = settings != null ? settings.MilkyWayBrightness : MissionSession.DefaultMilkyWayBrightness;
            float saturation = settings != null ? settings.MilkyWaySaturation : MissionSession.DefaultMilkyWaySaturation;
            material.SetVector("_SkyAdjust", new Vector4(brightness / 100f, saturation / 100f, 0, 0));
            // Stellar exposure is independent of solar transmission. The atmosphere
            // must not erase the bright photosphere to hide much fainter stars.
            StellarZenithVisibility = 1;
            material.SetVector("_SkyDaylight", Vector4.zero);
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
                float sourceVisibility = 1;
                if (backdrop && targetCamera == backdrop.BackgroundCamera)
                {
                    // Reconstruct the actual camera ECEF position in double precision.
                    // Camera is scaled ENU; the authoritative origin remains unchanged.
                    double lat = backdrop.FrameLatitudeRadians, lon = backdrop.FrameLongitudeRadians;
                    Vector3 offset = targetCamera.transform.position * 1000000f;
                    double sl = Math.Sin(lat), cl = Math.Cos(lat), so = Math.Sin(lon), co = Math.Cos(lon);
                    var cameraEcef = backdrop.RenderOriginEcef + new Vector3d(
                        -so * offset.x + cl * co * offset.y - sl * co * offset.z,
                        co * offset.x + cl * so * offset.y - sl * so * offset.z,
                        sl * offset.y + cl * offset.z);
                    sourceVisibility = SolarLighting.IsOccluded(cameraEcef, sunlight.SunEcef) ? 0 : 1;
                    var location = EarthFrames.EcefToGeodetic(cameraEcef);
                    double cameraLat = location.LatitudeRadians, cameraLon = location.LongitudeRadians;
                    var upEcef = new Vector3d(Math.Cos(cameraLat) * Math.Cos(cameraLon),
                        Math.Cos(cameraLat) * Math.Sin(cameraLon), Math.Sin(cameraLat));
                    Vector3 upSky = ToJ2000(ToWorld(upEcef)).normalized;
                    float elevation = (float)SolarLighting.ElevationDegrees(sunlight.SunEcef, cameraLat, cameraLon);
                    float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-12, 0, elevation));
                    // Display exposure, not atmospheric extinction: keep the night
                    // sky intact and recover space stars continuously with altitude.
                    float air = Mathf.Exp(-Mathf.Max(0, (float)location.AltitudeMeters) / 20000f);
                    float stellarExposure = 14 * daylight * air;
                    StellarZenithVisibility = Mathf.Exp(-stellarExposure);
                    material.SetVector("_SkyDaylight", new Vector4(upSky.x, upSky.y, upSky.z, stellarExposure));
                }
                else if (isMap)
                    sourceVisibility = SolarSystemMap.EarthOccludes(towardSun * distance + targetCamera.transform.position,
                        targetCamera.transform.position) ? 0 : 1;
                SolarFlareVisibility = sourceVisibility;
                SolarAngularDiameterDegrees = 2 * Mathf.Asin(Mathf.Clamp01(SolarSystemMap.SunRadius / distance)) * Mathf.Rad2Deg;
                material.SetVector("_SolarOptics", new Vector4(sourceVisibility, 0, 0, 0));
                Vector3 sunSky = ToJ2000(towardSun).normalized;
                material.SetVector("_SunSky", new Vector4(sunSky.x, sunSky.y, sunSky.z,
                    Mathf.Clamp(SolarSystemMap.SunRadius / distance, .00001f, .95f)));
            }
            else
            {
                SolarAngularDiameterDegrees = SolarFlareVisibility = 0;
                material.SetVector("_SunSky", Vector4.zero);
                material.SetVector("_SolarOptics", Vector4.zero);
            }
        }

        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
