using UnityEngine;
namespace ESP.Presentation
{
    public sealed class PreviewLabel : MonoBehaviour
    {
        void OnGUI()
        {
            GUI.Box(new Rect(20,20,440,76), "ESP / VEHICLE ASSET PREVIEW\nOriginal placeholder • not a simulated mission\nFlight/gameplay integration pending");
        }
        void Start() { Application.targetFrameRate=30; }
        void Update()
        {
            if (!System.Environment.GetCommandLineArgs().ContainsValue("-espCapture")) return;
            if (Time.frameCount==60) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"esp-assets.png"));
            if (Time.frameCount==100) Application.Quit();
        }
    }
    static class Arguments
    {
        public static bool ContainsValue(this string[] args,string value) { return System.Array.IndexOf(args,value)>=0; }
    }
}
