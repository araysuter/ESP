using System;
using System.Collections.Generic;
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
        private Label site, status, headerMissionTime, missionTime, weather, mapCredit, nasaStatus, flightDetailText;
        private BroadcastGauge speedGauge, altitudeGauge;
        private VisualElement telemetry, inputStrip, previewHousing;
        private ScrollView informationBody;
        private Image previewImage;
        private Label previewCaption, previewMarker;
        private Label throttleValue, vehicleNote, mapVehicleNote;
        private Label stageFuel, boosterFuel;
        private VisualElement stagingPanel, compactFuel;
        private readonly List<StagingFuelRow> stageRows = new List<StagingFuelRow>();
        private StagingFuelRow boosterRow;
        private VisualElement regionalImage, navballHousing, vehicleGroup, mapVehicleGroup;
        private Label rocketMarker, mapMission;
        private Label systemEarth, systemSun, systemVehicle;
        private readonly Label[] orbitMarkers = new Label[3];
        private MissionDirector subscribedDirector;
        private Button viewToggle, action, reentry, assist, vehicleChoice, mapVehicleChoice, modelCredit, mapModelCredit, weatherToggle, detailsToggle, mapDetailsToggle;
        private readonly Button[] rateButtons = new Button[2];
        private static readonly int[] Rates = { 1, 2, 3, 4, 5, 10, 20, 50, 100, 300, 1000 };
        private Label warpValue;
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
            headerMissionTime = Label("T+ 00:00:00", 11, Cyan, FontStyle.Bold);
            headerMissionTime.name = "header-mission-time";
            headerMissionTime.style.marginTop = 2;
            header.Add(headerMissionTime);
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

            systemEarth = Label("●  EARTH", 10, Cyan, FontStyle.Bold);
            systemSun = Label("SUN", 10, Amber, FontStyle.Bold);
            systemVehicle = Label("▲  STARSHIP", 10, Text, FontStyle.Bold);
            foreach (var label in new[] { systemEarth, systemSun, systemVehicle })
            {
                label.style.position = Position.Absolute;
                label.style.display = DisplayStyle.None;
                label.pickingMode = PickingMode.Ignore;
                root.Add(label);
            }

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
            var refreshImagery = Button("↻", () => { director?.mapLayers?.RefreshLatest(); Refresh(); }, false, "refresh-nasa-imagery");
            refreshImagery.style.width = 29;
            refreshImagery.style.minWidth = 29;
            refreshImagery.style.paddingLeft = refreshImagery.style.paddingRight = 0;
            refreshImagery.tooltip = "Fetch NASA availability and the latest advertised observations for the selected layer";
            layerRow.Add(refreshImagery);
            layerGroup.Add(layerRow);
            var scaleRow = Row();
            var shipView = Button("SHIP", () => director?.mapCamera?.GetComponent<GlobeCameraControls>()?.ShowShip(), false, "map-focus-ship");
            shipView.tooltip = "Focus the map on the vehicle";
            var systemView = Button("SUN / EARTH", () => director?.mapCamera?.GetComponent<GlobeCameraControls>()?.ShowSystem(), false, "map-sun-earth-view");
            systemView.tooltip = "Show the approximate Sun–Earth system and Earth's orbit";
            scaleRow.Add(shipView); scaleRow.Add(systemView); layerGroup.Add(scaleRow);
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
            Absolute(flightDetails, 18, null, null, 292);
            flightDetails.style.width = 400;
            var closeDetails = Button("CLOSE", () => { detailsOpen = false; Refresh(); }, false, "close-information");
            closeDetails.style.alignSelf = Align.FlexEnd;
            closeDetails.style.flexShrink = 0;
            flightDetails.Add(closeDetails);
            flightDetailText = Label("", 11, Text, FontStyle.Normal);
            flightDetailText.style.whiteSpace = WhiteSpace.Normal;
            informationBody = new ScrollView(ScrollViewMode.Vertical);
            informationBody.name = "flight-information-scroll";
            informationBody.style.flexShrink = 1;
            informationBody.style.minHeight = 0;
            informationBody.Add(flightDetailText);
            flightDetails.Add(informationBody);
            root.Add(flightDetails);

            stagingPanel = Panel();
            stagingPanel.name = "staging-stack";
            stagingPanel.pickingMode = PickingMode.Ignore;
            stagingPanel.style.width = 220;
            Absolute(stagingPanel, 18, null, null, 288);
            stagingPanel.Add(Label("STAGING", 10, Cyan, FontStyle.Bold));
            boosterRow = new StagingFuelRow("solid-booster-stage");
            stagingPanel.Add(boosterRow);
            root.Add(stagingPanel);

            var dock = new VisualElement(); dock.name = "cockpit-bottom-strip";
            dock.pickingMode = PickingMode.Ignore;
            dock.style.height = 268;
            vehicleGroup = VehicleSelector(false);
            Absolute(vehicleGroup, 6, 0, null, null);
            dock.Add(vehicleGroup);
            informationBody.Add(modelCredit);
            telemetry = Row(); telemetry.name = "primary-telemetry";
            telemetry.pickingMode = PickingMode.Ignore;
            Absolute(telemetry, 0, null, null, 0);
            speedGauge = new BroadcastGauge("SPEED", "KM/H", "speed-readout");
            altitudeGauge = new BroadcastGauge("ALTITUDE", "KM", "altitude-readout");
            speedGauge.tooltip = "Speed relative to the rotating Earth surface";
            altitudeGauge.tooltip = "Altitude above the WGS84 ellipsoid";
            telemetry.Add(speedGauge); telemetry.Add(altitudeGauge);
            dock.Add(telemetry);
            var fuel = Row(); compactFuel = fuel; fuel.name = "fuel-readout";
            fuel.pickingMode = PickingMode.Ignore;
            Absolute(fuel, 8, null, null, 0);
            stageFuel = Label("", 9, Text, FontStyle.Normal); stageFuel.name = "stage-fuel-readout";
            boosterFuel = Label("", 9, Muted, FontStyle.Normal); boosterFuel.name = "booster-fuel-readout";
            boosterFuel.style.marginLeft = 12;
            fuel.Add(stageFuel); fuel.Add(boosterFuel); dock.Add(fuel);

            navballHousing = Column(); navballHousing.name = "navball-housing";
            navballHousing.style.width = 144; navballHousing.style.flexShrink = 0;
            navballHousing.pickingMode = PickingMode.Ignore;
            Absolute(navballHousing, null, null, null, 0);
            navballHousing.style.height = 145; navballHousing.style.alignSelf = Align.FlexEnd;
            navballHousing.style.alignItems = Align.Center;
            navball = new NavballElement(); navball.name = "flight-navball";
            navball.style.width = 120; navball.style.height = 120; navball.style.flexShrink = 0;
            navballHousing.Add(navball);
            missionTime = Label("T+ 00:00:00  /  1×", 10, Cyan, FontStyle.Bold);
            missionTime.name = "mission-time-and-rate";
            missionTime.style.marginTop = 4;
            navballHousing.Add(missionTime); dock.Add(navballHousing);

            var right = Row(); inputStrip = right; right.name = "cockpit-flight-inputs"; right.pickingMode = PickingMode.Ignore;
            Absolute(right, null, 0, 0, null);
            right.style.width = 300;
            right.style.justifyContent = Justify.SpaceBetween; right.style.alignItems = Align.Center;
            var guidance = Column(); guidance.style.width = 102; guidance.style.flexShrink = 0;
            assist = Button("ASSIST ON", () => { if (director?.FlightDriver is RocketFlightDriver f) f.ToggleAssistance(); Refresh(); }, false, "assistance-toggle");
            assist.tooltip = "Assisted ascent follows the same throttle, steering and staging limits. Manual pitch: W/S or ↑/↓. Heading: A/D. Steering follows the model’s finite rate.";
            guidance.Add(assist);
            var rates = Row(); rates.name = "time-rate-controls";
            rateButtons[0] = Button("−", () => ChangeWarp(-1), false, "time-rate-down");
            rateButtons[1] = Button("+", () => ChangeWarp(1), false, "time-rate-up");
            rateButtons[1].tooltip = "Above 20× requires a verified, unpowered orbit. Achieved speed may be lower when the simulation is rate limited.";
            warpValue = Label("1×", 10, Text, FontStyle.Bold);
            warpValue.style.flexGrow = 1; warpValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            rates.Add(rateButtons[0]); rates.Add(warpValue); rates.Add(rateButtons[1]);
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
            reentry = Button("RE-ENTRY", () => {
                if (director?.FlightDriver is RocketFlightDriver f) f.BeginReentry();
                Refresh();
            }, false, "starship-reentry-action");
            reentry.style.height = 25;
            reentry.style.fontSize = 10;
            staging.Add(reentry);
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
            dock.Add(right);
            previewHousing = Column(); previewHousing.name = "live-preview";
            previewHousing.style.backgroundColor = new Color(.015f, .025f, .035f, .6f);
            Border(previewHousing, new Color(1, 1, 1, .45f));
            Absolute(previewHousing, null, null, 4, 4);
            previewHousing.RegisterCallback<ClickEvent>(_ => ToggleView());
            previewHousing.tooltip = "Click to swap main and inset views [M]";
            previewImage = new Image { name = "live-preview-image", scaleMode = ScaleMode.ScaleToFit };
            previewImage.pickingMode = PickingMode.Ignore;
            previewImage.style.width = Length.Percent(100); previewImage.style.flexGrow = 1;
            previewImage.style.flexBasis = 0; previewImage.style.minHeight = 0;
            previewHousing.Add(previewImage);
            previewMarker = Label("▲", 12, Color.white, FontStyle.Bold);
            previewMarker.name = "live-preview-vehicle-marker";
            previewMarker.style.position = Position.Absolute;
            previewMarker.style.display = DisplayStyle.None;
            previewMarker.style.unityTextOutlineColor = Color.black;
            previewMarker.style.unityTextOutlineWidth = 1;
            previewHousing.Add(previewMarker);
            previewCaption = Label("LIVE  ·  ALTERNATE VIEW", 9, Text, FontStyle.Normal);
            previewCaption.name = "live-preview-caption";
            previewCaption.style.paddingLeft = 8; previewCaption.style.height = 21;
            previewCaption.style.unityTextAlign = TextAnchor.MiddleLeft;
            previewHousing.Add(previewCaption);
            dock.Add(previewHousing);
            flightControls.Add(dock); root.Add(flightControls);
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
            float width = root.resolvedStyle.width;
            if (width <= 0) return;
            // Leave the close button outside the scrolling copy and constrain the panel
            // to the actual UI viewport, including short windows and compact displays.
            float height = root.resolvedStyle.height;
            float detailsBottom = Mathf.Min(292, Mathf.Max(16, height * .4f));
            flightDetails.style.bottom = detailsBottom;
            flightDetails.style.maxHeight = Mathf.Max(80, height - detailsBottom - 24);
            flightDetails.style.width = Mathf.Min(400, Mathf.Max(160, width - 36));
            stagingPanel.style.width = width < 760 ? 184 : 220;
            stagingPanel.style.bottom = 288;
            bool compact = width < 1050;
            bool narrow = width < 760;
            float gaugeSize = narrow ? 100 : compact ? 130 : 160;
            vehicleGroup.style.width = compact ? 112 : 140;
            speedGauge.SetSize(gaugeSize); altitudeGauge.SetSize(gaugeSize);
            telemetry.style.bottom = 18;
            navballHousing.style.left = (width - 24 - 144) * .5f;
            float navSize = narrow ? 92 : 120;
            navball.style.width = navSize; navball.style.height = navSize;
            navballHousing.style.height = navSize + 25;
            previewHousing.style.width = narrow ? 160 : compact ? 220 : 280;
            previewHousing.style.height = (narrow ? 160 : compact ? 220 : 280) * 9f / 16f + 21;
            inputStrip.style.top = 0;
            flightControls.style.height = narrow ? 252 : 268;

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
        private void ChangeWarp(int direction)
        {
            if (director?.FlightDriver is RocketFlightDriver flight)
            {
                int index = Array.IndexOf(Rates, flight.TimeRate);
                flight.SetTimeRate(Rates[Mathf.Clamp(index + direction, 0, Rates.Length - 1)]);
                Refresh();
            }
        }
        private void OpenWeather() { weatherOpen = true; Refresh(); }

        private void RefreshMarkers()
        {
            if (root == null || root.panel == null) return;
            var camera = director != null ? director.mapCamera : null;
            var controls = camera != null ? camera.GetComponent<GlobeCameraControls>() : null;
            bool systemView = director != null && director.IsMapView && controls != null && controls.Distance >= 1000;
            var sunlight = director != null ? director.GetComponent<MissionSunlight>() : null;
            PlaceSystemLabel(systemEarth, camera, Vector3.zero, systemView);
            systemVehicle.text = director != null && director.VehicleIndex == 1 ? "▲  STARSHIP" :
                director != null && director.VehicleIndex == 2 ? "▲  SLS" : "▲  ESP DEMO";
            PlaceSystemLabel(systemVehicle, camera, controls != null ? controls.FocusPosition : Vector3.zero, systemView, 4);
            Vector3 sunPosition = sunlight != null ? new Vector3((float)sunlight.SunEcef.X, (float)sunlight.SunEcef.Z,
                (float)sunlight.SunEcef.Y) * SolarSystemMap.AstronomicalUnit : Vector3.zero;
            PlaceSystemLabel(systemSun, camera, sunPosition, systemView && sunlight != null && sunlight.CurrentUtc != default);
            for (int i = 0; i < orbitMarkers.Length; i++)
            {
                Vector2 orbitScreen = default;
                double apsisHeight = 0;
                bool showOrbit = !systemView && director != null && director.IsMapView && director.OrbitVisual != null &&
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
            bool rocketVisible = !systemView && director != null && director.TryGetRocketScreenPosition(out rocketScreen);
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
                bool show = !systemView && director != null && director.IsMapView && director.TryGetSiteScreenPosition(i, out screen);
                markers[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                markers[i].style.left = point.x - 18; markers[i].style.top = point.y - 16;
                markers[i].text = "●  " + (i == 0 ? "STARBASE" : "LC-39B");
            }
        }

        private void PlaceSystemLabel(Label label, Camera camera, Vector3 position, bool visible, float yOffset = -18)
        {
            Vector3 screen = visible && camera != null ? camera.WorldToScreenPoint(position) : Vector3.zero;
            visible = visible && camera != null && screen.z > 0 && camera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            label.style.left = point.x + 9;
            label.style.top = point.y + yOffset;
        }

        private void Refresh()
        {
            if (mapControls == null) return;
            bool available = director != null;
            bool isMap = !available || director.IsMapView;
            var flight = available ? director.FlightDriver as RocketFlightDriver : null;
            bool mapFlying = isMap && flight != null && flight.HasPosition;
            mapControls.style.display = isMap ? DisplayStyle.Flex : DisplayStyle.None;
            flightControls.style.display = DisplayStyle.Flex;
            // Map layers stay above the shared broadcast instruments in both preflight and flight.
            mapControls.style.top = 70;
            mapControls.style.bottom = new StyleLength(StyleKeyword.Auto);
            mapControls.style.right = new StyleLength(StyleKeyword.Auto);
            mapControls.style.width = 350;
            mapVehicleGroup.style.display = DisplayStyle.None;
            mapDetailsToggle.style.display = DisplayStyle.None;
            flightDetails.style.display = detailsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            // Leave room between the top bar and cockpit dock, including the SLS booster row.
            bool showStaging = !isMap && !detailsOpen && flight != null && root.resolvedStyle.height >= 520;
            stagingPanel.style.display = showStaging ? DisplayStyle.Flex : DisplayStyle.None;
            compactFuel.style.display = showStaging ? DisplayStyle.None : DisplayStyle.Flex;
            weatherDetails.style.display = isMap && weatherOpen ? DisplayStyle.Flex : DisplayStyle.None;
            weatherDetails.style.bottom = 292;
            viewToggle.text = isMap ? "FLIGHT  [M]" : "MAP  [M]";
            viewToggle.SetEnabled(available);
            if (director != null && director.mapLayers != null) selectedLayer = director.mapLayers.SelectedIndex + 1;
            for (int i = 0; i < layers.Length; i++) Selected(layers[i], i == selectedLayer);
            Selected(weatherToggle, weatherOpen);
            Selected(detailsToggle, detailsOpen);
            Selected(mapDetailsToggle, detailsOpen);
            if (!available) return;
            var inset = director.GetComponent<MissionInsetView>();
            SetPreview(inset != null && inset.HasPreview ? inset.PreviewTexture : null, inset != null && inset.PreviewIsMap);
            bool showPreviewMarker = inset != null && inset.HasPreview && inset.PreviewIsMap && inset.HasMarker;
            previewMarker.style.display = showPreviewMarker ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPreviewMarker)
            {
                var point = inset.MarkerViewportPosition;
                previewMarker.style.left = Mathf.Clamp01(point.x) * previewImage.resolvedStyle.width - 6;
                previewMarker.style.top = (1 - Mathf.Clamp01(point.y)) * previewImage.resolvedStyle.height - 6;
            }
            // The shared dock carries mission time/rate in both views; avoid duplicate clocks.
            mapMission.style.display = DisplayStyle.None;
            double seconds = Math.Max(0, director.MissionTimeSeconds);
            headerMissionTime.text = string.Format("T+ {0:00}:{1:00}:{2:00}",
                (int)(seconds / 3600), (int)(seconds / 60) % 60, (int)seconds % 60);
            string clock = string.Format("T+ {0:00}:{1:00}:{2:00}  / requested {3}×", (int)(seconds / 3600), (int)(seconds / 60) % 60, (int)seconds % 60, flight != null ? flight.TimeRate : 1);
            missionTime.text = clock; mapMission.text = clock;
            if (flight != null)
            {
                string actual = string.Format(" · actual avg {0:F1}×{1}", flight.ActualTimeRate, flight.SimulationCatchingUp ? " (limited)" : "");
                missionTime.text += actual; mapMission.text += actual;
            }
            weather.text = director.WeatherSummary ?? "Weather unavailable";
            string imageryDate = director.mapLayers != null ? director.mapLayers.DisplayedDate : "";
            mapCredit.text = string.IsNullOrEmpty(imageryDate) ? "NASA · historical Blue Marble fallback" : "NASA · through " + imageryDate + " UTC\n" + director.mapLayers.Freshness;
            if (director.mapLayers != null && !string.IsNullOrEmpty(director.mapLayers.SelectionStatus))
                mapCredit.text += "\n" + director.mapLayers.SelectionStatus;
            mapCredit.tooltip = director.MapCredit;
            if (director.mapLayers != null && (director.mapLayers.Status.Contains("retained") || director.mapLayers.Status.StartsWith("No verified")))
                mapCredit.text += "\n" + director.mapLayers.Status;
            var mapCameraControls = director.mapCamera != null ? director.mapCamera.GetComponent<GlobeCameraControls>() : null;
            if (isMap && mapCameraControls != null && mapCameraControls.Distance >= 1000)
                mapCredit.text += " · EARTH ORBIT APPROXIMATE";

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
            status.style.display = flight != null && flight.ReentryActive ? DisplayStyle.Flex : DisplayStyle.None;
            status.tooltip = director.FlightStatus ?? "";
            status.style.color = flight != null && flight.OrbitVerified ? Cyan : Text;
            string vehicleInfo = director.VehicleIndex == 0 ? "ESP demo preset." : "Vehicle performance uses estimated specifications.";
            string mapInfo = (director.GetComponent<MissionSunlight>()?.Status ?? "") + "\n" + director.MapCredit + "\n\n" + director.SiteName + "\nRed triangle: current simulated vehicle position. Flight attitude is shown on the navball.\n" + vehicleInfo +
                "\nCyan: coast prediction — engines cut now; ignores drag. Muted blue: actual flown path behind the vehicle. Ap/Pe mark radial apsides with local WGS84 heights. Impact clips the path at the surface." +
                "\nSun / Earth view: the Sun follows the lighting direction; Earth's orbit is a visual, circular 1 AU approximation, not a simulated heliocentric trajectory.";
            string steeringInfo = "Pitch: W/S or ↑/↓  ·  Heading: A/D  ·  Stage: Space  ·  Map: M\n" +
                "Throttle or steering input switches assistance off. Attitude uses simplified elevation/heading with a finite steering rate; roll is not modeled.";
            var localSurface = director.GetComponent<LocalSurfaceTiles>();
            string surfaceInfo = localSurface != null ? "\n\n" + LocalSurfaceTiles.Attribution + "\n" + localSurface.Status : "";
            if (director.SelectedSiteIndex == 0 && director.siteStructurePrefabs != null &&
                director.siteStructurePrefabs.Length > 0 && director.siteStructurePrefabs[0])
                surfaceInfo += "\nStarbase tower and mount: herbys (Enrique Saggese), CC BY 4.0 as declared in the source handoff. Approximate ESP adaptation.";
            if (director.SelectedSiteIndex == 1 && director.siteStructurePrefabs != null && director.siteStructurePrefabs.Length > 1)
            {
                var structure = director.siteStructurePrefabs[1];
                if (LaunchEnvironment.IsModernLauncher(structure))
                    surfaceInfo += "\nApproximate LC-39B mobile launcher: NASA / Samuel H. Neblett. Simplified visual, not a surveyed reconstruction.";
                else if (structure != null)
                    surfaceInfo += "\nHistorical LC-39 gantry: NASA / Michael D. Carbajal. Approximate scale and placement; not the current facility layout.";
            }
            surfaceInfo += "\nExhaust textures: Waterfall / Chris Adderley (Nertea) and contributors, CC BY-NC-SA 4.0; converted to PNG. SRB emission scheduling: SmokeScreen / Sarbian, BSD-2-Clause. Full notices accompany the game.";
            string flightInfo = (director.FlightStatus ?? "READY") + "\n\n" +
                (flight != null ? flight.FlightTelemetry : "Flight simulation unavailable") +
                "\n" + (director.GetComponent<MissionSunlight>()?.Status ?? "") + "\n\n" + steeringInfo;
            flightDetailText.text = isMap ? mapInfo + (mapFlying ? "\n\n" + flightInfo : "") + surfaceInfo : flightInfo + "\n\n" + vehicleInfo + surfaceInfo;
            assist.SetEnabled(flight != null && !flight.ReentryActive);
            assist.text = flight != null && flight.ReentryActive ? "RETURN AUTO" : flight != null && flight.AssistanceEnabled ? "ASSIST ON" : "MANUAL";
            Selected(assist, flight != null && flight.AssistanceEnabled);
            for (int i = 0; i < rateButtons.Length; i++)
            {
                rateButtons[i].SetEnabled(flight != null);
            }
            if (warpValue != null) warpValue.text = (flight != null ? flight.TimeRate : 1) + "×";
            altitudeGauge.SetValue((director.AltitudeMeters / 1000).ToString(Math.Abs(director.AltitudeMeters) < 10000 ? "N2" : "N1"));
            speedGauge.SetValue((director.SpeedMetersPerSecond * 3.6).ToString("N0"));
            if (flight != null)
            {
                var fuel = flight.FuelTelemetry;
                if (showStaging) RefreshStaging(flight, fuel);
                stageFuel.text = $"FUEL  S{fuel.StageNumber}  {fuel.StageKg:N0} kg";
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
            action.SetEnabled(director.FlightDriver != null && !(flight?.ReentryActive ?? false));
            reentry.style.display = director.VehicleIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            reentry.SetEnabled(flight != null && flight.CanBeginReentry);
            reentry.text = flight != null && flight.ReentryActive ? "RETURNING" : "RE-ENTRY";
            reentry.tooltip = flight != null ? flight.ReentryStatus : "Starship return to Starbase";
            throttle.SetEnabled(director.FlightDriver != null && !(flight?.ReentryActive ?? false));
            throttle.SetValueWithoutNotify(director.Throttle);
            throttleValue.text = (director.Throttle * 100).ToString("F0") + "%";
            throttleValue.style.color = director.Throttle > 0 ? Text : Muted;
        }

        private static string Distance(double meters) => Math.Abs(meters) >= 10000 ? (meters / 1000).ToString("N1") + " km" : meters.ToString("N0") + " m";
        /// <summary>The alternate-view renderer supplies its live texture without owning HUD layout.</summary>
        public void SetPreview(RenderTexture texture, bool isMap)
        {
            if (previewImage == null) return;
            if (previewImage.image != texture) previewImage.image = texture;
            previewCaption.text = texture == null ? "PREVIEW UNAVAILABLE" : isMap ? "LIVE  ·  EARTH MAP  ↗" : "LIVE  ·  FLIGHT  ↗";
        }
        private static void Selected(Button button, bool selected)
        {
            button.style.color = selected ? Cyan : Text;
            button.style.backgroundColor = selected ? new Color(.07f, .17f, .20f, .98f) : Raised;
            Border(button, selected ? new Color(.22f, .49f, .53f) : Edge);
        }
        private void RefreshStaging(RocketFlightDriver flight, (int StageNumber, double StageKg, double? BoosterKg) fuel)
        {
            int stageCount = flight.StageCount;
            while (stageRows.Count < stageCount)
            {
                var row = new StagingFuelRow($"staging-stage-{stageRows.Count + 1}");
                stageRows.Add(row);
                stagingPanel.Insert(stagingPanel.childCount - 1, row);
            }

            for (int i = 0; i < stageRows.Count; i++)
            {
                var row = stageRows[i];
                row.style.display = i < stageCount ? DisplayStyle.Flex : DisplayStyle.None;
                if (i >= stageCount) continue;
                int stage = i + 1;
                bool active = stage == fuel.StageNumber;
                bool separated = stage < fuel.StageNumber;
                double capacity = flight.StagePropellantCapacityKg(i);
                double remaining = active ? fuel.StageKg : capacity;
                string state = separated ? "SEPARATED" : !active ? "QUEUED" : remaining <= 0 ? "EMPTY" : "CURRENT";
                row.UpdateFuel($"STAGE {stage}", state, separated ? (double?)null : remaining, capacity, active);
            }

            double? boosterCapacity = flight.BoosterPropellantCapacityKg;
            boosterRow.style.display = boosterCapacity.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!boosterCapacity.HasValue) return;
            bool burning = flight.Simulation?.BoosterBurning == true;
            string boosterState = !fuel.BoosterKg.HasValue ? "SEPARATED" : fuel.BoosterKg.Value <= 0 ? "SPENT" : burning ? "BURNING" : "ATTACHED";
            boosterRow.UpdateFuel("SOLID BOOSTERS", boosterState, fuel.BoosterKg, boosterCapacity.Value, burning);
        }

        private sealed class StagingFuelRow : VisualElement
        {
            private readonly Label title, status, readout;
            private readonly VisualElement fill;

            public StagingFuelRow(string name)
            {
                this.name = name;
                pickingMode = PickingMode.Ignore;
                style.marginTop = 5;
                style.paddingLeft = style.paddingRight = 6;
                style.paddingTop = style.paddingBottom = 3;
                style.backgroundColor = Raised;
                Radius(this, 3);
                var heading = Row();
                heading.pickingMode = PickingMode.Ignore;
                heading.style.justifyContent = Justify.SpaceBetween;
                title = Label("", 9, Text, FontStyle.Bold);
                status = Label("", 8, Muted, FontStyle.Bold);
                heading.Add(title);
                heading.Add(status);
                Add(heading);

                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.style.height = 4;
                track.style.backgroundColor = Edge;
                track.style.overflow = Overflow.Hidden;
                Radius(track, 2);
                fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.style.height = Length.Percent(100);
                track.Add(fill);
                Add(track);
                readout = Label("", 9, Muted, FontStyle.Normal);
                Add(readout);
            }

            public void UpdateFuel(string label, string state, double? remaining, double capacity, bool active)
            {
                float fraction = remaining.HasValue && capacity > 0
                    ? Mathf.Clamp01((float)(remaining.Value / capacity)) : 0f;
                title.text = label;
                title.style.color = remaining.HasValue ? Text : Muted;
                status.text = state;
                status.style.color = active ? Amber : Muted;
                fill.style.backgroundColor = active ? Cyan : Muted;
                fill.style.width = Length.Percent(100 * fraction);
                readout.text = remaining.HasValue ? $"{remaining.Value:N0} kg  ·  {fraction:P0}" : "—";
                Border(this, active ? Amber : Edge);
            }
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
