using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>Compact cockpit instruments and map controls. Authoritative telemetry comes from the flight driver.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionUI : MonoBehaviour
    {
        public MissionDirector director;
        private VisualElement mapControls, flightControls, weatherDetails, flightDetails;
        private Label site, status, altitude, speed, missionTime, weather, mapCredit, nasaStatus, flightDetailText;
        private Label throttleValue, vehicleNote, mapVehicleNote;
        private Label stageFuel, boosterFuel;
        private VisualElement regionalImage, navballHousing, vehicleGroup, mapVehicleGroup;
        private Label rocketMarker, mapMission;
        private readonly Label[] orbitMarkers = new Label[3];
        private MissionDirector subscribedDirector;
        private Button viewToggle, action, assist, vehicleChoice, mapVehicleChoice, modelCredit, mapModelCredit, weatherToggle, detailsToggle, mapDetailsToggle;
        private readonly Button[] rateButtons = new Button[3];
        private static readonly int[] Rates = { 1, 5, 20 };
        private Slider throttle;
        private NavballElement navball;
        private float nextRefresh, nextMarkerRefresh;
        private VisualElement root;
        private static VisualElement activeRoot;
        private readonly Button[] markers = new Button[2];
        private bool weatherOpen, detailsOpen;
        private int selectedLayer;
        private readonly Button[] layers = new Button[3];
        private static readonly Color Ink = new Color(.035f, .047f, .065f, .96f);
        private static readonly Color Raised = new Color(.065f, .085f, .11f, .98f);
        private static readonly Color Cyan = new Color(.39f, .86f, .89f);
        private static readonly Color Amber = new Color(.99f, .73f, .32f);
        private static readonly Color Text = new Color(.88f, .93f, .96f);
        private static readonly Color Muted = new Color(.51f, .62f, .69f);
        private static readonly Color Edge = new Color(.18f, .25f, .30f, .9f);

        private void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            activeRoot = root;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.RegisterCallback<GeometryChangedEvent>(_ => Resize());

            var header = Column();
            header.name = "mission-header";
            Absolute(header, 18, 16, null, null);
            header.pickingMode = PickingMode.Ignore;
            site = Label("", 11, Text, FontStyle.Normal); site.name = "launch-site";
            header.Add(site);
            status = Label("", 10, Cyan, FontStyle.Normal); status.name = "flight-status";
            status.style.maxWidth = 380;
            Ellipsis(status);
            status.style.display = DisplayStyle.None;
            header.Add(status);
            root.Add(header);

            viewToggle = Button("LAUNCH VIEW  [M]", ToggleView, false, "view-toggle");
            Absolute(viewToggle, null, 16, 18, null);
            root.Add(viewToggle);
            mapMission = Label("", 10, Text, FontStyle.Bold);
            mapMission.name = "map-mission-clock";
            mapMission.pickingMode = PickingMode.Ignore;
            Absolute(mapMission, null, 54, 21, null);
            root.Add(mapMission);

            for (int i = 0; i < markers.Length; i++)
            {
                int index = i;
                markers[i] = Button("●", () => { director?.GoToLaunch(index); Refresh(); }, false, "site-marker-" + i);
                markers[i].style.position = Position.Absolute;
                markers[i].style.display = DisplayStyle.None;
                root.Add(markers[i]);
            }
            rocketMarker = Label("▲", 20, new Color(1f, .19f, .13f), FontStyle.Bold);
            rocketMarker.style.unityTextOutlineColor = new Color(.05f, .015f, .01f);
            rocketMarker.style.unityTextOutlineWidth = 1.2f;
            rocketMarker.style.width = 26; rocketMarker.style.height = 26;
            rocketMarker.style.unityTextAlign = TextAnchor.MiddleCenter;
            rocketMarker.name = "rocket-map-marker";
            rocketMarker.pickingMode = PickingMode.Position;
            rocketMarker.style.position = Position.Absolute;
            rocketMarker.style.display = DisplayStyle.None;
            root.Add(rocketMarker);

            for (int i = 0; i < orbitMarkers.Length; i++)
            {
                orbitMarkers[i] = Label("", 10, i == 2 ? Amber : Cyan, FontStyle.Bold);
                orbitMarkers[i].name = "orbit-marker-" + i;
                orbitMarkers[i].style.position = Position.Absolute;
                orbitMarkers[i].style.display = DisplayStyle.None;
                orbitMarkers[i].style.backgroundColor = new Color(.02f, .04f, .06f, .78f);
                orbitMarkers[i].style.paddingLeft = orbitMarkers[i].style.paddingRight = 4;
                orbitMarkers[i].pickingMode = PickingMode.Position;
                root.Add(orbitMarkers[i]);
            }

            BuildMapControls();
            BuildFlightControls();
            Resize();
            Refresh();
        }

        private void BuildMapControls()
        {
            mapControls = Column(); mapControls.name = "map-controls";
            mapControls.pickingMode = PickingMode.Ignore;
            Absolute(mapControls, 12, null, 12, 10);

            weatherDetails = Panel(); weatherDetails.name = "point-weather-panel";
            weatherDetails.style.width = 370;
            weatherDetails.style.maxHeight = 330;
            weatherDetails.style.marginBottom = 8;
            Absolute(weatherDetails, null, null, 12, 80);
            var weatherHeading = Row(); weatherHeading.style.justifyContent = Justify.SpaceBetween;
            weatherHeading.style.alignItems = Align.Center;
            weatherHeading.Add(Label("POINT WEATHER", 10, Cyan, FontStyle.Bold));
            weatherHeading.Add(Button("CLOSE", () => { weatherOpen = false; Refresh(); }, false, "close-weather"));
            weatherDetails.Add(weatherHeading);
            weather = Label("", 11, Text, FontStyle.Normal); weather.name = "point-weather-summary";
            weather.style.whiteSpace = WhiteSpace.Normal; weather.style.marginTop = 6;
            weatherDetails.Add(weather);
            var refreshWeather = Button("REFRESH WEATHER", () => { director?.RefreshPointWeather(true); Refresh(); }, false, "refresh-point-weather");
            refreshWeather.style.alignSelf = Align.FlexStart; refreshWeather.style.marginTop = 6;
            weatherDetails.Add(refreshWeather);
            regionalImage = new VisualElement(); regionalImage.name = "regional-satellite-preview";
            regionalImage.style.width = 150; regionalImage.style.height = 100;
            regionalImage.style.marginTop = 6; regionalImage.style.display = DisplayStyle.None;
            weatherDetails.Add(regionalImage);
            nasaStatus = Label("", 9, Muted, FontStyle.Normal);
            nasaStatus.style.whiteSpace = WhiteSpace.Normal;
            weatherDetails.Add(nasaStatus);
            root.Add(weatherDetails);

            var mapBar = Row(); mapBar.name = "map-bottom-strip";
            mapBar.pickingMode = PickingMode.Ignore;
            mapBar.style.flexDirection = FlexDirection.Row;
            mapBar.style.alignItems = Align.Center;
            mapBar.style.justifyContent = Justify.SpaceBetween;
            mapVehicleGroup = VehicleSelector(true);
            mapBar.Add(mapVehicleGroup);
            var layerGroup = Column();
            var layerRow = Row();
            string[] names = { "SURFACE", "SATELLITE", "CLOUDS" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                layers[i] = Button(names[i], () => { selectedLayer = index; director?.SelectLayer(index); Refresh(); }, false, "map-layer-" + names[i].ToLowerInvariant());
                layerRow.Add(layers[i]);
            }
            layerGroup.Add(layerRow);
            mapBar.Add(layerGroup);
            weatherToggle = Button("WEATHER", () => { weatherOpen = !weatherOpen; if (weatherOpen) director?.RefreshPointWeather(); Refresh(); }, false, "point-weather-toggle");
            var mapUtilities = Row(); mapUtilities.pickingMode = PickingMode.Ignore;
            mapUtilities.Add(weatherToggle);
            mapDetailsToggle = Button("i", () => { detailsOpen = !detailsOpen; Refresh(); }, false, "map-information-toggle");
            mapDetailsToggle.tooltip = "Imagery dates, sources, model credits and simulation limitations";
            mapDetailsToggle.style.width = 29;
            mapUtilities.Add(mapDetailsToggle);
            mapBar.Add(mapUtilities);
            mapControls.Add(mapBar);
            mapCredit = Label("", 9, Muted, FontStyle.Normal); mapCredit.name = "imagery-provenance";
            mapCredit.style.whiteSpace = WhiteSpace.Normal; mapCredit.style.marginTop = 5;
            mapCredit.pickingMode = PickingMode.Ignore;
            mapControls.Add(mapCredit);
            root.Add(mapControls);
        }

        private void BuildFlightControls()
        {
            flightControls = Column(); flightControls.name = "flight-controls";
            Absolute(flightControls, 12, null, 12, 10);
            flightControls.pickingMode = PickingMode.Ignore;
            flightDetails = Panel(); flightDetails.name = "flight-information-panel";
            Absolute(flightDetails, 12, null, null, 174);
            flightDetails.style.width = 400;
            var closeDetails = Button("CLOSE", () => { detailsOpen = false; Refresh(); }, false, "close-information");
            closeDetails.style.alignSelf = Align.FlexEnd;
            flightDetails.Add(closeDetails);
            flightDetailText = Label("", 11, Text, FontStyle.Normal);
            flightDetailText.style.whiteSpace = WhiteSpace.Normal;
            flightDetails.Add(flightDetailText);
            root.Add(flightDetails);

            var dock = Row(); dock.name = "cockpit-bottom-strip";
            dock.pickingMode = PickingMode.Ignore;
            dock.style.height = 84;
            dock.style.flexDirection = FlexDirection.Row; dock.style.alignItems = Align.Center;
            var left = Row(); left.name = "cockpit-left-instruments"; left.pickingMode = PickingMode.Ignore;
            left.style.flexGrow = 1; left.style.flexBasis = 0; left.style.minWidth = 0;
            left.style.alignItems = Align.Center;
            vehicleGroup = VehicleSelector(false); left.Add(vehicleGroup);
            flightDetails.Add(modelCredit);
            var telemetry = Panel(); telemetry.name = "primary-telemetry";
            telemetry.style.flexDirection = FlexDirection.Row;
            telemetry.style.marginLeft = 8;
            telemetry.style.flexGrow = 1; telemetry.style.justifyContent = Justify.SpaceAround;
            altitude = Metric(telemetry, "ALTITUDE", "altitude-readout");
            speed = Metric(telemetry, "SURFACE SPEED", "speed-readout");
            var fuel = Column();
            fuel.name = "fuel-readout";
            fuel.Add(Label("PROPELLANT", 9, Muted, FontStyle.Bold));
            stageFuel = Label("", 11, Text, FontStyle.Bold);
            stageFuel.name = "stage-fuel-readout";
            fuel.Add(stageFuel);
            boosterFuel = Label("", 10, Muted, FontStyle.Normal);
            boosterFuel.name = "booster-fuel-readout";
            boosterFuel.style.display = DisplayStyle.None;
            fuel.Add(boosterFuel);
            telemetry.Add(fuel);
            left.Add(telemetry); dock.Add(left);

            navballHousing = Column(); navballHousing.name = "navball-housing";
            navballHousing.style.width = 144; navballHousing.style.flexShrink = 0;
            navballHousing.style.height = 145; navballHousing.style.alignSelf = Align.FlexEnd;
            navballHousing.style.alignItems = Align.Center;
            navball = new NavballElement(); navball.name = "flight-navball";
            navball.style.width = 120; navball.style.height = 120; navball.style.flexShrink = 0;
            navballHousing.Add(navball);
            missionTime = Label("T+ 00:00:00  /  1×", 10, Cyan, FontStyle.Bold);
            missionTime.name = "mission-time-and-rate";
            missionTime.style.marginTop = 4;
            navballHousing.Add(missionTime); dock.Add(navballHousing);

            var right = Row(); right.name = "cockpit-flight-inputs"; right.pickingMode = PickingMode.Ignore;
            right.style.flexGrow = 1; right.style.flexBasis = 0; right.style.minWidth = 0;
            right.style.justifyContent = Justify.SpaceBetween; right.style.alignItems = Align.Center;
            var guidance = Column(); guidance.style.width = 102; guidance.style.flexShrink = 0;
            assist = Button("ASSIST ON", () => { if (director?.FlightDriver is RocketFlightDriver f) f.ToggleAssistance(); Refresh(); }, false, "assistance-toggle");
            assist.tooltip = "Assisted ascent follows the same throttle, steering and staging limits. Manual pitch: W/S or ↑/↓. Heading: A/D. Steering follows the model’s finite rate.";
            guidance.Add(assist);
            var rates = Row(); rates.name = "time-rate-controls";
            for (int i = 0; i < Rates.Length; i++)
            {
                int rate = Rates[i];
                rateButtons[i] = Button(rate + "×", () => { if (director?.FlightDriver is RocketFlightDriver f) f.SetTimeRate(rate); Refresh(); }, false, "time-rate-" + rate);
                rateButtons[i].style.flexGrow = 1; rateButtons[i].style.paddingLeft = 4; rateButtons[i].style.paddingRight = 4;
                rateButtons[i].style.height = 23;
                rates.Add(rateButtons[i]);
            }
            guidance.Add(rates); right.Add(guidance);

            var throttleBox = Column(); throttleBox.name = "throttle-instrument";
            throttleBox.style.width = 80; throttleBox.style.marginLeft = 7; throttleBox.style.marginRight = 7;
            throttleBox.Add(Label("THROTTLE", 9, Muted, FontStyle.Normal));
            throttleValue = Label("100%", 18, Text, FontStyle.Bold); throttleValue.name = "throttle-readout";
            throttleBox.Add(throttleValue);
            throttle = new Slider(0, 1) { value = 1, name = "throttle-slider" };
            throttle.tooltip = "Adjust throttle (switches to manual control)";
            throttle.style.marginLeft = 0; throttle.style.marginRight = 0;
            throttle.RegisterValueChangedCallback(evt => { director?.SetThrottle(evt.newValue); Refresh(); });
            throttleBox.Add(throttle); right.Add(throttleBox);

            var staging = Column(); staging.style.width = 83; staging.style.flexShrink = 0;
            action = Button("LAUNCH", () => {
                if (director == null) return;
                if (director.CanLaunch) director.Launch(); else director.Stage();
                Refresh();
            }, true, "launch-stage-action");
            action.style.height = 38;
            staging.Add(action);
            var utility = Row();
            var reset = Button("RESET", () => { director?.ResetMission(); Refresh(); }, false, "reset-mission");
            reset.style.flexGrow = 1; reset.style.paddingLeft = 4; reset.style.paddingRight = 4; reset.style.height = 23;
            utility.Add(reset);
            detailsToggle = Button("i", () => { detailsOpen = !detailsOpen; Refresh(); }, false, "flight-information-toggle");
            detailsToggle.tooltip = "Flight details, atmosphere source and keyboard controls";
            detailsToggle.style.width = 23; detailsToggle.style.height = 23; detailsToggle.style.paddingLeft = 0; detailsToggle.style.paddingRight = 0;
            utility.Add(detailsToggle); staging.Add(utility); right.Add(staging);
            dock.Add(right); flightControls.Add(dock); root.Add(flightControls);
        }

        private VisualElement VehicleSelector(bool map)
        {
            var group = Column(); group.name = map ? "map-vehicle-selector" : "flight-vehicle-selector";
            group.style.width = 140; group.style.flexShrink = 0;
            var choice = Button("STARSHIP  ›", () => { if (director != null) director.SelectVehicle((director.VehicleIndex + 1) % 3); Refresh(); }, false, map ? "map-vehicle-choice" : "flight-vehicle-choice");
            choice.style.unityTextAlign = TextAnchor.MiddleLeft;
            choice.tooltip = "Choose vehicle before launch";
            group.Add(choice);
            var note = Label("ESTIMATED SPECS", 8, Muted, FontStyle.Normal);
            var credit = Button("MODEL CREDIT ↗", OpenModelCredit, false, map ? "map-model-credit" : "flight-model-credit");
            credit.style.fontSize = 8; credit.style.height = 16; credit.style.marginTop = 0; credit.style.marginBottom = 0;
            credit.style.paddingLeft = 0; credit.style.paddingRight = 0;
            credit.style.backgroundColor = Color.clear; Border(credit, Color.clear);
            credit.style.unityTextAlign = TextAnchor.MiddleLeft; credit.style.color = Muted;
            if (map) { mapVehicleChoice = choice; mapModelCredit = credit; mapVehicleNote = note; }
            else { vehicleChoice = choice; modelCredit = credit; vehicleNote = note; }
            return group;
        }

        private void OpenModelCredit()
        {
            if (director == null) return;
            Application.OpenURL(director.VehicleIndex == 1
                ? "https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2"
                : "https://science.nasa.gov/3d-resources/space-launch-system-sls-block-1/");
        }

        private void Resize()
        {
            if (vehicleGroup == null) return;
            bool compact = root.resolvedStyle.width > 0 && root.resolvedStyle.width < 1050;
            vehicleGroup.style.width = compact ? 112 : 140;
            navballHousing.style.width = compact ? 130 : 144;
            altitude.style.fontSize = compact ? 18 : 21;
            speed.style.fontSize = compact ? 18 : 21;
        }

        private void Update()
        {
            var focused = root?.panel?.focusController?.focusedElement as VisualElement;
            bool editingText = focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
            if (!MissionSession.BlocksInput && !editingText && Application.isFocused && GUIUtility.keyboardControl == 0 && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                ToggleView();
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
                if (director != null)
                    navball?.SetFlight(director.FlightDriver as RocketFlightDriver, LaunchSites.All[director.SelectedSiteIndex]);
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

        private void ToggleView()
        {
            if (director == null || MissionSession.BlocksInput) return;
            if (director.IsMapView) director.GoToLaunch(director.SelectedSiteIndex);
            else director.ShowMap();
            Refresh();
        }
        private void OpenWeather() { weatherOpen = true; Refresh(); }

        private void RefreshMarkers()
        {
            if (root == null || root.panel == null) return;
            for (int i = 0; i < orbitMarkers.Length; i++)
            {
                Vector2 orbitScreen = default;
                double apsisHeight = 0;
                bool showOrbit = director != null && director.IsMapView && director.OrbitVisual != null &&
                    director.OrbitVisual.TryGetMarkerScreenPosition((FlightCore.OrbitMarkerKind)i, out orbitScreen, out apsisHeight);
                var marker = orbitMarkers[i];
                marker.style.display = showOrbit ? DisplayStyle.Flex : DisplayStyle.None;
                if (!showOrbit) continue;
                Vector2 orbitPoint = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(orbitScreen.x, Screen.height - orbitScreen.y));
                marker.style.left = orbitPoint.x + 5; marker.style.top = orbitPoint.y - 16;
                marker.text = i == 0 ? "Ap" : i == 1 ? "Pe" : "Impact";
                marker.tooltip = (i == 2 ? "Predicted WGS84 surface intersection" : (i == 0 ? "Apoapsis " : "Periapsis ") + Distance(apsisHeight) + " above WGS84 at this point") +
                    ". Coast prediction — engines cut now; ignores drag.";
            }
            Vector2 rocketScreen = default;
            bool rocketVisible = director != null && director.TryGetRocketScreenPosition(out rocketScreen);
            rocketMarker.style.display = rocketVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (rocketVisible)
            {
                Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(rocketScreen.x, Screen.height - rocketScreen.y));
                rocketMarker.style.left = p.x - 13; rocketMarker.style.top = p.y - 13;
                rocketMarker.tooltip = director.VehicleIndex == 1 ? "Starship — current position" : director.VehicleIndex == 2 ? "SLS — current position" : "Vehicle — current position";
            }
            for (int i = 0; i < markers.Length; i++)
            {
                Vector2 screen = default;
                bool show = director != null && director.IsMapView && director.TryGetSiteScreenPosition(i, out screen);
                markers[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                markers[i].style.left = point.x - 18; markers[i].style.top = point.y - 16;
                markers[i].text = "●  " + (i == 0 ? "STARBASE" : "LC-39B");
            }
        }

        private void Refresh()
        {
            if (mapControls == null) return;
            bool available = director != null;
            bool isMap = !available || director.IsMapView;
            var flight = available ? director.FlightDriver as RocketFlightDriver : null;
            bool mapFlying = isMap && flight != null && flight.HasPosition;
            mapControls.style.display = isMap ? DisplayStyle.Flex : DisplayStyle.None;
            flightControls.style.display = !isMap || mapFlying ? DisplayStyle.Flex : DisplayStyle.None;
            // Keep map selectors in a separate corner once the shared flight dock is visible.
            // Its navball reaches 155 px above the bottom, so popovers clear it by 19 px.
            mapControls.style.top = mapFlying ? new StyleLength(48) : new StyleLength(StyleKeyword.Auto);
            mapControls.style.bottom = mapFlying ? new StyleLength(StyleKeyword.Auto) : new StyleLength(10);
            mapControls.style.right = mapFlying ? new StyleLength(StyleKeyword.Auto) : new StyleLength(12);
            mapControls.style.width = mapFlying ? new StyleLength(350) : new StyleLength(StyleKeyword.Auto);
            mapVehicleGroup.style.display = mapFlying ? DisplayStyle.None : DisplayStyle.Flex;
            mapDetailsToggle.style.display = mapFlying ? DisplayStyle.None : DisplayStyle.Flex;
            flightDetails.style.display = detailsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            weatherDetails.style.display = isMap && weatherOpen ? DisplayStyle.Flex : DisplayStyle.None;
            weatherDetails.style.bottom = mapFlying ? 174 : 80;
            viewToggle.text = isMap ? "FLIGHT  [M]" : "MAP  [M]";
            viewToggle.SetEnabled(available);
            if (director != null && director.mapLayers != null) selectedLayer = director.mapLayers.SelectedIndex + 1;
            for (int i = 0; i < layers.Length; i++) Selected(layers[i], i == selectedLayer);
            Selected(weatherToggle, weatherOpen);
            Selected(detailsToggle, detailsOpen);
            Selected(mapDetailsToggle, detailsOpen);
            if (!available) return;
            // The shared dock carries mission time/rate in both views; avoid duplicate clocks.
            mapMission.style.display = DisplayStyle.None;
            double seconds = Math.Max(0, director.MissionTimeSeconds);
            string clock = string.Format("T+ {0:00}:{1:00}:{2:00}  /  {3}×", (int)(seconds / 3600), (int)(seconds / 60) % 60, (int)seconds % 60, flight != null ? flight.TimeRate : 1);
            missionTime.text = clock; mapMission.text = clock;
            weather.text = director.WeatherSummary ?? "Weather unavailable";
            string imageryDate = director.mapLayers != null ? director.mapLayers.DisplayedDate : "";
            mapCredit.text = string.IsNullOrEmpty(imageryDate) ? "NASA · Blue Marble" : "NASA · through " + imageryDate;
            nasaStatus.text = director.NasaStatus;
            regionalImage.style.display = director.RegionalImage != null ? DisplayStyle.Flex : DisplayStyle.None;
            regionalImage.style.backgroundImage = director.RegionalImage != null ? new StyleBackground((Texture2D)director.RegionalImage) : new StyleBackground(StyleKeyword.None);
            string vehicleName = director.VehicleIndex == 1 ? "STARSHIP" : director.VehicleIndex == 2 ? "SLS" : "ESP DEMO";
            vehicleChoice.text = mapVehicleChoice.text = vehicleName + "  ›";
            vehicleChoice.SetEnabled(director.CanLaunch); mapVehicleChoice.SetEnabled(director.CanLaunch);
            vehicleNote.text = mapVehicleNote.text = director.VehicleIndex == 0 ? "DEMO PRESET" : "ESTIMATED SPECS";
            modelCredit.style.display = mapModelCredit.style.display = director.VehicleIndex == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            modelCredit.text = mapModelCredit.text = director.VehicleIndex == 1 ? "Clarence365 · CC BY 4.0 ↗" : "NASA · adapted ↗";
            modelCredit.tooltip = mapModelCredit.tooltip = director.VehicleIndex == 1 ? "Starship model by Clarence365, CC BY 4.0; adapted for ESP. Open source model." : "NASA SLS model adapted for ESP. Open source model.";
            site.text = (director.SiteName ?? "Launch site").Replace(" (approximate)", "").Replace(" (Approximate)", "").ToUpperInvariant();
            status.text = isMap ? "EARTH / SELECT A LAUNCH SITE" : (director.FlightStatus ?? "READY").ToUpperInvariant();
            status.style.color = flight != null && flight.OrbitVerified ? Cyan : Text;
            string vehicleInfo = director.VehicleIndex == 0 ? "ESP demo preset." : "Vehicle performance uses estimated specifications.";
            string mapInfo = (director.GetComponent<MissionSunlight>()?.Status ?? "") + "\n" + director.MapCredit + "\n\n" + director.SiteName + "\nRed triangle: current simulated vehicle position. Flight attitude is shown on the navball.\n" + vehicleInfo +
                "\nCyan: coast prediction — engines cut now; ignores drag. Muted blue: actual flown path behind the vehicle. Ap/Pe mark radial apsides with local WGS84 heights. Impact clips the path at the surface.";
            string steeringInfo = "Pitch: W/S or ↑/↓  ·  Heading: A/D  ·  Stage: Space  ·  Map: M\n" +
                "Throttle or steering input switches assistance off. Attitude uses simplified elevation/heading with a finite steering rate; roll is not modeled.";
            var localSurface = director.GetComponent<LocalSurfaceTiles>();
            string surfaceInfo = localSurface != null ? "\n\n" + LocalSurfaceTiles.Attribution + "\n" + localSurface.Status : "";
            if (director.SelectedSiteIndex == 1 && director.siteStructurePrefabs != null && director.siteStructurePrefabs.Length > 1 && director.siteStructurePrefabs[1] != null)
                surfaceInfo += "\nHistorical LC-39 gantry: NASA / Michael D. Carbajal. Approximate scale and placement; not the current facility layout.";
            surfaceInfo += "\nExhaust textures: Waterfall / Chris Adderley (Nertea) and contributors, CC BY-NC-SA 4.0; converted to PNG. SRB emission scheduling: SmokeScreen / Sarbian, BSD-2-Clause. Full notices accompany the game.";
            string flightInfo = (director.FlightStatus ?? "READY") + "\n\n" +
                (flight != null ? flight.FlightTelemetry : "Flight simulation unavailable") +
                "\n" + (director.GetComponent<MissionSunlight>()?.Status ?? "") + "\n\n" + steeringInfo;
            flightDetailText.text = isMap ? mapInfo + (mapFlying ? "\n\n" + flightInfo : "") : flightInfo + "\n\n" + vehicleInfo + surfaceInfo;
            assist.SetEnabled(flight != null);
            assist.text = flight != null && flight.AssistanceEnabled ? "ASSIST ON" : "MANUAL";
            Selected(assist, flight != null && flight.AssistanceEnabled);
            for (int i = 0; i < rateButtons.Length; i++)
            {
                rateButtons[i].SetEnabled(flight != null);
                Selected(rateButtons[i], flight != null && flight.TimeRate == Rates[i]);
            }
            altitude.text = Distance(director.AltitudeMeters);
            speed.text = director.SpeedMetersPerSecond.ToString("N0") + " m/s";
            if (flight != null)
            {
                var fuel = flight.FuelTelemetry;
                stageFuel.text = $"Stage {fuel.StageNumber} · {fuel.StageKg:N0} kg";
                boosterFuel.style.display = fuel.BoosterKg.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (fuel.BoosterKg.HasValue)
                    boosterFuel.text = $"SRBs · {fuel.BoosterKg.Value:N0} kg";
            }
            else
            {
                stageFuel.text = "Unavailable";
                boosterFuel.style.display = DisplayStyle.None;
            }
            action.text = director.FlightDriver == null || director.CanLaunch ? "LAUNCH" : "STAGE";
            action.SetEnabled(director.FlightDriver != null);
            throttle.SetEnabled(director.FlightDriver != null);
            throttle.SetValueWithoutNotify(director.Throttle);
            throttleValue.text = (director.Throttle * 100).ToString("F0") + "%";
            throttleValue.style.color = director.Throttle > 0 ? Text : Muted;
        }

        private static string Distance(double meters) => Math.Abs(meters) >= 10000 ? (meters / 1000).ToString("N1") + " km" : meters.ToString("N0") + " m";
        private static Label Metric(VisualElement parent, string title, string name)
        {
            var group = Column(); group.style.minWidth = 78;
            group.Add(Label(title, 9, Muted, FontStyle.Normal));
            var value = Label("—", 21, Text, FontStyle.Bold); value.name = name;
            group.Add(value); parent.Add(group); return value;
        }
        private static void Selected(Button button, bool selected)
        {
            button.style.color = selected ? Cyan : Text;
            button.style.backgroundColor = selected ? new Color(.07f, .17f, .20f, .98f) : Raised;
            Border(button, selected ? new Color(.22f, .49f, .53f) : Edge);
        }
        private static Label Label(string text, int size, Color color, FontStyle weight)
        {
            var label = new Label(text);
            label.style.fontSize = size; label.style.color = color; label.style.unityFontStyleAndWeight = weight;
            label.style.marginLeft = 0; label.style.marginRight = 0;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
        private static void Ellipsis(VisualElement element)
        {
            element.style.overflow = Overflow.Hidden; element.style.whiteSpace = WhiteSpace.NoWrap;
            element.style.textOverflow = TextOverflow.Ellipsis;
        }
        private static VisualElement Column() { var element = new VisualElement(); element.style.flexDirection = FlexDirection.Column; return element; }
        private static VisualElement Row() { var element = new VisualElement(); element.style.flexDirection = FlexDirection.Row; return element; }
        private static VisualElement Panel()
        {
            var element = new VisualElement(); element.style.backgroundColor = Ink;
            element.style.paddingTop = 8; element.style.paddingBottom = 8; element.style.paddingLeft = 12; element.style.paddingRight = 12;
            Border(element, Edge); Radius(element, 5);
            return element;
        }
        private static Button Button(string text, Action click, bool primary, string name)
        {
            var button = new Button(click) { text = text, name = name };
            button.style.backgroundColor = primary ? Amber : Raised;
            button.style.color = primary ? new Color(.09f, .075f, .04f) : Text;
            button.style.fontSize = 10; button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.height = 29; button.style.paddingLeft = 10; button.style.paddingRight = 10;
            button.style.paddingTop = 0; button.style.paddingBottom = 0;
            button.style.marginLeft = 0; button.style.marginRight = 3; button.style.marginTop = 2; button.style.marginBottom = 2;
            Border(button, primary ? Amber : Edge); Radius(button, 3);
            return button;
        }
        private static void Border(VisualElement element, Color color)
        {
            element.style.borderTopWidth = element.style.borderBottomWidth = element.style.borderLeftWidth = element.style.borderRightWidth = 1;
            element.style.borderTopColor = element.style.borderBottomColor = element.style.borderLeftColor = element.style.borderRightColor = color;
        }
        private static void Radius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
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
