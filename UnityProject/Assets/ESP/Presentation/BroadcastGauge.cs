using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Open 240-degree instrument outline; the readout is unscaled flight telemetry.</summary>
    public sealed class BroadcastGauge : VisualElement
    {
        private readonly Label value;
        private readonly VisualElement copy;

        public BroadcastGauge(string title, string units, string readoutName)
        {
            name = readoutName + "-gauge";
            pickingMode = PickingMode.Ignore;
            style.flexShrink = 0;
            copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.style.position = Position.Absolute;
            copy.style.left = 0; copy.style.right = 0;
            copy.style.top = Length.Percent(25);
            copy.style.alignItems = Align.Center;
            copy.Add(Text(title, 10));
            value = Text("—", 32); value.name = readoutName;
            value.style.marginTop = 2; value.style.marginBottom = 0;
            copy.Add(value);
            var unit = Text(units, 9); unit.style.marginTop = 0;
            copy.Add(unit); Add(copy);
            generateVisualContent += Draw;
            SetSize(160);
        }

        public void SetValue(string text) { value.text = text; }
        public void SetSize(float size)
        {
            style.width = size; style.height = size;
            value.style.fontSize = size < 120 ? 22 : size < 150 ? 28 : 34;
            MarkDirtyRepaint();
        }

        private static Label Text(string text, int size)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size; label.style.color = Color.white;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginLeft = label.style.marginRight = 0;
            label.style.unityTextOutlineColor = new Color(0, 0, 0, .55f);
            label.style.unityTextOutlineWidth = .5f;
            return label;
        }

        private void Draw(MeshGenerationContext context)
        {
            float size = Mathf.Min(contentRect.width, contentRect.height);
            if (size <= 0) return;
            var center = new Vector2(contentRect.width * .5f, contentRect.height * .5f);
            float radius = size * .45f;
            var painter = context.painter2D;
            // UI y points downward: the missing 120 degrees sit at the bottom.
            painter.lineWidth = 1.4f;
            painter.strokeColor = new Color(1, 1, 1, .92f);
            painter.BeginPath();
            for (int i = 0; i <= 80; i++)
            {
                float angle = (150 + i * 3) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.Stroke();
        }
    }
}
