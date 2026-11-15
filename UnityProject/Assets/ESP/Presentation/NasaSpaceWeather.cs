using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    // NASA storm catalog only; never substitute these events for atmospheric flight weather.
    public sealed class NasaSpaceWeather : MonoBehaviour
    {
        private const string FeedUrl = "https://raw.githubusercontent.com/araysuter/ESP/nasa-data/donki.json";
        [Serializable] private sealed class Response { public int schemaVersion; public string generatedUtc; public Storm[] items; }
        [Serializable] private sealed class Storm { public string startTime; public KpObservation[] allKpIndex; }
        [Serializable] private sealed class KpObservation { public string observedTime; public float kpIndex; }
        public string Status { get; private set; } = "Loading NASA DONKI geomagnetic storms";

        private IEnumerator Start()
        {
            while (true)
            {
                yield return Load();
                yield return new WaitForSecondsRealtime(1800);
            }
        }
        private IEnumerator Load()
        {
            Response response = null;
            string provenance = null;
            // Only public observations are downloaded. No NASA credential is in the player.
            using (var request = UnityWebRequest.Get(FeedUrl))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var candidate = Parse(request.downloadHandler.text, false);
                    if (candidate != null && candidate.schemaVersion == 1 &&
                        Timestamp(candidate.generatedUtc, out var generated) &&
                        Fresh(generated, DateTime.UtcNow))
                    {
                        response = candidate;
                        provenance = "cached " + generated.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
                    }
                }
            }
            // Retain the existing public NASA endpoint if the scheduled feed is missing,
            // stale or unavailable. This fallback needs no developer API key.
            if (response == null)
            {
                var today = DateTime.UtcNow.Date;
                var url = "https://ccmc.gsfc.nasa.gov/DONKI-API/get/GST?startDate=" +
                    today.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&endDate=" +
                    today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 15;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                        response = Parse(request.downloadHandler.text, true);
                }
                provenance = "direct NASA catalog";
            }
            if (response == null) { Status = "NASA DONKI unavailable; not an all-clear"; yield break; }
            if (response.items.Length == 0)
            {
                Status = "No catalogued GST in past 7 days; not an all-clear · " + provenance;
                yield break;
            }
            Storm latest = null; DateTime latestTime = DateTime.MinValue;
            foreach (var storm in response.items)
                if (storm != null && Timestamp(storm.startTime, out var start) && start <= DateTime.UtcNow.AddMinutes(5) && start > latestTime)
                { latest = storm; latestTime = start; }
            if (latest == null) { Status = "NASA DONKI storm times invalid; not an all-clear"; yield break; }
            KpObservation max = null;
            if (latest.allKpIndex != null)
                foreach (var observation in latest.allKpIndex)
                    if (observation != null && !float.IsNaN(observation.kpIndex) && !float.IsInfinity(observation.kpIndex) &&
                        observation.kpIndex >= 0 && observation.kpIndex <= 9 && Timestamp(observation.observedTime, out _) &&
                        (max == null || observation.kpIndex > max.kpIndex)) max = observation;
            Status = "NASA DONKI GST started " + latest.startTime + (max == null ? "; Kp unavailable" :
                "; max Kp " + max.kpIndex.ToString("0.##", CultureInfo.InvariantCulture) + " observed " + max.observedTime) + " · " + provenance;
        }
        private static Response Parse(string json, bool array)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 2000000) return null;
            try
            {
                var response = JsonUtility.FromJson<Response>(array ? "{\"items\":" + json + "}" : json);
                return response != null && response.items != null && response.items.Length <= 1000 ? response : null;
            }
            catch (Exception) { return null; }
        }
        internal static bool Fresh(DateTime generated, DateTime now) => generated <= now.AddMinutes(5) && generated >= now.AddHours(-2);
        private static bool Timestamp(string text, out DateTime value) => DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }
}
