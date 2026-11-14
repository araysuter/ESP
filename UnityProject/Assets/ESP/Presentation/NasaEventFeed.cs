using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;

namespace ESP.Presentation
{
    public sealed class NasaEventFeed : MonoBehaviour
    {
        private const string Endpoint = "https://eonet.gsfc.nasa.gov/api/v3/events?category=severeStorms&status=open&days=30&limit=20";

        [Serializable] private sealed class Response { public EventData[] events; }
        [Serializable] private sealed class EventData { public string title; public Geometry[] geometry; }
        [Serializable] private sealed class Geometry { public string date; public string type; public float[] coordinates; }

        public sealed class Entry
        {
            public string Title { get; }
            public string Date { get; }
            public float Latitude { get; }
            public float Longitude { get; }

            internal Entry(string title, string date, float latitude, float longitude)
            {
                Title = title;
                Date = date;
                Latitude = latitude;
                Longitude = longitude;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private ReadOnlyCollection<Entry> readOnlyEntries;
        public string Status { get; private set; } = "Loading NASA EONET events";
        public IReadOnlyList<Entry> Entries => readOnlyEntries ?? (readOnlyEntries = entries.AsReadOnly());

        private IEnumerator Start()
        {
            using (var request = UnityWebRequest.Get(Endpoint))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Status = "NASA EONET unavailable; not an all-clear";
                    yield break;
                }

                Response response;
                try { response = JsonUtility.FromJson<Response>(request.downloadHandler.text); }
                catch (Exception)
                {
                    Status = "NASA EONET unavailable; not an all-clear";
                    yield break;
                }

                if (response == null || response.events == null)
                {
                    Status = "NASA EONET unavailable; not an all-clear";
                    yield break;
                }

                foreach (var item in response.events)
                {
                    if (item == null || item.geometry == null) continue;
                    Geometry latest = null;
                    foreach (var geometry in item.geometry)
                    {
                        if (geometry == null || geometry.type != "Point" || geometry.coordinates == null || geometry.coordinates.Length < 2) continue;
                        if (latest == null || string.CompareOrdinal(geometry.date, latest.date) > 0) latest = geometry;
                    }
                    if (latest != null)
                        entries.Add(new Entry(item.title, latest.date, latest.coordinates[1], latest.coordinates[0]));
                }

                Status = response.events.Length == 0
                    ? "No events returned; not an all-clear"
                    : "Fetched " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC") + "; " + entries.Count + " reports";
                Debug.Log("NASA EONET point events loaded: " + entries.Count);
            }
        }
    }
}
