using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ESP.Presentation
{
    /// <summary>Scaled-space visual only. The flight state remains in the flight core.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class FlightEarthBackdrop : MonoBehaviour
    {
        public RocketFlightDriver driver;
        public MissionDirector director;
        public Mesh globeMesh;
        public Material globeMaterial;
        public Shader starShader;
        private Material starMaterial;
        public Light earthSun;
        // Reserve this layer for the backdrop; no other scene objects should use it.
        [Range(0, 31)] public int backdropLayer = 31;
        public double minimumAltitudeMeters = 120000;
        private Camera background;
        private GameObject globe;
        private CloudShell cloudShell;
        private Renderer globeRenderer;
        private Light backgroundSun;
        private UniversalAdditionalCameraData launchData, backgroundData;
        private int originalMask;
        private CameraRenderType originalRenderType;
        private bool configured;

        private void LateUpdate() { Apply(driver, director); }

        public void Apply(RocketFlightDriver flight, MissionDirector mission)
        {
            bool show = flight != null && mission != null && !mission.IsMapView &&
                mission.launchCamera != null && flight.HasPosition &&
                flight.AltitudeMeters >= minimumAltitudeMeters && globeMesh != null && globeMaterial != null;
            if (!show) { Restore(); return; }
            if (!configured && !Configure(mission.launchCamera)) return;

            var site = LaunchSites.All[mission.SelectedSiteIndex];
            double lat = site.Latitude * Math.PI / 180, lon = site.Longitude * Math.PI / 180;
            var state = flight.Simulation.State;
            var ecef = EarthFrames.InertialToEcef(state.Position,
                Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            var center = CameraRelativeCoordinates.FromEcef(0, 0, 0, ecef.X, ecef.Y, ecef.Z, lat, lon);
            globe.transform.position = center / 1000000f;
            // Source mesh axes are ECEF X,Z,Y. Map them into fixed-pad east,up,north.
            Vector3 sourceY = new Vector3(0, (float)Math.Sin(lat), (float)Math.Cos(lat));
            Vector3 sourceZ = new Vector3((float)Math.Cos(lon), (float)(Math.Cos(lat) * Math.Sin(lon)), (float)(-Math.Sin(lat) * Math.Sin(lon)));
            globe.transform.rotation = Quaternion.LookRotation(sourceZ, sourceY);
            // Share the actual selected map material, including newly downloaded imagery.
            var selectedMaterial = mission.mapLayers != null && mission.mapLayers.earth != null
                ? mission.mapLayers.earth.sharedMaterial : globeMaterial;
            if (globeRenderer.sharedMaterial != selectedMaterial) globeRenderer.sharedMaterial = selectedMaterial;
            if (backgroundSun != null && earthSun != null)
                backgroundSun.transform.rotation = globe.transform.rotation * earthSun.transform.rotation;
            background.transform.position = mission.launchCamera.transform.position / 1000000f;
            background.transform.rotation = mission.launchCamera.transform.rotation;
            background.fieldOfView = mission.launchCamera.fieldOfView;
            background.aspect = mission.launchCamera.aspect;
            if (mission.mapLayers != null && mission.mapLayers.CloudsVisible)
                cloudShell.Show(mission.mapLayers.CloudDensityTexture, mission.mapLayers.CloudShader);
            else cloudShell.Hide();
        }

        private bool Configure(Camera launch)
        {
            launchData = launch.GetUniversalAdditionalCameraData();
            originalRenderType = launchData.renderType;
            originalMask = launch.cullingMask;
            var cameraObject = new GameObject("Scaled Earth background camera");
            cameraObject.transform.SetParent(launch.transform, false);
            background = cameraObject.AddComponent<Camera>();
            background.CopyFrom(launch);
            background.cullingMask = 1 << backdropLayer;
            background.nearClipPlane = .00001f;
            background.farClipPlane = 1000f;
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = new Color(.002f, .005f, .014f);
            backgroundData = background.GetUniversalAdditionalCameraData();
            backgroundData.renderType = CameraRenderType.Base;
            launchData.renderType = CameraRenderType.Overlay;
            launch.cullingMask = originalMask & ~(1 << backdropLayer);
            backgroundData.cameraStack.Add(launch);
            if (starShader) { starMaterial = new Material(starShader); StarField.Create(background, starMaterial, backdropLayer); }
            globe = new GameObject("Scaled WGS84 flight backdrop");
            globe.layer = backdropLayer;
            globe.AddComponent<MeshFilter>().sharedMesh = globeMesh;
            globeRenderer = globe.AddComponent<MeshRenderer>();
            globeRenderer.sharedMaterial = globeMaterial;
            if (earthSun != null)
            {
                var sunObject = new GameObject("Earth-frame backdrop sunlight");
                sunObject.layer = backdropLayer;
                sunObject.transform.SetParent(globe.transform, false);
                backgroundSun = sunObject.AddComponent<Light>();
                backgroundSun.type = LightType.Directional;
                backgroundSun.intensity = earthSun.intensity;
                backgroundSun.color = earthSun.color;
                backgroundSun.cullingMask = 1 << backdropLayer;
                backgroundSun.shadows = LightShadows.None;
            }
            cloudShell = globe.AddComponent<CloudShell>();
            configured = true;
            return true;
        }

        private void Restore()
        {
            if (!configured) return;
            if (backgroundData != null && launchData != null)
                backgroundData.cameraStack.Remove(launchData.GetComponent<Camera>());
            if (launchData != null)
            {
                launchData.GetComponent<Camera>().cullingMask = originalMask;
                launchData.renderType = originalRenderType;
            }
            if (background != null) Destroy(background.gameObject);
            if (globe != null) Destroy(globe);
            if (starMaterial != null) Destroy(starMaterial);
            background = null; globe = null; cloudShell = null; configured = false;
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
