using System;
using FlightCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Compact spherical surface attitude instrument, sourced only from flight-core state.</summary>
    public sealed class NavballElement : VisualElement
    {
        private const int Resolution = 192;
        private const float Radius = .44f;
        private readonly VisualElement ball, prograde;
        private readonly Label heading, mode;
        private readonly Label[] markings = new Label[9];
        private readonly Color32[] pixels = new Color32[Resolution * Resolution];
        private Texture2D texture;
        private NavballProjection reading = NavballProjection.Pad(90);
        private bool needsPaint = true;
        private double paintedPitch = double.NaN, paintedHeading = double.NaN;
        private static readonly string[] Cardinals = { "N", "E", "S", "W" };
        private static readonly Color Amber = new Color(1f, .76f, .26f);
        private static readonly Color Green = new Color(.68f, 1f, .36f);

        public NavballElement()
        {
            name = "surface-navball";
            pickingMode = PickingMode.Ignore;
            style.width = 120; style.height = 120;
            style.flexShrink = 0;
            tooltip = "Surface attitude from actual flight-core pitch and heading at the vehicle's WGS84 location. " +
                "Green: surface-relative prograde (hidden below 1 m/s or on the back hemisphere). " +
                "Roll is not simulated; the ball uses a zero-bank reference. Independent of the camera.";
            ball = new VisualElement { pickingMode = PickingMode.Ignore };
            Position(ball, 0, 0, 100, 100);
            Add(ball);
            heading = Text("HDG 090°", 9, new Color(.90f, .95f, 1f));
            Position(heading, 10, 0, 80, 12); Add(heading);
            mode = Text("SURFACE", 7, new Color(.68f, .77f, .82f));
            Position(mode, 0, 91, 100, 9); Add(mode);
            for (int i = 0; i < markings.Length; i++)
            {
                markings[i] = Text("", 8, new Color(.91f, .96f, .98f, .82f));
                Add(markings[i]);
            }
            // Fixed nose/wing reference, not a velocity or camera reticle.
            Bar(35, 49.2f, 11, 1.8f, Amber); Bar(54, 49.2f, 11, 1.8f, Amber);
            Bar(44.5f, 49.2f, 1.8f, 4, Amber); Bar(53.7f, 49.2f, 1.8f, 4, Amber);
            Bar(49.2f, 48.5f, 1.6f, 3, Amber);
            prograde = new VisualElement { pickingMode = PickingMode.Ignore };
            prograde.style.position = UnityEngine.UIElements.Position.Absolute;
            prograde.style.width = Length.Percent(8); prograde.style.height = Length.Percent(8);
            prograde.style.borderTopLeftRadius = Length.Percent(50); prograde.style.borderTopRightRadius = Length.Percent(50);
            prograde.style.borderBottomLeftRadius = Length.Percent(50); prograde.style.borderBottomRightRadius = Length.Percent(50);
            prograde.style.borderTopWidth = 1.4f; prograde.style.borderBottomWidth = 1.4f;
            prograde.style.borderLeftWidth = 1.4f; prograde.style.borderRightWidth = 1.4f;
            prograde.style.borderTopColor = Green; prograde.style.borderBottomColor = Green;
            prograde.style.borderLeftColor = Green; prograde.style.borderRightColor = Green;
            prograde.style.display = DisplayStyle.None; Add(prograde);
            RegisterCallback<AttachToPanelEvent>(_ => { needsPaint = true; Paint(); });
            RegisterCallback<DetachFromPanelEvent>(_ => {
                ball.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                if (texture != null) UnityEngine.Object.Destroy(texture);
                texture = null; needsPaint = true;
            });
            UpdateLabels();
        }

        public void SetFlight(RocketFlightDriver driver, LaunchSite site)
        {
            RocketSimulation simulation = driver != null ? driver.Simulation : null;
            NavballProjection next;
            if (simulation == null) next = NavballProjection.Pad(site != null ? site.HeadingDegrees : 90);
            else
            {
                RocketSnapshot state = simulation.State;
                next = NavballProjection.FromFlight(state.Position, state.Velocity, state.MissionTime,
                    simulation.ActualPitchDegrees, simulation.ActualHeadingDegrees,
                    simulation.ManualRotation ? simulation.ThrustDirectionInertial : (Vector3d?)null,
                    simulation.ManualRotation ? simulation.ManualForwardInertial : (Vector3d?)null);
                if (simulation.ManualRotation) needsPaint = true;
            }
            if (double.IsNaN(paintedPitch) || Math.Abs(next.PitchDegrees - paintedPitch) > .02 ||
                Math.Abs(next.HeadingDegrees - paintedHeading) > .02) needsPaint = true;
            reading = next;
            UpdateLabels();
            Paint();
        }

        private void Paint()
        {
            if (!needsPaint || panel == null) return;
            if (texture == null)
            {
                texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false) {
                    name = "ESP runtime surface navball", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                ball.style.backgroundImage = new StyleBackground(texture);
            }
            // Orthographic sphere, sampled in instrument space. Latitude is relative
            // to geodetic up; heading increases north->east. No world/camera floats.
            float r = Resolution * Radius;
            for (int py = 0; py < Resolution; py++)
            for (int px = 0; px < Resolution; px++)
            {
                double x = (px + .5 - Resolution * .5) / r;
                double y = (py + .5 - Resolution * .5) / r;
                double squared = x * x + y * y;
                if (squared >= 1) { pixels[py * Resolution + px] = new Color32(0, 0, 0, 0); continue; }
                double z = Math.Sqrt(1 - squared);
                Vector3d direction = reading.Unproject(x, y, z);
                double latitude = Math.Asin(Math.Max(-1, Math.Min(1, direction.Z))) * 180 / Math.PI;
                double longitude = Math.Atan2(direction.X, direction.Y) * 180 / Math.PI;
                float light = .59f + .41f * (float)z;
                Color color = latitude >= 0 ? new Color(.12f, .43f, .66f) : new Color(.42f, .24f, .15f);
                color *= light; color.a = 1;
                double latitudeLine = Math.Abs(latitude - Math.Round(latitude / 30) * 30);
                double longitudeLine = Math.Abs(longitude - Math.Round(longitude / 30) * 30);
                if (latitudeLine < .45 || longitudeLine * Math.Cos(latitude * Math.PI / 180) < .30)
                    color = Color.Lerp(color, new Color(.8f, .9f, .95f), .22f);
                double headingDistance = Math.Abs((longitude - reading.HeadingDegrees + 540) % 360 - 180);
                double pitchTick = Math.Abs(latitude - Math.Round(latitude / 10) * 10);
                if (pitchTick < .55 && headingDistance < 9 && Math.Abs(latitude) < 85)
                    color = Color.Lerp(color, Color.white, .70f);
                if (Math.Abs(latitude) < .7) color = new Color(.86f, .94f, .96f);
                double edge = Math.Sqrt(squared);
                if (edge > .967) color = new Color(.20f, .26f, .29f);
                color.a = Mathf.Clamp01((float)((1 - edge) * r));
                pixels[py * Resolution + px] = color;
            }
            texture.SetPixels32(pixels); texture.Apply(false, false); needsPaint = false;
            paintedPitch = reading.PitchDegrees; paintedHeading = reading.HeadingDegrees;
        }

        private void UpdateLabels()
        {
            heading.text = "HDG " + ((int)Math.Round(reading.HeadingDegrees) % 360).ToString("000") + "°";
            for (int i = 0; i < 4; i++) Place(markings[i], Cardinals[i], NavballProjection.Direction(0, i * 90));
            for (int i = 0; i < 5; i++)
            {
                int pitch = (i - 2) * 30;
                Place(markings[i + 4], pitch == 0 ? "0" : pitch.ToString(), NavballProjection.Direction(pitch, reading.HeadingDegrees + 15));
            }
            Vector3d p = reading.Project(reading.SurfacePrograde);
            // Keep the complete symbol inside the rim and suppress undefined pad velocity.
            bool show = reading.HasPrograde && p.Z > 0 && p.X * p.X + p.Y * p.Y < .82;
            prograde.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) { prograde.style.left = Length.Percent(46 + (float)p.X * Radius * 100); prograde.style.top = Length.Percent(46 - (float)p.Y * Radius * 100); }
        }

        private void Place(Label label, string text, Vector3d direction)
        {
            Vector3d p = reading.Project(direction);
            bool visible = p.Z > .12 && p.X * p.X + p.Y * p.Y < .88;
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            label.text = text;
            Position(label, 44 + (float)p.X * Radius * 100, 46 - (float)p.Y * Radius * 100, 12, 8);
        }

        private static Label Text(string text, int size, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size; label.style.color = color;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginLeft = 0; label.style.marginRight = 0; label.style.marginTop = 0; label.style.marginBottom = 0;
            label.style.paddingLeft = 0; label.style.paddingRight = 0; label.style.paddingTop = 0; label.style.paddingBottom = 0;
            return label;
        }
        private void Bar(float left, float top, float width, float height, Color color)
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore }; Position(bar, left, top, width, height);
            bar.style.backgroundColor = color; Add(bar);
        }
        private static void Position(VisualElement element, float left, float top, float width, float height)
        {
            element.style.position = UnityEngine.UIElements.Position.Absolute;
            element.style.left = Length.Percent(left); element.style.top = Length.Percent(top);
            element.style.width = Length.Percent(width); element.style.height = Length.Percent(height);
        }
    }
}
