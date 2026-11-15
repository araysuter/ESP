using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    /// <summary>Opt-in development-player captures and paired frame-time samples.</summary>
    public sealed class CelestialSkySmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espCelestialSkySmoke") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director) director.gameObject.AddComponent<CelestialSkySmoke>();
        }
        private IEnumerator Start()
        {
            yield return null;
            var director = GetComponent<MissionDirector>();
            var lighting = GetComponent<MissionSunlight>();
            MissionSession.Instance.NewMission(); director.SelectVehicle(0); director.ShowMap();
            lighting.ReviewUtc = new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc);
            yield return Shot("map");
            var cam = director.mapCamera;
            cam.GetComponent<GlobeCameraControls>().enabled = false;
            // Galactic-center region near Sagittarius, above the unobstructed sky.
            double ra=266.4*Math.PI/180, dec=-28.94*Math.PI/180;
            var ecef=CelestialFrames.J2000ToEcef(new Vector3d(Math.Cos(dec)*Math.Cos(ra),Math.Cos(dec)*Math.Sin(ra),Math.Sin(dec)),lighting.ReviewUtc.Value);
            var direction=new Vector3((float)ecef.X,(float)ecef.Z,(float)ecef.Y);
            cam.transform.position=direction*20;
            cam.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            yield return Shot("sagittarius");
            var field=cam.GetComponentInChildren<StarField>();
            if (!field) { Debug.LogError("ESP_SKY_FAIL missing field"); Application.Quit(1); yield break; }
            var texture=field.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_Sky") as Texture2D;
            if (!texture || texture.format!=TextureFormat.BC6H || texture.isReadable || texture.width!=8192)
            { Debug.LogError("ESP_SKY_FAIL texture budget/import"); Application.Quit(1); yield break; }
            QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            var renderer=field.GetComponent<MeshRenderer>();
            foreach (bool on in new[] { false,true,false,true })
            {
                renderer.enabled=on;
                for(int i=0;i<30;i++) yield return null;
                var times=new List<float>();
                for(int i=0;i<240;i++) { yield return null; times.Add(Time.unscaledDeltaTime*1000); }
                times.Sort(); double total=0; foreach(float time in times) total+=time;
                Debug.Log("ESP_SKY_TIMING enabled="+on+" frames="+times.Count+" meanMs="+(total/times.Count).ToString("F3")+" p50Ms="+times[times.Count/2].ToString("F3")+" p95Ms="+times[(int)(times.Count*.95)].ToString("F3")+" resolution="+Screen.width+"x"+Screen.height);
            }
            renderer.enabled=true; QualitySettings.vSyncCount=1;
            lighting.ReviewUtc=new DateTime(2026,3,20,6,0,0,DateTimeKind.Utc);
            director.GoToLaunch(0);
            director.launchCamera.GetComponent<FlightCameraControls>().enabled=false;
            director.launchCamera.transform.position=new Vector3(75,38,-90);
            director.launchCamera.transform.rotation=Quaternion.Euler(-25,0,0);
            yield return Shot("night-pad");
            lighting.ReviewUtc=new DateTime(2026,3,20,18,0,0,DateTimeKind.Utc);
            yield return Shot("day-pad");
            lighting.ReviewUtc=null;
            Debug.Log("ESP_SKY_OK"); Application.Quit();
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"celestial-"+name+".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
    }
}
