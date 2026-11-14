using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ESP.Presentation
{
    /// <summary>Runtime presentation for the map and the current mission.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionUI : MonoBehaviour
    {
        public MissionDirector director;
        private VisualElement mapControls, flightControls, weatherDetails;
        private Label site, status, altitude, speed, missionTime, weather, mapCredit, nasaStatus;
        private VisualElement regionalImage;
        private Label rocketMarker, mapMission;
        private MissionDirector subscribedDirector;
        private Button viewToggle, action, assist, vehicleChoice, modelCredit;
        private readonly Button[] rateButtons = new Button[3];
        private static readonly int[] Rates = { 1, 5, 20 };
        private Slider throttle;
        private float nextRefresh, nextMarkerRefresh;
        private VisualElement root;
        private static VisualElement activeRoot;
        private readonly Button[] markers = new Button[2];
        private bool weatherOpen;
        private int selectedLayer;
        private readonly Button[] layers = new Button[3];
        private static readonly Color Ink = new Color(.035f, .065f, .09f, .94f);
        private static readonly Color Mint = new Color(.58f, .94f, .78f);
        private static readonly Color Text = new Color(.91f, .96f, .95f);
        private static readonly Color Muted = new Color(.61f, .72f, .73f);

        private void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            activeRoot = root;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            var header = Row();
            Absolute(header, 24, 24, null, null);
            header.pickingMode = PickingMode.Ignore;
            var brand = Column();
            brand.pickingMode = PickingMode.Ignore;
            brand.Add(Label("ESP", 22, Text, FontStyle.Bold));
            brand.Add(Label("EARTH SPACE PROGRAM", 10, Muted, FontStyle.Normal));
            header.Add(brand);
            root.Add(header);

            viewToggle = Button("LAUNCH VIEW", () => {
                if (director == null) return;
                if (director.IsMapView) director.GoToLaunch(director.SelectedSiteIndex);
                else director.ShowMap();
                Refresh();
            }, false);
            Absolute(viewToggle, null, 24, 24, null);
            root.Add(viewToggle);

            for (int i = 0; i < markers.Length; i++)
            {
                int index = i;
                markers[i] = Button("●", () => { director?.GoToLaunch(index); Refresh(); }, false);
                markers[i].style.position = Position.Absolute;
                markers[i].style.display = DisplayStyle.None;
                root.Add(markers[i]);
            }

            rocketMarker = Label("▲  ROCKET", 12, Mint, FontStyle.Bold);
            rocketMarker.pickingMode = PickingMode.Ignore;
            rocketMarker.style.position = Position.Absolute;
            rocketMarker.style.display = DisplayStyle.None;
            root.Add(rocketMarker);
            mapMission = Label("", 12, Text, FontStyle.Bold);
            mapMission.pickingMode = PickingMode.Ignore;
            Absolute(mapMission, null, 74, 28, null);
            root.Add(mapMission);

            mapControls = Column();
            mapControls.pickingMode = PickingMode.Ignore;
            Absolute(mapControls, 24, null, null, 24);
            var mapBar = Row();
            mapBar.pickingMode = PickingMode.Ignore;
            string[] names = { "SURFACE", "SATELLITE", "CLOUDS" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                layers[i] = Button(names[i], () => { selectedLayer = index; director?.SelectLayer(index); Refresh(); }, false);
                mapBar.Add(layers[i]);
            }
            mapBar.Add(Button("WEATHER  +", () => { weatherOpen = !weatherOpen; Refresh(); }, false));
            mapControls.Add(mapBar);
            weatherDetails = Panel();
            weatherDetails.style.marginTop = 8;
            weatherDetails.style.maxWidth = 420;
            weather = Label("", 12, Text, FontStyle.Normal);
            weather.style.whiteSpace = WhiteSpace.Normal;
            weatherDetails.Add(weather);
            regionalImage = new VisualElement();
            regionalImage.style.width = 180; regionalImage.style.height = 120;
            regionalImage.style.display = DisplayStyle.None;
            weatherDetails.Add(regionalImage);
            nasaStatus = Label("", 10, Muted, FontStyle.Normal);
            nasaStatus.style.whiteSpace = WhiteSpace.Normal;
            weatherDetails.Add(nasaStatus);
            mapControls.Add(weatherDetails);
            mapCredit = Label("", 10, Muted, FontStyle.Normal);
            mapCredit.style.whiteSpace = WhiteSpace.Normal;
            mapCredit.style.maxWidth = 800;
            mapCredit.style.marginTop = 5;
            mapControls.Add(mapCredit);
            root.Add(mapControls);

            flightControls = Column();
            Absolute(flightControls, 24, null, 24, 24);
            flightControls.pickingMode = PickingMode.Ignore;
            var flightHeading = Column();
            flightHeading.pickingMode = PickingMode.Ignore;
            site = Label("", 24, Text, FontStyle.Bold);
            status = Label("", 11, Mint, FontStyle.Normal);
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.maxWidth = 1100;
            vehicleChoice = Button("ESP DEMO ROCKET", () => { if (director != null) director.SelectVehicle((director.VehicleIndex + 1) % 3); Refresh(); }, false);
            vehicleChoice.style.alignSelf = Align.FlexStart;
            flightHeading.Add(vehicleChoice);
            modelCredit = Button("MODEL CREDIT", () => Application.OpenURL(director.VehicleIndex == 1 ? "https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2" : "https://science.nasa.gov/3d-resources/space-launch-system-sls-block-1/"), false);
            modelCredit.style.fontSize = 10; modelCredit.style.alignSelf = Align.FlexStart;
            flightHeading.Add(modelCredit);
            flightHeading.Add(site); flightHeading.Add(status);
            flightControls.Add(flightHeading);
            var flightOptions = Row();
            flightOptions.pickingMode = PickingMode.Ignore;
            flightOptions.style.marginTop = 8;
            assist = Button("ASSIST ON", () => { if (director?.FlightDriver is RocketFlightDriver f) f.ToggleAssistance(); Refresh(); }, false);
            flightOptions.Add(assist);
            for (int i = 0; i < Rates.Length; i++)
            {
                int rate = Rates[i];
                rateButtons[i] = Button(rate + "×", () => { if (director?.FlightDriver is RocketFlightDriver f) f.SetTimeRate(rate); Refresh(); }, false);
                flightOptions.Add(rateButtons[i]);
            }
            flightControls.Add(flightOptions);
            var bottom = Row();
            bottom.style.marginTop = 14;
            bottom.style.alignItems = Align.FlexEnd;
            bottom.style.flexWrap = Wrap.Wrap;
            var telemetry = Panel();
            telemetry.style.flexDirection = FlexDirection.Row;
            altitude = Metric(telemetry, "ALTITUDE");
            speed = Metric(telemetry, "SPEED");
            missionTime = Metric(telemetry, "MISSION TIME");
            bottom.Add(telemetry);
            var controls = Panel();
            controls.style.marginLeft = 10;
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            action = Button("LAUNCH", () => {
                if (director == null) return;
                if (director.CanLaunch) director.Launch(); else director.Stage();
                Refresh();
            }, true);
            controls.Add(action);
            var throttleBox = Column();
            throttleBox.style.width = 150;
            throttleBox.style.marginLeft = 14;
            throttleBox.Add(Label("THROTTLE", 10, Muted, FontStyle.Normal));
            throttle = new Slider(0, 1) { value = 1 };
            throttle.RegisterValueChangedCallback(evt => director?.SetThrottle(evt.newValue));
            throttleBox.Add(throttle);
            controls.Add(throttleBox);
            controls.Add(Button("RESET", () => { director?.ResetMission(); Refresh(); }, false));
            bottom.Add(controls);
            flightControls.Add(bottom);
            root.Add(flightControls);
            Refresh();
        }

        private void Update()
        {
            if (subscribedDirector != director)
            {
                if (subscribedDirector != null) subscribedDirector.PointSelected -= OpenWeather;
                subscribedDirector = director;
                if (subscribedDirector != null) subscribedDirector.PointSelected += OpenWeather;
            }
            if (Time.unscaledTime >= nextMarkerRefresh)
            {
                nextMarkerRefresh = Time.unscaledTime + 1f / 30f;
                RefreshMarkers();
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .2f;
                Refresh();
            }
        }

        private void OnDestroy()
        {
            if (subscribedDirector != null) subscribedDirector.PointSelected -= OpenWeather;
            if (activeRoot == root) activeRoot = null;
        }

        /// <summary>Tests a screen point in Unity's bottom-left pixel coordinates.</summary>
        public static bool IsPointerOverUI(Vector2 screenBottomLeft)
        {
            if (activeRoot == null || activeRoot.panel == null) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(activeRoot.panel,
                new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y));
            return activeRoot.panel.Pick(panelPoint) != null;
        }

        private void OpenWeather() { weatherOpen = true; Refresh(); }

        private void RefreshMarkers()
        {
            if (root == null || root.panel == null) return;
            Vector2 rocketScreen = default;
            bool rocketVisible = director != null && director.TryGetRocketScreenPosition(out rocketScreen);
            rocketMarker.style.display = rocketVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (rocketVisible)
            {
                Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(rocketScreen.x, Screen.height - rocketScreen.y));
                rocketMarker.style.left = p.x; rocketMarker.style.top = p.y - 18;
            }
            for (int i = 0; i < markers.Length; i++)
            {
                Vector2 screen = default;
                bool show = director != null && director.IsMapView && director.TryGetSiteScreenPosition(i, out screen);
                markers[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                markers[i].style.left = point.x - 18;
                markers[i].style.top = point.y - 16;
                markers[i].text = "●  " + (i == 0 ? "STARBASE" : "LC-39A");
            }
        }

        private void Refresh()
        {
            if (mapControls == null) return;
            bool available = director != null;
            bool isMap = !available || director.IsMapView;
            mapControls.style.display = isMap ? DisplayStyle.Flex : DisplayStyle.None;
            flightControls.style.display = isMap ? DisplayStyle.None : DisplayStyle.Flex;
            var mapFlight = available ? director.FlightDriver as RocketFlightDriver : null;
            bool showMapMission = isMap && mapFlight != null && mapFlight.HasPosition;
            mapMission.style.display = showMapMission ? DisplayStyle.Flex : DisplayStyle.None;
            if (showMapMission)
            {
                double t = Math.Max(0, director.MissionTimeSeconds);
                mapMission.text = string.Format("MISSION {0:00}:{1:00}:{2:00}  •  {3}× SELECTED", (int)(t / 3600), (int)(t / 60) % 60, (int)t % 60, mapFlight.TimeRate);
            }
            weatherDetails.style.display = weatherOpen ? DisplayStyle.Flex : DisplayStyle.None;
            viewToggle.text = isMap ? "LAUNCH VIEW  ↗" : "←  MAP VIEW";
            viewToggle.SetEnabled(available);
            for (int i = 0; i < layers.Length; i++)
                layers[i].style.color = i == selectedLayer ? Mint : Text;
            if (!available) return;
            weather.text = director.WeatherSummary ?? "Weather unavailable";
            mapCredit.text = director.MapCredit;
            nasaStatus.text = director.NasaStatus;
            regionalImage.style.display = director.RegionalImage != null ? DisplayStyle.Flex : DisplayStyle.None;
            regionalImage.style.backgroundImage = director.RegionalImage != null ? new StyleBackground((Texture2D)director.RegionalImage) : new StyleBackground(StyleKeyword.None);
            vehicleChoice.text = director.VehicleLabel + "  ›";
            vehicleChoice.SetEnabled(director.CanLaunch);
            modelCredit.style.display = director.VehicleIndex == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            modelCredit.text = director.VehicleIndex == 1 ? "Model: Clarence365 · CC BY 4.0 · adapted ↗" : "Model: NASA · ESP adaptation ↗";
            site.text = director.SiteName ?? "Launch site";
            status.text = (director.FlightStatus ?? "READY").ToUpperInvariant();
            var flight = director.FlightDriver as RocketFlightDriver;
            if (flight != null) status.text += "  •  " + flight.FlightTelemetry;
            assist.SetEnabled(flight != null);
            assist.text = flight != null && flight.AssistanceEnabled ? "ASSIST ON" : "ASSIST OFF";
            assist.style.color = flight != null && flight.AssistanceEnabled ? Mint : Text;
            for (int i = 0; i < rateButtons.Length; i++)
            {
                rateButtons[i].SetEnabled(flight != null);
                rateButtons[i].style.color = flight != null && flight.TimeRate == Rates[i] ? Mint : Text;
            }
            altitude.text = director.AltitudeMeters.ToString("N0") + " m";
            speed.text = director.SpeedMetersPerSecond.ToString("N0") + " m/s";
            double seconds = Math.Max(0, director.MissionTimeSeconds);
            missionTime.text = string.Format("{0:00}:{1:00}:{2:00}", (int)(seconds / 3600), (int)(seconds / 60) % 60, (int)seconds % 60);
            action.text = director.FlightDriver == null || director.CanLaunch ? "LAUNCH  ↗" : "STAGE  ↗";
            action.SetEnabled(director.FlightDriver != null);
            throttle.SetEnabled(director.FlightDriver != null);
            throttle.SetValueWithoutNotify(director.Throttle);
        }

        private static Label Metric(VisualElement parent, string title)
        {
            var group = Column(); group.style.minWidth = 115; group.style.marginRight = 15;
            group.Add(Label(title, 10, Muted, FontStyle.Normal));
            var value = Label("—", 18, Text, FontStyle.Bold); group.Add(value); parent.Add(group); return value;
        }
        private static Label Label(string text, int size, Color color, FontStyle weight)
        {
            var label = new Label(text); label.style.fontSize = size; label.style.color = color; label.style.unityFontStyleAndWeight = weight; return label;
        }
        private static VisualElement Column() { var element = new VisualElement(); element.style.flexDirection = FlexDirection.Column; return element; }
        private static VisualElement Row() { var element = new VisualElement(); element.style.flexDirection = FlexDirection.Row; return element; }
        private static VisualElement Panel()
        {
            var element = new VisualElement(); element.style.backgroundColor = Ink;
            element.style.paddingTop = 12; element.style.paddingBottom = 12; element.style.paddingLeft = 16; element.style.paddingRight = 16;
            element.style.borderTopLeftRadius = 10; element.style.borderTopRightRadius = 10; element.style.borderBottomLeftRadius = 10; element.style.borderBottomRightRadius = 10;
            return element;
        }
        private static Button Button(string text, Action click, bool primary)
        {
            var button = new Button(click) { text = text };
            button.style.backgroundColor = primary ? Mint : Ink;
            button.style.color = primary ? new Color(.025f, .09f, .075f) : Text;
            button.style.fontSize = 11; button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.paddingLeft = 16; button.style.paddingRight = 16; button.style.paddingTop = 10; button.style.paddingBottom = 10;
            button.style.borderTopWidth = 0; button.style.borderBottomWidth = 0; button.style.borderLeftWidth = 0; button.style.borderRightWidth = 0;
            button.style.borderTopLeftRadius = 8; button.style.borderTopRightRadius = 8; button.style.borderBottomLeftRadius = 8; button.style.borderBottomRightRadius = 8;
            return button;
        }
        private static void Absolute(VisualElement element, float? left, float? top, float? right, float? bottom)
        {
            element.style.position = Position.Absolute;
            if (left.HasValue) element.style.left = left.Value;
            if (top.HasValue) element.style.top = top.Value;
            if (right.HasValue) element.style.right = right.Value;
            if (bottom.HasValue) element.style.bottom = bottom.Value;
        }
    }
}
