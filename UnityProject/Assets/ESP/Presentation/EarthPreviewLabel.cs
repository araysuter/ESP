using System;
using System.IO;
using UnityEngine;

namespace ESP.Presentation
{
    // A visual-preview label only; this component does not represent flight state.
    public sealed class EarthPreviewLabel : MonoBehaviour
    {
        private bool capture;
        private GUIStyle titleStyle;
        private GUIStyle detailStyle;

        private void Start()
        {
            Application.targetFrameRate=30;Application.runInBackground=true;
            capture = Array.IndexOf(Environment.GetCommandLineArgs(), "-espCapture") >= 0;
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                detailStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    normal = { textColor = new Color(0.78f, 0.87f, 0.91f) }
                };
            }

            GUI.Box(new Rect(20, 20, 330, 110), GUIContent.none);
            GUI.Label(new Rect(34, 29, 300, 25), "EARTH IMAGERY PREVIEW", titleStyle);
            GUI.Label(new Rect(34, 57, 300, 20), "NASA observations  /  WGS84", detailStyle);
            GUI.Label(new Rect(34, 77, 300, 20), "Map scale: 1 unit = 1,000 km", detailStyle);
            GUI.Label(new Rect(34, 97, 300, 20), "Not a simulated flight", detailStyle);
        }

        private void Update()
        {
            if (!capture) return;
            if (Time.frameCount == (Array.IndexOf(Environment.GetCommandLineArgs(), "-espLiveTest")>=0 ? 1800 : 60))
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "esp-earth.png"));
            if (Time.frameCount == (Array.IndexOf(Environment.GetCommandLineArgs(), "-espLiveTest")>=0 ? 1850 : 100)) Application.Quit();
        }
    }
}
