using UnityEngine;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>
    /// Orbits the camera around the map origin (or an optional target). The globe
    /// stays fixed in its geographic frame. Zoom is centered on the target, not
    /// the cursor, and is limited to the resolution of the existing imagery.
    /// </summary>
    public sealed class GlobeCameraControls : MonoBehaviour
    {
        public Transform target;
        [SerializeField] private float degreesPerPixel = 0.2f;
        [SerializeField] private float zoomSensitivity = 0.001f;
        private float yaw;
        private float pitch;
        private float distance;
        private bool dragging;

        private Vector3 Center => target != null ? target.position : Vector3.zero;

        private void Start()
        {
            Vector3 offset = transform.position - Center;
            distance = Mathf.Clamp(offset.magnitude, 7.2f, 35f);
            if (offset.sqrMagnitude < 1e-8f) offset = Vector3.back;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -85f, 85f);
            ApplyPose();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) { dragging = false; return; }
            Vector2 pointer = mouse.position.ReadValue();
            bool overPanel = (pointer.x>Screen.width-355 && pointer.y>Screen.height-350) || pointer.y < 180f || (pointer.x < 365f && pointer.y > Screen.height - 535f);
            if (mouse.leftButton.wasPressedThisFrame) dragging = !overPanel;
            if (mouse.leftButton.wasReleasedThisFrame) dragging = false;
            if (dragging && mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw -= delta.x * degreesPerPixel;
                pitch = Mathf.Clamp(pitch - delta.y * degreesPerPixel, -85f, 85f);
            }
            if (!overPanel)
            {
                float scroll = mouse.scroll.ReadValue().y;
                distance = Mathf.Clamp(distance * Mathf.Exp(-scroll * zoomSensitivity), 7.2f, 35f);
            }
            ApplyPose();
        }

        public void Focus(double latitude,double longitude){yaw=90-(float)longitude;pitch=Mathf.Clamp((float)latitude,-85,85);ApplyPose();}

        private void ApplyPose()
        {
            float yawRadians = yaw * Mathf.Deg2Rad;
            float pitchRadians = pitch * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitchRadians);
            Vector3 offset = new Vector3(horizontal * Mathf.Sin(yawRadians), Mathf.Sin(pitchRadians), horizontal * Mathf.Cos(yawRadians)) * distance;
            transform.position = Center + offset;
            transform.LookAt(Center, Vector3.up);
        }
    }
}
