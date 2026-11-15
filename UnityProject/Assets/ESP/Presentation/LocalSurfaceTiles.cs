using System;
using System.Collections;
using System.Collections.Generic;
using FlightCore;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    /// <summary>
    /// Bounded, visual-only imagery around the spacecraft. WGS84 geometry is
    /// computed in doubles before conversion to the launch site's fixed ENU frame.
    /// No elevation, collision or flight-state information is inferred from imagery.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public sealed class LocalSurfaceTiles : MonoBehaviour
    {
        public RocketFlightDriver driver;
        public MissionDirector director;
        public Shader surfaceShader;
        public Shader offlineLightsShader;
        public Light surfaceSun;
        public bool networkEnabled = true;
        public const string Attribution = "Ground: USDA / USGS The National Map orthoimagery; NASA / USGS Landsat WELD (2000 annual composite); NASA GIBS Blue Marble (MODIS)";
        public string Status { get; private set; } = "Bundled imagery fallback";
        public int ResidentTileCount => tiles.Count;
        public int ActiveRequestCount => requests.Count;
        private const int CacheLimit = 48, ConcurrentRequests = 2, Subdivisions = 16;
        private const double MaximumAltitude = 400000, MercatorLimit = 85.05112878;
        private const string Landsat = "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/Landsat_WELD_CorrectedReflectance_TrueColor_Global_Annual/default/2000-12-01/GoogleMapsCompatible_Level12/";
        private const string BlueMarble = "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/BlueMarble_NextGeneration/default/GoogleMapsCompatible_Level8/";
        private const string Usgs = "https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer/tile/";
        private readonly Dictionary<TileKey, Tile> tiles = new Dictionary<TileKey, Tile>();
        private readonly Dictionary<TileKey, UnityWebRequest> requests = new Dictionary<TileKey, UnityWebRequest>();
        private readonly HashSet<UnityWebRequest> abandonedRequests = new HashSet<UnityWebRequest>();
        private readonly Dictionary<TileKey, float> retryAfter = new Dictionary<TileKey, float>();
        private readonly HashSet<TileKey> wanted = new HashSet<TileKey>();
        private readonly List<TileKey> ordered = new List<TileKey>();
        private readonly List<TileKey> scratch = new List<TileKey>();
        private RocketSimulation previousSimulation;
        private Transform previousEnvironment;
        private Renderer oldGround, offlineLights;
        private Material offlineLightsMaterial;
        private int siteIndex = -1, generation, maximumDetailZoom = 16;
        private double previousMissionTime = -1;
        private float nextSelection, nextRequest;
        private Grid coarseGrid, detailGrid;

        private struct TileKey : IEquatable<TileKey>
        {
            public int Source, Zoom, X, Y;
            public TileKey(int source, int zoom, int x, int y) { Source = source; Zoom = zoom; X = x; Y = y; }
            public bool Equals(TileKey other) => Source == other.Source && Zoom == other.Zoom && X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is TileKey other && Equals(other);
            public override int GetHashCode() => (((Source * 397) ^ Zoom) * 397 ^ X) * 397 ^ Y;
        }
        private struct Grid { public int Zoom, X, Y, Radius; public bool Enabled; }
        private sealed class Tile
        {
            public GameObject Object;
            public Mesh Mesh;
            public Material Material;
            public Texture2D Texture;
            public Vector3d Anchor;
            public float LastUsed, LoadedAt;
        }

        private void LateUpdate()
        {
            if (director == null || driver == null || surfaceShader == null) return;
            var simulation = driver.Simulation;
            double missionTime = simulation != null ? simulation.State.MissionTime : 0;
            bool reset = siteIndex != director.SelectedSiteIndex || previousEnvironment != director.EnvironmentRoot ||
                (previousSimulation != null && !ReferenceEquals(previousSimulation, simulation)) ||
                missionTime < previousMissionTime;
            if (reset)
            {
                ReleaseAll();
                siteIndex = director.SelectedSiteIndex;
                previousEnvironment = director.EnvironmentRoot;
                // Only this legacy visual plane is hidden. Keep authored pad/deck/buildings.
                var legacy = previousEnvironment != null ? previousEnvironment.Find("Visual ground (2 km)") : null;
                oldGround = legacy != null ? legacy.GetComponent<Renderer>() : null;
                if (oldGround != null && offlineLightsShader != null)
                {
                    // Reuse the four-vertex offline plane; never clone globe geometry.
                    var overlay = new GameObject("Observed lights on offline ground");
                    overlay.transform.SetParent(legacy, false);
                    overlay.AddComponent<MeshFilter>().sharedMesh = legacy.GetComponent<MeshFilter>().sharedMesh;
                    offlineLights = overlay.AddComponent<MeshRenderer>();
                    offlineLightsMaterial = new Material(offlineLightsShader);
                    offlineLights.sharedMaterial = offlineLightsMaterial;
                    offlineLights.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    offlineLights.receiveShadows = false;
                }
            }
            previousSimulation = simulation;
            previousMissionTime = missionTime;
            var site = LaunchSites.All[director.SelectedSiteIndex];
            double frameLat = site.Latitude * Math.PI / 180, frameLon = site.Longitude * Math.PI / 180;
            Vector3d origin = simulation != null
                ? EarthFrames.InertialToEcef(simulation.State.Position, Aerodynamics.EarthAngularVelocityRadiansPerSecond * missionTime)
                : EarthFrames.GeodeticToEcef(frameLat, frameLon, site.ElevationMeters);
            var geo = EarthFrames.EcefToGeodetic(origin);
            double latitude = geo.LatitudeRadians * 180 / Math.PI, longitude = geo.LongitudeRadians * 180 / Math.PI;
            var camera = director.FlightRenderCamera;
            bool visible = camera != null && camera.gameObject.activeInHierarchy && director.EnvironmentRoot != null &&
                geo.AltitudeMeters < MaximumAltitude && Math.Abs(latitude) < MercatorLimit;
            if (!visible)
            {
                if (wanted.Count > 0) { wanted.Clear(); ordered.Clear(); AbortUnwanted(); }
                foreach (var tile in tiles.Values) tile.Object.SetActive(false);
                if (oldGround != null) oldGround.enabled = true;
                if (offlineLights != null) offlineLights.enabled = !director.IsMapView;
                return;
            }
            if (!camera.isActiveAndEnabled)
            {
                // A skipped inset frame must not cancel in-flight tile requests,
                // clear selection, or render local terrain into another domain.
                foreach (var tile in tiles.Values) tile.Object.SetActive(false);
                if (oldGround != null) oldGround.enabled = true;
                if (offlineLights != null) offlineLights.enabled = !director.IsMapView;
                return;
            }
            if (Time.unscaledTime >= nextSelection)
            {
                SelectTiles(latitude, longitude, Math.Max(0, geo.AltitudeMeters));
                nextSelection = Time.unscaledTime + .35f;
            }
            float altitudeFade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(300000, (float)MaximumAltitude, (float)geo.AltitudeMeters));
            foreach (var entry in tiles)
            {
                Tile tile = entry.Value;
                bool draw = wanted.Contains(entry.Key);
                tile.Object.SetActive(draw);
                if (!draw) continue;
                tile.LastUsed = Time.unscaledTime;
                tile.Object.transform.position = CameraRelativeCoordinates.FromEcef(tile.Anchor.X, tile.Anchor.Y, tile.Anchor.Z,
                    origin.X, origin.Y, origin.Z, frameLat, frameLon);
                Grid grid = entry.Key.Source == 1 ? detailGrid : coarseGrid;
                int dx = SurfaceTileMath.WrappedDelta(entry.Key.X, grid.X, 1 << grid.Zoom);
                tile.Material.SetVector("_PatchUv", new Vector4(dx + grid.Radius, grid.Y + grid.Radius - entry.Key.Y, 1f / (2 * grid.Radius + 1), 0));
                float detailFade = entry.Key.Source == 1 ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(14000, 18000, (float)geo.AltitudeMeters)) : 1;
                tile.Material.SetFloat("_Opacity", altitudeFade * detailFade * Mathf.Clamp01((Time.unscaledTime - tile.LoadedAt) / .6f));
                tile.Material.SetFloat("_CameraAltitude", (float)geo.AltitudeMeters);
                if (surfaceSun != null)
                {
                    Vector3 toSun = -surfaceSun.transform.forward;
                    tile.Material.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0));
                    tile.Material.SetColor("_SunColor", surfaceSun.color * surfaceSun.intensity);
                }
            }
            // The broad curved tile under the pad replaces the stretched 2 km image.
            // Failed/disabled network requests leave the original offline visual intact.
            if (oldGround != null)
            {
                TileKey padTile = KeyAt(geo.AltitudeMeters >= 60000 ? 2 : 0, coarseGrid.Zoom, site.Latitude, site.Longitude);
                oldGround.enabled = !(wanted.Contains(padTile) && tiles.ContainsKey(padTile));
                if (offlineLights != null) offlineLights.enabled = oldGround.enabled;
            }
            if (networkEnabled && Time.unscaledTime >= nextRequest)
            {
                foreach (TileKey key in ordered)
                {
                    if (requests.Count >= ConcurrentRequests) break;
                    if (tiles.ContainsKey(key) || requests.ContainsKey(key) ||
                        (retryAfter.TryGetValue(key, out float retry) && retry > Time.unscaledTime)) continue;
                    StartCoroutine(Fetch(key, generation));
                    // Limit start rate as well as concurrency when the craft moves quickly.
                    nextRequest = Time.unscaledTime + .15f;
                    break;
                }
            }
        }

        private void SelectTiles(double latitude, double longitude, double altitude)
        {
            wanted.Clear(); ordered.Clear();
            double localScale = Math.Max(.09, Math.Cos(latitude * Math.PI / 180));
            int coarseSource = altitude >= 60000 ? 2 : 0;
            int coarseZoom = Math.Max(5, Math.Min(coarseSource == 2 ? 8 : 12, (int)Math.Floor(Math.Log(40075016.686 * localScale / Math.Max(9000, altitude * 1.6), 2))));
            coarseGrid = MakeGrid(coarseSource, coarseZoom, latitude, longitude, 1);
            // Deliberately exclude Alaska/Hawaii and other countries: their imagery
            // provenance/rights differ. Global public NASA imagery remains available.
            bool conus = latitude >= 24 && latitude <= 49.5 && longitude >= -125 && longitude <= -66;
            int detailZoom = Math.Max(12, Math.Min(maximumDetailZoom, (int)Math.Floor(Math.Log(40075016.686 * localScale / Math.Max(240, altitude * 1.3), 2))));
            detailGrid = conus && altitude < 18000 ? MakeGrid(1, detailZoom, latitude, longitude, 2) : default;
            AddCenter(coarseSource, coarseGrid);
            AddCenter(1, detailGrid);
            AddRing(1, detailGrid, 1);
            AddRing(coarseSource, coarseGrid, 1);
            AddRing(1, detailGrid, 2);
            AbortUnwanted();
            scratch.Clear();
            foreach (var entry in retryAfter) if (!wanted.Contains(entry.Key)) scratch.Add(entry.Key);
            foreach (var key in scratch) retryAfter.Remove(key);
        }

        private static Grid MakeGrid(int source, int zoom, double latitude, double longitude, int radius)
        {
            TileKey key = KeyAt(source, zoom, latitude, longitude);
            return new Grid { Zoom = zoom, X = key.X, Y = key.Y, Radius = radius, Enabled = true };
        }
        private void AddCenter(int source, Grid grid) { if (grid.Enabled) AddKey(source, grid, 0, 0); }
        private void AddRing(int source, Grid grid, int radius)
        {
            if (!grid.Enabled || radius > grid.Radius) return;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (Math.Abs(x) == radius || Math.Abs(y) == radius) AddKey(source, grid, x, y);
        }
        private void AddKey(int source, Grid grid, int dx, int dy)
        {
            int count = 1 << grid.Zoom, y = grid.Y + dy;
            if (y < 0 || y >= count) return;
            var key = new TileKey(source, grid.Zoom, (grid.X + dx + count) % count, y);
            if (wanted.Add(key)) ordered.Add(key);
        }
        private void AbortUnwanted()
        {
            foreach (var entry in requests)
                if (!wanted.Contains(entry.Key)) { abandonedRequests.Add(entry.Value); entry.Value.Abort(); }
        }

        private IEnumerator Fetch(TileKey key, int requestGeneration)
        {
            string url = (key.Source == 1 ? Usgs : key.Source == 2 ? BlueMarble : Landsat) + key.Zoom + "/" + key.Y + "/" + key.X + (key.Source == 1 ? "" : ".jpeg");
            using (var request = UnityWebRequestTexture.GetTexture(url, true))
            {
                request.timeout = 15;
                requests.Add(key, request);
                yield return request.SendWebRequest();
                // A reset clears ownership; never let an old response replace a new tile.
                if (requests.TryGetValue(key, out var current) && ReferenceEquals(current, request)) requests.Remove(key);
                bool abandoned = abandonedRequests.Remove(request);
                Texture2D image = null;
                if (request.result == UnityWebRequest.Result.Success)
                    try { image = DownloadHandlerTexture.GetContent(request); } catch (Exception) { }
                if (requestGeneration != generation || !wanted.Contains(key) || abandoned)
                {
                    if (image != null) Destroy(image);
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    retryAfter[key] = Time.unscaledTime + 45;
                    // Advertised USGS levels exceed populated tiles. Only a missing
                    // center tile reduces LOD; offshore neighbor gaps do not degrade land.
                    if (request.responseCode == 404 && key.Source == 1 && key.Zoom > 12 && key.X == detailGrid.X && key.Y == detailGrid.Y)
                    { maximumDetailZoom = key.Zoom - 1; nextSelection = 0; }
                    Status = "Ground imagery unavailable; bundled fallback retained";
                    yield break;
                }
                if (image == null || image.width != 256 || image.height != 256)
                {
                    if (image != null) Destroy(image);
                    retryAfter[key] = Time.unscaledTime + 60;
                    Status = "Ground tile rejected; bundled fallback retained";
                    yield break;
                }
                EvictForOneTile();
                image.wrapMode = TextureWrapMode.Clamp;
                image.filterMode = FilterMode.Trilinear;
                image.anisoLevel = 4;
                tiles.Add(key, BuildTile(key, image));
                Status = key.Source == 1 ? "USDA / USGS aerial imagery near craft (source resolution varies)" : key.Source == 2 ? "NASA GIBS Blue Marble (MODIS)" : "NASA / USGS Landsat WELD 2000 (30 m source)";
            }
        }

        private Tile BuildTile(TileKey key, Texture2D image)
        {
            var site = LaunchSites.All[director.SelectedSiteIndex];
            double lat = site.Latitude * Math.PI / 180, lon = site.Longitude * Math.PI / 180;
            double n = 1 << key.Zoom;
            // Site elevation is a constant surface approximation, not a terrain model.
            double elevation = site.ElevationMeters - .04;
            Vector3d anchor = EarthFrames.GeodeticToEcef(SurfaceTileMath.LatitudeAtRow(key.Y + .5, n), SurfaceTileMath.LongitudeAtColumn(key.X + .5, n), elevation);
            int side = Subdivisions + 1;
            var vertices = new Vector3[side * side]; var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            var geographicUv = new Vector2[vertices.Length];
            var triangles = new int[Subdivisions * Subdivisions * 6];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    double u = (double)x / Subdivisions, v = (double)y / Subdivisions;
                    double vertexLat = SurfaceTileMath.LatitudeAtRow(key.Y + 1 - v, n), vertexLon = SurfaceTileMath.LongitudeAtColumn(key.X + u, n);
                    Vector3d p = EarthFrames.GeodeticToEcef(vertexLat, vertexLon, elevation);
                    int i = y * side + x;
                    vertices[i] = CameraRelativeCoordinates.FromEcef(p.X, p.Y, p.Z, anchor.X, anchor.Y, anchor.Z, lat, lon);
                    normals[i] = CameraRelativeCoordinates.FromEcef(Math.Cos(vertexLat) * Math.Cos(vertexLon), Math.Cos(vertexLat) * Math.Sin(vertexLon), Math.Sin(vertexLat), 0, 0, 0, lat, lon);
                    uv[i] = new Vector2((float)u, (float)v);
                    geographicUv[i] = new Vector2((float)(vertexLon / (2 * Math.PI) + .5), (float)(vertexLat / Math.PI + .5));
                    if (x < Subdivisions && y < Subdivisions)
                    {
                        int t = (y * Subdivisions + x) * 6;
                        triangles[t] = i; triangles[t + 1] = i + side; triangles[t + 2] = i + side + 1;
                        triangles[t + 3] = i; triangles[t + 4] = i + side + 1; triangles[t + 5] = i + 1;
                    }
                }
            var mesh = new Mesh { name = "WGS84 imagery tile" };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.uv2 = geographicUv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            var material = new Material(surfaceShader) { name = "Geographic surface imagery", renderQueue = key.Source == 1 ? 2991 : 2990 };
            material.SetTexture("_BaseMap", image);
            var obj = new GameObject((key.Source == 1 ? "USGS ortho " : key.Source == 2 ? "NASA Blue Marble " : "NASA Landsat ") + key.Zoom + "/" + key.Y + "/" + key.X);
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            obj.SetActive(false);
            return new Tile { Object = obj, Mesh = mesh, Material = material, Texture = image, Anchor = anchor, LastUsed = Time.unscaledTime, LoadedAt = Time.unscaledTime };
        }

        private void EvictForOneTile()
        {
            while (tiles.Count >= CacheLimit)
            {
                bool found = false; TileKey oldest = default; float oldestTime = float.PositiveInfinity;
                foreach (var entry in tiles)
                    if (!wanted.Contains(entry.Key) && entry.Value.LastUsed < oldestTime) { found = true; oldest = entry.Key; oldestTime = entry.Value.LastUsed; }
                if (!found) return; // Desired set is bounded to 34, below the 48-tile budget.
                ReleaseTile(tiles[oldest]); tiles.Remove(oldest);
            }
        }
        private static TileKey KeyAt(int source, int zoom, double latitude, double longitude)
        {
            int n = 1 << zoom;
            return new TileKey(source, zoom, SurfaceTileMath.ColumnAtLongitude(longitude, n), SurfaceTileMath.RowAtLatitude(latitude, n));
        }
        private void ReleaseTile(Tile tile)
        {
            if (tile.Object != null) { tile.Object.SetActive(false); Destroy(tile.Object); }
            if (tile.Mesh != null) Destroy(tile.Mesh);
            if (tile.Material != null) Destroy(tile.Material);
            if (tile.Texture != null) Destroy(tile.Texture);
        }
        public void ClearTiles() { ReleaseAll(); previousSimulation = null; previousMissionTime = -1; previousEnvironment = null; siteIndex = -1; }
        private void ReleaseAll()
        {
            generation++;
            foreach (var request in requests.Values) request.Abort();
            StopAllCoroutines();
            foreach (var request in requests.Values) request.Dispose();
            requests.Clear(); abandonedRequests.Clear(); wanted.Clear(); ordered.Clear(); retryAfter.Clear();
            foreach (var tile in tiles.Values) ReleaseTile(tile);
            tiles.Clear();
            if (oldGround != null) oldGround.enabled = true;
            oldGround = null;
            if (offlineLights != null) Destroy(offlineLights.gameObject);
            if (offlineLightsMaterial != null) Destroy(offlineLightsMaterial);
            offlineLights = null; offlineLightsMaterial = null;
            nextSelection = nextRequest = 0;
            maximumDetailZoom = 16;
            Status = "Bundled imagery fallback";
        }
        private void OnDisable() { ReleaseAll(); previousEnvironment = null; }
        private void OnDestroy() { ReleaseAll(); }
    }
}
