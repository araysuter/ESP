using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Opt-in development player visual regression route. Never changes flight state.</summary>
    public sealed class AuroraReviewSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-espAuroraReview") < 0) return;
            var director = FindFirstObjectByType<MissionDirector>();
            if (director) director.gameObject.AddComponent<AuroraReviewSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null; yield return null;
            var director = GetComponent<MissionDirector>();
            var session = MissionSession.Instance;
            session.NewMission(); director.ShowMap();
            var camera = director.mapCamera;
            camera.GetComponent<GlobeCameraControls>().enabled = false;
            var effect = director.mapRoot.GetComponentInChildren<AuroraCurtains>();
            if (!effect || !effect.CurtainMaterial.shader.isSupported)
            { Debug.LogError("ESP_AURORA_FAIL shader or effect missing"); Application.Quit(1); yield break; }
            var sun = GetComponent<MissionSunlight>();
            sun.ReviewUtc = new DateTime(2026,11,15,6,0,0,DateTimeKind.Utc);
            effect.ReviewSun = -Magnetic(60,0);
            effect.ReviewSeconds = 120;
            effect.ReviewSettings = new Vector3(5,150,300);
            // Exercise real UI callbacks, ranges, and persistence setters.
            session.OpenSettings(); yield return null; yield return null;
            var document = FindFirstObjectByType<MissionMenuUI>().GetComponent<UIDocument>();
            var kp = document.rootVisualElement.Q<Slider>("aurora-kp");
            var color = document.rootVisualElement.Q<Slider>("aurora-color-boost");
            var intensity = document.rootVisualElement.Q<Slider>("aurora-intensity");
            if (kp == null || color == null || intensity == null || kp.lowValue != 0 || kp.highValue != 9 ||
                color.lowValue != 50 || color.highValue != 500 || intensity.lowValue != 50 || intensity.highValue != 500)
            { Debug.LogError("ESP_AURORA_FAIL settings controls/ranges"); Application.Quit(1); yield break; }
            Vector3 saved = new Vector3(session.AuroraKp,session.AuroraColorBoost,session.AuroraIntensity);
            kp.value=9; color.value=500; intensity.value=50;
            if (session.AuroraKp!=9 || session.AuroraColorBoost!=500 || session.AuroraIntensity!=50)
            { Debug.LogError("ESP_AURORA_FAIL settings callbacks"); Application.Quit(1); yield break; }
            session.SetAuroraKp(float.NaN); session.SetAuroraColorBoost(float.PositiveInfinity); session.SetAuroraIntensity(-100);
            if (session.AuroraKp!=AuroraCurtains.DefaultKp || session.AuroraColorBoost!=AuroraCurtains.DefaultColor || session.AuroraIntensity!=50)
            { Debug.LogError("ESP_AURORA_FAIL nonfinite/range protection"); Application.Quit(1); yield break; }
            kp.value=5; color.value=150; intensity.value=300;
            document.rootVisualElement.Q<ScrollView>("session-menu-content").ScrollTo(kp);
            yield return Shot("settings");
            session.SetAuroraKp(saved.x); session.SetAuroraColorBoost(saved.y); session.SetAuroraIntensity(saved.z); PlayerPrefs.Save();
            session.ResumeMission();
            foreach(var ui in FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) ui.rootVisualElement.style.display=DisplayStyle.None;
            if (director.Inset) director.Inset.enabled=false;
            // Place only the review camera. Underlying flight state stays untouched.
            Vector3 radial = Magnetic(60,0), north = (Magnetic(60.1f,0)-radial).normalized;
            camera.nearClipPlane=.000001f; camera.farClipPlane=100; camera.fieldOfView=75;
            camera.transform.position=Vector3.Scale(radial,new Vector3(6.378137f,6.3567523f,6.378137f)) + radial*.0001f;
            camera.transform.rotation=Quaternion.LookRotation((north*.85f+radial*.52f).normalized,radial);
            yield return Shot("ground-enhanced");
            effect.ReviewSettings=new Vector3(5,50,300); yield return Shot("ground-color50");
            effect.ReviewSettings=new Vector3(5,500,300); yield return Shot("ground-color500");
            effect.ReviewSettings=new Vector3(5,150,50); yield return Shot("ground-intensity50");
            effect.ReviewSettings=new Vector3(5,150,500); yield return Shot("ground-intensity500");
            effect.ReviewSettings=new Vector3(5,50,50); yield return Shot("ground-low");
            effect.ReviewSettings=new Vector3(5,500,500); yield return Shot("ground-high");
            effect.ReviewSettings=new Vector3(5,150,300);
            effect.ReviewSeconds=180; yield return Shot("ground-motion");
            camera.transform.position=Magnetic(55,0)*6.9f;
            camera.transform.rotation=Quaternion.LookRotation(Magnetic(66,0)*6.52f-camera.transform.position,Magnetic(55,0));
            camera.fieldOfView=65;
            yield return Shot("orbit-enhanced");
            effect.ReviewSettings=new Vector3(0,150,300); yield return Shot("orbit-kp0");
            effect.ReviewSettings=new Vector3(9,150,300); yield return Shot("orbit-kp9");
            effect.ReviewSettings=new Vector3(5,150,300);
            effect.ReviewSun=Magnetic(60,0); yield return Shot("orbit-day");
            effect.ReviewSun=-Magnetic(60,0);
            QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            var renderer=effect.GetComponent<MeshRenderer>();
            foreach(bool on in new[]{false,true,false,true})
            {
                renderer.enabled=on;
                for(int i=0;i<40;i++) yield return null;
                var times=new List<float>();
                for(int i=0;i<180;i++) { yield return null; times.Add(Time.unscaledDeltaTime*1000); }
                times.Sort();
                Debug.Log("ESP_AURORA_TIMING enabled="+on+" p50="+times[90].ToString("F3")+" p95="+times[171].ToString("F3")+" resolution="+Screen.width+"x"+Screen.height);
            }
            Debug.Log("ESP_AURORA_OK shader="+effect.CurtainMaterial.shader.name+" graphics="+SystemInfo.graphicsDeviceName);
            Application.Quit();
        }
        private static Vector3 Magnetic(float latitude,float longitude)
        {
            float lat=latitude*Mathf.Deg2Rad,lon=longitude*Mathf.Deg2Rad;
            Vector3 pole=new Vector3(.0486f,.9866f,-.1559f),east=new Vector3(.9549f,0,.2969f);
            return (east*(Mathf.Cos(lat)*Mathf.Cos(lon))+pole*Mathf.Sin(lat)+Vector3.Cross(pole,east)*(Mathf.Cos(lat)*Mathf.Sin(lon))).normalized;
        }
        private IEnumerator Shot(string name)
        {
            for(int i=0;i<8;i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"aurora-"+name+".png"));
            yield return new WaitForSecondsRealtime(.4f);
        }
    }
}
