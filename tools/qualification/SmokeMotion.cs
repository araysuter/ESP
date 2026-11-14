using UnityEngine;

// Temporary infrastructure fixture, not flight/game logic.
public class SmokeMotion : MonoBehaviour
{
    private bool captured;
    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 30;
        Debug.Log($"ESP_SMOKE_START unity={Application.unityVersion} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}");
    }

    private void Update()
    {
        transform.Rotate(0, 45 * Time.deltaTime, 0);
        if (!captured && Time.frameCount >= 90)
        {
            captured = true;
            var path = System.IO.Path.Combine(Application.persistentDataPath, "esp-smoke.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"ESP_SMOKE_FRAME frame={Time.frameCount} elapsed={Time.time:F2} screenshot={path}");
        }
#if !UNITY_EDITOR
        if (Time.frameCount >= 180) Application.Quit(0);
#endif
    }
}
