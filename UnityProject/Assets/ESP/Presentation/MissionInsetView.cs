using System;
using FlightCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ESP.Presentation
{
    /// <summary>Live, bounded-cost second view of the same mission objects and state.</summary>
    [DefaultExecutionOrder(180)]
    public sealed class MissionInsetView : MonoBehaviour
    {
        public const int MapLayer = 30;
        public RenderTexture PreviewTexture { get; private set; }
        public bool PreviewIsMap => director != null && !director.IsMapView;
        public bool HasPreview { get; private set; }
        public bool HasMarker { get; private set; }
        public Vector2 MarkerViewportPosition { get; private set; }
        public Camera MapPreviewCamera => mapPreview;
        public Camera FlightPreviewCamera => flightPreview;
        private MissionDirector director;
        private Camera mapPreview, flightPreview;
        private Material skyMaterial;
        private bool previousMapView;
        private float nextFrame;
        private readonly System.Collections.Generic.List<Transform> mapTransforms = new System.Collections.Generic.List<Transform>();

        public void Initialize(MissionDirector owner)
        {
            director = owner;
            PreviewTexture = new RenderTexture(384, 216, 24, RenderTextureFormat.ARGB32)
            {
                name = "Live mission inset 384x216", antiAliasing = 1,
                useMipMap = false, autoGenerateMips = false, filterMode = FilterMode.Bilinear
            };
            PreviewTexture.Create();
            mapPreview = CreateCamera("Live Earth inset camera", director.mapCamera);
            flightPreview = CreateCamera("Live vehicle inset camera", director.launchCamera);
            mapPreview.cullingMask = 1 << MapLayer;
            director.mapCamera.cullingMask = 1 << MapLayer;
            flightPreview.cullingMask &= ~((1 << MapLayer) | (1 << 31));
            director.launchCamera.cullingMask &= ~(1 << MapLayer);
            var controls = flightPreview.gameObject.AddComponent<FlightCameraControls>();
            controls.director = director;
            controls.passiveView = true;
            var backdrop = director.GetComponent<FlightEarthBackdrop>();
            if (backdrop != null && backdrop.starShader != null && backdrop.celestialSky != null)
            {
                skyMaterial = new Material(backdrop.starShader);
                StarField.Create(mapPreview, skyMaterial, MapLayer, backdrop.celestialSky);
            }
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            SynchronizeView();
        }

        private Camera CreateCamera(string label, Camera source)
        {
            var root = new GameObject(label);
            root.transform.SetParent(transform, false);
            var camera = root.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            camera.enabled = false;
            camera.targetTexture = PreviewTexture;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = 384f / 216f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorTexture = false;
            data.requiresDepthTexture = false;
            return camera;
        }

        public void SynchronizeView()
        {
            if (director == null || mapPreview == null) return;
            previousMapView = director.IsMapView;
            HasPreview = false;
            HasMarker = false;
            nextFrame = 0;
            mapPreview.gameObject.SetActive(!director.IsMapView);
            flightPreview.gameObject.SetActive(director.IsMapView);
            mapPreview.enabled = !director.IsMapView;
            flightPreview.enabled = director.IsMapView;
            AssignMapLayers();
            UpdateMapPose();
        }

        private void LateUpdate()
        {
            if (director == null || PreviewTexture == null) return;
            if (previousMapView != director.IsMapView) SynchronizeView();
            // New cloud shells, trajectories and vehicle markers inherit their domain.
            AssignMapLayers();
            UpdateMapPose();
            bool render = Time.unscaledTime >= nextFrame;
            if (render) nextFrame = Time.unscaledTime + .1f;
            mapPreview.enabled = render && !director.IsMapView;
            flightPreview.enabled = render && director.IsMapView;
            // FlightEarthBackdrop runs at 200 and stacks this dedicated overlay above
            // the same continuous Earth. No Camera.Render or temporary scene toggles.
        }

        private void AssignMapLayers()
        {
            if (director.mapRoot != null)
            {
                mapTransforms.Clear();
                director.mapRoot.GetComponentsInChildren(true, mapTransforms);
                foreach (var child in mapTransforms) child.gameObject.layer = MapLayer;
            }
            // Its screen quad must never bleed into the flight backdrop's layer 31.
            var stars = director.mapCamera.GetComponentInChildren<StarField>(true);
            if (stars != null) stars.gameObject.layer = MapLayer;
        }

        private void UpdateMapPose()
        {
            var site = LaunchSites.All[director.SelectedSiteIndex];
            var ecef = EarthFrames.GeodeticToEcef(site.Latitude * Math.PI / 180,
                site.Longitude * Math.PI / 180, site.ElevationMeters);
            if (director.FlightDriver is RocketFlightDriver flight && flight.HasPosition)
            {
                var state = flight.Simulation.State;
                ecef = EarthFrames.InertialToEcef(state.Position,
                    Aerodynamics.EarthAngularVelocityRadiansPerSecond * state.MissionTime);
            }
            Vector3 point = new Vector3((float)(ecef.X / 1000000), (float)(ecef.Z / 1000000), (float)(ecef.Y / 1000000));
            Vector3 radial = point.normalized;
            // A globe view centered beneath the active craft (or selected pad).
            mapPreview.fieldOfView = 45;
            mapPreview.transform.position = radial * Mathf.Max(19f, point.magnitude + 10f);
            mapPreview.transform.rotation = Quaternion.LookRotation(-radial,
                Mathf.Abs(Vector3.Dot(radial, Vector3.up)) > .98f ? Vector3.forward : Vector3.up);
            Vector3 marker = mapPreview.WorldToViewportPoint(point + radial * .02f);
            MarkerViewportPosition = new Vector2(marker.x, marker.y);
            HasMarker = PreviewIsMap && marker.z > 0 && marker.x >= 0 && marker.x <= 1 && marker.y >= 0 && marker.y <= 1;
        }

        private void OnCameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (camera == (PreviewIsMap ? mapPreview : flightPreview)) HasPreview = true;
        }

        private void OnDisable()
        {
            if (mapPreview != null) mapPreview.enabled = false;
            if (flightPreview != null) flightPreview.enabled = false;
            HasPreview = false;
            director?.RefreshFlightBackdrop();
        }

        private void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            if (mapPreview != null) Destroy(mapPreview.gameObject);
            if (flightPreview != null) Destroy(flightPreview.gameObject);
            if (skyMaterial != null) Destroy(skyMaterial);
            if (PreviewTexture != null) { PreviewTexture.Release(); Destroy(PreviewTexture); }
        }
    }
}
