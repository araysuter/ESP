using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    // Catalog information only; this component does not affect flight physics.
    public sealed class NasaSpaceWeather : MonoBehaviour
    {
        [Serializable] private sealed class Response { public Storm[] items; }
        [Serializable] private sealed class Storm { public string gstID; public string startTime; public KpObservation[] allKpIndex; }
        [Serializable] private sealed class KpObservation { public string observedTime; public float kpIndex; }

        public string Status { get; private set; } = "Loading NASA DONKI geomagnetic storms";

        private IEnumerator Start()
        {
            var today = DateTime.UtcNow.Date;
            var url = "https://ccmc.gsfc.nasa.gov/DONKI-API/get/GST?startDate=" +
                today.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&endDate=" +
                today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Status = "NASA DONKI unavailable; not an all-clear";
                    yield break;
                }

                Response response;
                try { response = JsonUtility.FromJson<Response>("{\"items\":" + request.downloadHandler.text + "}"); }
                catch (Exception)
                {
                    Status = "NASA DONKI unavailable; not an all-clear";
                    yield break;
                }
                if (response == null || response.items == null)
                {
                    Status = "NASA DONKI unavailable; not an all-clear";
                    yield break;
                }

                Storm latest = null;
                foreach (var storm in response.items)
                    if (storm != null && !string.IsNullOrEmpty(storm.startTime) &&
                        (latest == null || string.CompareOrdinal(storm.startTime, latest.startTime) > 0)) latest = storm;
                if (response.items.Length == 0)
                {
                    Status = "No catalogued GST in range — not all clear";
                    yield break;
                }
                if (latest == null)
                {
                    Status = "NASA DONKI response missing storm time; not an all-clear";
                    yield break;
                }

                KpObservation max = null;
                if (latest.allKpIndex != null)
                    foreach (var observation in latest.allKpIndex)
                        if (observation != null && (max == null || observation.kpIndex > max.kpIndex)) max = observation;
                Status = "NASA DONKI GST started " + latest.startTime + (max == null
                    ? "; Kp unavailable"
                    : "; max Kp " + max.kpIndex.ToString("0.##", CultureInfo.InvariantCulture) + " observed " + max.observedTime);
            }
        }
    }
}
