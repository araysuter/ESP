using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>Camera-relative orbit around the visible flight vehicle; never changes flight state.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FlightCameraControls : MonoBehaviour
    {
        public MissionDirector director;
        private Transform trackedVehicle;
        private Vector3 center;
        private float yaw, pitch, distance;
        private bool dragging;

        private void LateUpdate()
        {
            if (director == null || director.IsMapView || director.RocketVisual == null) { dragging = false; return; }
            Transform vehicle = director.RocketVisual;
            if (!TryVisibleBounds(vehicle, out Bounds bounds)) { dragging = false; return; }
            float radius = Mathf.Max(1f, bounds.extents.magnitude);
            float minDistance = radius * 1.5f;
            float maxDistance = Mathf.Max(minDistance * 2f, radius * 15f);
            if (trackedVehicle != vehicle)
            {
                trackedVehicle = vehicle;
                center = bounds.center;
                Vector3 offset = transform.position - center;
                if (offset.sqrMagnitude < 1f) offset = new Vector3(75, 20, -90);
                yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -80f, 80f);
                distance = Mathf.Clamp(offset.magnitude * Mathf.Max(1f, radius * 2f / 60f), minDistance, maxDistance);
            }
            else
            {
                // Smooth changes in framing when a stage disappears, without changing the chosen zoom.
                center = Vector3.Lerp(center, bounds.center, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            }
            Mouse mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) dragging = false;
            else
            {
                Vector2 pointer = mouse.position.ReadValue();
                bool inside = pointer.x >= 0 && pointer.x < Screen.width && pointer.y >= 0 && pointer.y < Screen.height;
                bool overUI = MissionUI.IsPointerOverUI(pointer);
                if (mouse.leftButton.wasPressedThisFrame) dragging = inside && !overUI;
                if (!inside || mouse.leftButton.wasReleasedThisFrame) dragging = false;
                if (dragging && mouse.leftButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * .2f;
                    pitch = Mathf.Clamp(pitch - delta.y * .2f, -80f, 80f);
                }
                if (inside && !overUI) distance *= Mathf.Exp(-mouse.scroll.ReadValue().y * .003f);
            }
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(p) * Mathf.Sin(y), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Cos(y));
            transform.position = center + direction * distance;
            transform.LookAt(center, Vector3.up);
        }

        private static bool TryVisibleBounds(Transform vehicle, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var view = vehicle.GetComponent<RocketPresentation>();
            foreach (Renderer renderer in vehicle.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || RocketPlume.IsExhaustRenderer(renderer) ||
                    (view != null && view.exhaust != null && renderer.transform.IsChildOf(view.exhaust))) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) dragging = false; }
        private void OnDisable() { dragging = false; }
    }
}
