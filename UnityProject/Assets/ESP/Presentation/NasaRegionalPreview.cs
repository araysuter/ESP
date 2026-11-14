using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    // Regional WMS detail inset; pixels do not imply the source sensor's resolution.
    public sealed class NasaRegionalPreview : MonoBehaviour
    {
        private const string Endpoint = "https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi";
        private int generation;
        private UnityWebRequest activeRequest;
        public Texture2D Image { get; private set; }
        public string Status { get; private set; } = "No regional image requested";

        public void Fetch(double lat, double lon, string layer, string date)
        {
            generation++;
            if(Image!=null){Destroy(Image);Image=null;}
            if (activeRequest != null) activeRequest.Abort();
            DateTime parsed;
            bool isStatic=layer=="BlueMarble_ShadedRelief_Bathymetry";
            if (double.IsNaN(lat) || double.IsInfinity(lat) || double.IsNaN(lon) || double.IsInfinity(lon) ||
                lat < -90 || lat > 90 || lon < -180 || lon > 180 || string.IsNullOrWhiteSpace(layer) ||
                (!isStatic && !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)))
            {
                Status = "Invalid regional request; no regional image displayed";
                return;
            }
            double south = Math.Max(-90, lat - 5), north = Math.Min(90, lat + 5);
            double west = Math.Max(-180, lon - 5), east = Math.Min(180, lon + 5);
            string bbox = string.Format(CultureInfo.InvariantCulture, "{0:0.######},{1:0.######},{2:0.######},{3:0.######}", south, west, north, east);
            string description = "bbox (S,W,N,E) " + bbox + " | " + layer + " | " + (isStatic?"static composite (no observation day)":date) + " | 1024 × 1024 pixels; source resolution varies";
            Status = "Fetching " + description;
            StartCoroutine(Load(layer, date, bbox, description, generation));
        }

        private IEnumerator Load(string layer, string date, string bbox, string description, int id)
        {
            string url = Endpoint + "?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=" + UnityWebRequest.EscapeURL(layer) +
                "&STYLES=&CRS=EPSG%3A4326&BBOX=" + UnityWebRequest.EscapeURL(bbox) +
                "&WIDTH=1024&HEIGHT=1024&FORMAT=image%2Fpng" + (layer=="BlueMarble_ShadedRelief_Bathymetry" ? "" : "&TIME="+UnityWebRequest.EscapeURL(date));
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                activeRequest = request;
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (id != generation) yield break;
                activeRequest = null;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Status = "Regional image unavailable; no regional image displayed | " + description;
                    yield break;
                }
                Texture2D texture = null;
                try { texture = DownloadHandlerTexture.GetContent(request); }
                catch (Exception) { }
                if (texture == null || texture.width != 1024 || texture.height != 1024)
                {
                    if (texture != null) Destroy(texture);
                    Status = "Invalid regional image; no regional image displayed | " + description;
                    yield break;
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                if (Image != null) Destroy(Image);
                Image = texture;
                Status = "NASA GIBS regional image | " + description;
                Debug.Log("ESP_NASA_REGION_OK "+description);
            }
        }

        private void OnDestroy()
        {
            generation++;
            if (activeRequest != null) activeRequest.Abort();
            if (Image != null) Destroy(Image);
        }
    }
}
