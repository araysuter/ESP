using UnityEngine;
namespace ESP.Presentation
{
    // Presentation-only boundary: the flight adapter owns all simulation state.
    public sealed class RocketPresentation : MonoBehaviour
    {
        public Transform vehicle;
        public Transform booster;
        public Transform exhaust;
        public void SetPose(Vector3 cameraRelativeMeters, Quaternion localAttitude)
        {
            vehicle.SetPositionAndRotation(cameraRelativeMeters, localAttitude);
        }
        public void SetThrottle(float fraction)
        {
            if (exhaust == null) return;
            fraction = Mathf.Clamp01(fraction);
            exhaust.gameObject.SetActive(fraction > .001f);
            exhaust.localScale = new Vector3(.8f, 2f + 6f * fraction, .8f);
        }
        // The flight adapter supplies both stage poses; no invented separation impulse.
        public void SetBoosterPose(Vector3 cameraRelativeMeters, Quaternion localAttitude)
        {
            if (booster == null) return;
            booster.SetParent(null, true);
            booster.SetPositionAndRotation(cameraRelativeMeters, localAttitude);
        }
    }
}
