using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace ESP.Presentation
{
    /// <summary>Session menus and an optional live physics readout; the session owns all game state.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionMenuUI : MonoBehaviour
    {
        private VisualElement root, gameplay, overlayRoot, modal, card, physicsPanel, modifiedBadge;
        private ScrollView menuScroll, physicsScroll;
        private Label physicsText, physicsState, gravityValue, settingsNote, cheatState, modifiedText, impactText;
        private Button menuButton, resumeButton, collapsePhysics, impactRestart;
        private Slider gravitySlider;
        private Toggle infiniteFuel, crashDestruction, vSync, physicsVisible;
        private readonly List<Button> capButtons = new List<Button>();
        private static readonly int[] Caps = { 30, 60, 90, 120, 144, 0 };
        private MissionMenuPage? renderedPage;
        private float nextReadout;
        private bool physicsCollapsed;
        private VisualElement nerdHeader;
        private Vector2 nerdPointerStart, nerdPanelStart;
        private bool draggingNerdPanel;
        private VisualElement nerdPanel;
        private Label nerdText;
        private Toggle nerdToggle;
        private bool nerdVisible;
        private readonly Queue<string> nerdEvents = new Queue<string>();
        private string lastNerdState;
        private float nextNerdUpdate;
        private static readonly Color Backdrop = new Color(.012f, .025f, .045f, .84f);
        private static readonly Color Surface = new Color(.035f, .052f, .075f, .98f);
        private static readonly Color Raised = new Color(.065f, .092f, .12f, 1);
        private static readonly Color Text = new Color(.90f, .95f, .97f);
        private static readonly Color Muted = new Color(.55f, .66f, .73f);
        private static readonly Color Cyan = new Color(.39f, .86f, .89f);
        private static readonly Color Amber = new Color(.99f, .73f, .32f);
        private static readonly Color Edge = new Color(.18f, .27f, .34f);

        private IEnumerator Start()
        {
            // MissionUI clears the shared document in Start. Append only after that has completed.
            yield return null;
            var document = GetComponent<UIDocument>();
            root = document.rootVisualElement;
            if (root == null) yield break;
            var existing = new List<VisualElement>(root.Children());
            gameplay = new VisualElement { name = "mission-gameplay-hud", pickingMode = PickingMode.Ignore };
            Fill(gameplay);
            root.Add(gameplay);
            foreach (var element in existing) gameplay.Add(element);

            overlayRoot = new VisualElement { name = "mission-session-ui", pickingMode = PickingMode.Ignore };
            Fill(overlayRoot);
            root.Add(overlayRoot);
            BuildFlightOverlay();
            nerdVisible = PlayerPrefs.GetInt("ESP.NerdStats", 0) == 1;
            BuildNerdPanel();
            BuildModal();
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RefreshSession();
            RefreshNerdStats();
        }

        private void Update()
        {
            if (root == null || overlayRoot == null) return;
            RefreshSession();
            RefreshNerdStats();
        }

        private void RefreshSession()
        {
            var session = MissionSession.Instance;
            if (session == null) return;
            bool open = session.Page != MissionMenuPage.None;
            gameplay.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            modal.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            menuButton.style.display = !open && session.HasSession ? DisplayStyle.Flex : DisplayStyle.None;
            modifiedBadge.style.display = !open && session.HasSession && (session.ModifiedFlight || session.FlightDestroyed) ? DisplayStyle.Flex : DisplayStyle.None;
            physicsPanel.style.display = !open && session.HasSession && session.PhysicsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            nerdPanel.style.display = !open && session.HasSession && nerdVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open && nerdVisible) ClampNerdPanel(new Vector2(nerdPanel.resolvedStyle.left, nerdPanel.resolvedStyle.top));
            if (renderedPage != session.Page)
            {
                renderedPage = session.Page;
                BuildPage(session);
                SyncValues(session);
            }
            if (Time.unscaledTime >= nextReadout)
            {
                nextReadout = Time.unscaledTime + .25f;
                SyncValues(session);
            }
        }

        private void BuildFlightOverlay()
        {
            menuButton = Button("MENU  [ESC]", "session-menu-button", () => MissionSession.Instance?.OpenPause());
            menuButton.style.position = Position.Absolute;
            menuButton.style.top = 16; menuButton.style.right = 122;
            menuButton.style.height = 29; menuButton.style.fontSize = 10;
            menuButton.style.paddingLeft = 10; menuButton.style.paddingRight = 10;
            overlayRoot.Add(menuButton);

            var badgeAnchor = new VisualElement { pickingMode = PickingMode.Ignore };
            badgeAnchor.style.position = Position.Absolute;
            badgeAnchor.style.left = 0; badgeAnchor.style.right = 0; badgeAnchor.style.top = 18;
            badgeAnchor.style.alignItems = Align.Center;
            modifiedBadge = new VisualElement { name = "modified-flight-badge", pickingMode = PickingMode.Position };
            modifiedBadge.style.backgroundColor = Surface;
            modifiedBadge.style.paddingLeft = 9; modifiedBadge.style.paddingRight = 9;
            modifiedBadge.style.paddingTop = 5; modifiedBadge.style.paddingBottom = 5;
            Border(modifiedBadge, new Color(.49f, .34f, .15f)); Radius(modifiedBadge, 3);
            impactText = Label("IMPACT · VEHICLE DESTROYED", 9, new Color(.99f, .43f, .37f), true);
            impactText.name = "impact-destruction-notice";
            impactText.style.unityTextAlign = TextAnchor.MiddleCenter;
            modifiedText = Label("MODIFIED FLIGHT · NOT VERIFIED", 9, Amber, true);
            modifiedText.name = "active-flight-overrides";
            modifiedText.style.unityTextAlign = TextAnchor.MiddleCenter;
            modifiedBadge.Add(impactText); modifiedBadge.Add(modifiedText);
            impactRestart = Button("RESTART MISSION", "impact-restart-mission", () => MissionSession.Instance?.RestartMission());
            impactRestart.style.height = 28; impactRestart.style.marginTop = 6;
            impactRestart.style.fontSize = 10;
            modifiedBadge.Add(impactRestart);
            badgeAnchor.Add(modifiedBadge); overlayRoot.Add(badgeAnchor);

            physicsPanel = new VisualElement { name = "live-physics-panel" };
            physicsPanel.style.position = Position.Absolute;
            physicsPanel.style.top = 128; physicsPanel.style.right = 14;
            physicsPanel.style.width = 278;
            physicsPanel.style.backgroundColor = Surface;
            physicsPanel.style.paddingLeft = 11; physicsPanel.style.paddingRight = 11;
            physicsPanel.style.paddingTop = 7; physicsPanel.style.paddingBottom = 9;
            Border(physicsPanel, Edge); Radius(physicsPanel, 5);
            var heading = Row(); heading.style.alignItems = Align.Center;
            var title = Label("FLIGHT PHYSICS", 10, Cyan, true); title.style.flexGrow = 1;
            heading.Add(title);
            collapsePhysics = IconButton("−", "collapse-physics", () => {
                physicsCollapsed = !physicsCollapsed;
                physicsScroll.style.display = physicsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
                collapsePhysics.text = physicsCollapsed ? "+" : "−";
                collapsePhysics.tooltip = physicsCollapsed ? "Expand physics readout" : "Collapse physics readout";
            });
            collapsePhysics.tooltip = "Collapse physics readout";
            heading.Add(collapsePhysics);
            var close = IconButton("×", "close-physics", () => MissionSession.Instance?.SetPhysicsVisible(false));
            close.tooltip = "Hide physics readout";
            heading.Add(close); physicsPanel.Add(heading);
            physicsScroll = new ScrollView(ScrollViewMode.Vertical) { name = "physics-readout-scroll" };
            physicsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            physicsScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            physicsState = Label("", 9, Amber, true); physicsState.style.marginBottom = 5;
            physicsState.style.whiteSpace = WhiteSpace.Normal;
            physicsScroll.Add(physicsState);
            physicsText = Label("— Launch to inspect forces", 11, Text);
            physicsText.name = "live-physics-values";
            physicsText.style.whiteSpace = WhiteSpace.Normal;
            physicsScroll.Add(physicsText); physicsPanel.Add(physicsScroll);
            overlayRoot.Add(physicsPanel);
        }

        private void BuildModal()
        {
            modal = new VisualElement { name = "session-modal", pickingMode = PickingMode.Position, focusable = true };
            Fill(modal);
            modal.style.backgroundColor = Backdrop;
            modal.style.alignItems = Align.Center;
            modal.style.justifyContent = Justify.Center;
            modal.style.paddingLeft = 18; modal.style.paddingRight = 18;
            modal.style.paddingTop = 18; modal.style.paddingBottom = 18;
            // Empty areas still intercept pointers; scene input is additionally gated by MissionSession.BlocksInput.
            modal.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            modal.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            card = new VisualElement { name = "session-menu-card" };
            card.style.width = 430;
            card.style.maxWidth = new Length(100, LengthUnit.Percent);
            card.style.maxHeight = new Length(100, LengthUnit.Percent);
            card.style.paddingLeft = 24; card.style.paddingRight = 24;
            card.style.paddingTop = 22; card.style.paddingBottom = 20;
            card.style.backgroundColor = Surface;
            Border(card, Edge); Radius(card, 9);
            modal.Add(card); overlayRoot.Add(modal);
        }

        private void BuildPage(MissionSession session)
        {
            card.Clear();
            capButtons.Clear();
            resumeButton = null; gravitySlider = null; gravityValue = null;
            infiniteFuel = null; crashDestruction = null; vSync = null; physicsVisible = null;
            settingsNote = null; cheatState = null;
            if (session.Page == MissionMenuPage.None) { menuScroll = null; return; }

            string eyebrow = session.Page == MissionMenuPage.Start ? "EARTH SPACE PROGRAM" :
                session.Page == MissionMenuPage.Pause ? "MISSION PAUSED" : "FLIGHT DECK";
            card.Add(Label(eyebrow, 10, Cyan, true));
            string title = session.Page == MissionMenuPage.Start ? "ESP" :
                session.Page == MissionMenuPage.Pause ? "Pause" :
                session.Page == MissionMenuPage.Settings ? "Settings" : session.Page == MissionMenuPage.Tutorial ? "Quick tutorial" : "Cheats";
            var titleLabel = Label(title, session.Page == MissionMenuPage.Start ? 48 : 31, Text, true);
            titleLabel.style.marginTop = 4; titleLabel.style.marginBottom = 17;
            card.Add(titleLabel);
            menuScroll = new ScrollView(ScrollViewMode.Vertical) { name = "session-menu-content" };
            menuScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            menuScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            menuScroll.style.flexShrink = 1;
            card.Add(menuScroll);
            switch (session.Page)
            {
                case MissionMenuPage.Start: BuildStart(session); break;
                case MissionMenuPage.Pause: BuildPause(session); break;
                case MissionMenuPage.Settings: BuildSettings(session); break;
                case MissionMenuPage.Cheats: BuildCheats(session); break;
                case MissionMenuPage.Tutorial: BuildTutorial(session); break;
            }
            Resize();
        }

        private void BuildStart(MissionSession session)
        {
            AddAction("New mission", "new-mission", session.NewMission, true);
            resumeButton = AddAction("Resume mission", "resume-from-start", session.ResumeMission);
            resumeButton.SetEnabled(session.HasSession);
            AddAction("Settings", "start-settings", session.OpenSettings);
            AddAction("Quit", "start-quit", session.Quit);
        }

        private void BuildPause(MissionSession session)
        {
            AddAction("Resume flight", "resume-flight", session.ResumeMission, true);
            AddAction("Restart mission", "restart-mission", session.RestartMission);
            physicsVisible = Switch("Live physics panel", "physics-panel-toggle", session.SetPhysicsVisible);
            menuScroll.Add(physicsVisible);
            AddAction("Cheats", "pause-cheats", session.OpenCheats);
            AddAction("Settings", "pause-settings", session.OpenSettings);
            AddAction("Main menu", "return-to-start", session.ReturnToStart);
        }

        private void BuildSettings(MissionSession session)
        {
            AddAction("Open tutorial", "open-tutorial", session.OpenTutorial);
            var lighting = FindFirstObjectByType<MissionSunlight>();
            if (lighting != null)
            {
                menuScroll.Add(Label("DAY / NIGHT", 10, Muted, true));
                var timeLabel = Label("Visual time offset: " + lighting.LightingOffsetHours.ToString("+0.0;-0.0;0.0") + " h", 11, Text);
                menuScroll.Add(timeLabel);
                var timeSlider = new Slider(-12, 12) { name = "lighting-time-offset" };
                timeSlider.SetValueWithoutNotify(lighting.LightingOffsetHours);
                timeSlider.RegisterValueChangedCallback(evt => {
                    float hours = Mathf.Round(evt.newValue * 2) / 2;
                    lighting.SetLightingOffset(hours);
                    timeSlider.SetValueWithoutNotify(hours);
                    timeLabel.text = "Visual time offset: " + hours.ToString("+0.0;-0.0;0.0") + " h";
                });
                menuScroll.Add(timeSlider);
                var lightingNote = Note("Changes the sky and lighting; flight time and physics stay unchanged. Set to 0 for actual time.");
                lightingNote.style.marginBottom = 10;
                menuScroll.Add(lightingNote);
            }
            nerdToggle = Switch("Stats for nerds", "nerd-stats-toggle", value => {
                nerdVisible = value;
                PlayerPrefs.SetInt("ESP.NerdStats", value ? 1 : 0);
                PlayerPrefs.Save();
            });
            nerdToggle.SetValueWithoutNotify(nerdVisible);
            menuScroll.Add(nerdToggle);
            var panel = GetComponent<UIDocument>().panelSettings;
            if (panel != null)
            {
                menuScroll.Add(Label("INTERFACE", 10, Muted, true));
                var scaleLabel = Label("GUI scale: " + Mathf.RoundToInt(panel.scale * 100) + "%", 11, Text);
                menuScroll.Add(scaleLabel);
                var scale = new Slider(.75f, 1.25f) { name = "gui-scale" };
                scale.SetValueWithoutNotify(panel.scale);
                scale.RegisterValueChangedCallback(evt => {
                    panel.scale = Mathf.Round(evt.newValue * 20) / 20;
                    scale.SetValueWithoutNotify(panel.scale);
                    scaleLabel.text = "GUI scale: " + Mathf.RoundToInt(panel.scale * 100) + "%";
                });
                menuScroll.Add(scale);
            }
            menuScroll.Add(Label("KEYBOARD INPUT", 10, Muted, true));
            var keyboardHelp = Note("W / S — pitch up / down\nA / D — yaw left / right\nQ / E — roll\nHold Shift with a steering key for full control; normal input is half strength. Rotation continues after release.\nShift / Ctrl alone — increase / decrease throttle\nSpace — launch or stage\nM — map / flight view\nEsc — pause / back\n\nManual steering is a simplified rotational model, not a physical gimbal simulation.");
            keyboardHelp.name = "keyboard-input-reference";
            keyboardHelp.style.marginBottom = 16;
            menuScroll.Add(keyboardHelp);
            menuScroll.Add(Label("GRAPHICS · NIGHT SKY", 10, Muted, true));
            var brightnessLabel = Label("Milky Way brightness: " + Mathf.RoundToInt(session.MilkyWayBrightness) + "%", 11, Text);
            menuScroll.Add(brightnessLabel);
            var brightness = new Slider(10, 500) { name = "milky-way-brightness" };
            brightness.SetValueWithoutNotify(session.MilkyWayBrightness);
            brightness.RegisterValueChangedCallback(evt => { session.SetMilkyWayBrightness(Mathf.Round(evt.newValue)); brightnessLabel.text = "Milky Way brightness: " + Mathf.RoundToInt(session.MilkyWayBrightness) + "%"; });
            menuScroll.Add(brightness);
            var saturationLabel = Label("Milky Way saturation: " + Mathf.RoundToInt(session.MilkyWaySaturation) + "%", 11, Text);
            menuScroll.Add(saturationLabel);
            var saturation = new Slider(10, 300) { name = "milky-way-saturation" };
            saturation.SetValueWithoutNotify(session.MilkyWaySaturation);
            saturation.RegisterValueChangedCallback(evt => { session.SetMilkyWaySaturation(Mathf.Round(evt.newValue)); saturationLabel.text = "Milky Way saturation: " + Mathf.RoundToInt(session.MilkyWaySaturation) + "%"; });
            menuScroll.Add(saturation);
            menuScroll.Add(Label("GRAPHICS · AURORA", 10, Muted, true));
            AddAuroraSlider("Simulated Kp index", "aurora-kp", session.AuroraKp, 0, 9, false, session.SetAuroraKp);
            AddAuroraSlider("Color boost", "aurora-color-boost", session.AuroraColorBoost, 50, 500, true, session.SetAuroraColorBoost);
            AddAuroraSlider("Intensity", "aurora-intensity", session.AuroraIntensity, 50, 500, true, session.SetAuroraIntensity);
            var auroraHelp = Note("Simulated magnetic activity, not live space weather. Higher Kp widens the polar oval toward lower latitudes. Color boost expands saturation and violet/red tones; intensity increases brightness. Enhanced reference look: 150% color / 300% intensity. Best viewed on Earth's night side.");
            auroraHelp.style.marginBottom = 16; menuScroll.Add(auroraHelp);
            menuScroll.Add(Label("FRAME RATE LIMIT", 10, Muted, true));
            var caps = Row(); caps.style.flexWrap = Wrap.Wrap;
            caps.style.marginTop = 7; caps.style.marginBottom = 12;
            foreach (int cap in Caps)
            {
                int selected = cap;
                var button = Button(cap == 0 ? "Unlimited" : cap.ToString(), "frame-cap-" + cap, () => session.SetFrameRateLimit(selected));
                button.style.width = 115; button.style.height = 35;
                button.style.fontSize = 11; button.style.marginRight = 6;
                button.style.marginBottom = 6;
                caps.Add(button); capButtons.Add(button);
            }
            menuScroll.Add(caps);
            vSync = Switch("VSync", "vsync-toggle", session.SetVSync);
            menuScroll.Add(vSync);
            settingsNote = Note(""); settingsNote.name = "frame-pacing-description";
            settingsNote.style.marginTop = 5; settingsNote.style.marginBottom = 17;
            menuScroll.Add(settingsNote);
            AddAction("Back", "settings-back", session.Back);
        }

        private void AddAuroraSlider(string title, string name, float value, float low, float high, bool percent, Action<float> change)
        {
            var label = Label("", 11, Text); label.style.marginTop = 8;
            Action<float> display = v => label.text = title + ": " + (percent ? Mathf.RoundToInt(v) + "%" : v.ToString("F1"));
            display(value); menuScroll.Add(label);
            var slider = new Slider(low, high) { name = name };
            slider.style.height = 26;
            slider.SetValueWithoutNotify(value);
            slider.RegisterValueChangedCallback(evt => { float v = percent ? Mathf.Round(evt.newValue) : Mathf.Round(evt.newValue * 10) / 10; change(v); display(v); });
            slider.RegisterCallback<PointerUpEvent>(_ => PlayerPrefs.Save());
            slider.RegisterCallback<FocusOutEvent>(_ => PlayerPrefs.Save());
            menuScroll.Add(slider);
        }

        private void BuildTutorial(MissionSession session)
        {
            var introduction = Note("Welcome to ESP. Choose a launch site on the Earth map, then use the flight view to launch and steer your vehicle.");
            introduction.style.marginBottom = 14;
            menuScroll.Add(introduction);
            var controls = Note("M — switch between map and flight\nLAUNCH — start the mission\nW / S — pitch down / up\nA / D — turn left / right\nShift / Ctrl — increase / decrease throttle\nSpace — stage\nEsc — pause or go back\n\nYou can reopen this tutorial from Settings.");
            controls.name = "tutorial-controls";
            controls.style.marginBottom = 16;
            menuScroll.Add(controls);
            AddAction("Got it", "tutorial-done", session.CloseTutorial, true);
            AddAction("Skip tutorial", "tutorial-skip", session.CloseTutorial);
        }

        private void BuildCheats(MissionSession session)
        {
            cheatState = Note(""); cheatState.name = "cheats-flight-status";
            cheatState.style.color = Amber; cheatState.style.marginBottom = 17;
            menuScroll.Add(cheatState);
            var gravityHeading = Row(); gravityHeading.style.justifyContent = Justify.SpaceBetween;
            gravityHeading.Add(Label("GRAVITY", 10, Muted, true));
            gravityValue = Label("1.00×", 14, Cyan, true); gravityValue.name = "gravity-multiplier-value";
            gravityHeading.Add(gravityValue); menuScroll.Add(gravityHeading);
            gravitySlider = new Slider(0, 5) { name = "gravity-multiplier-slider" };
            gravitySlider.style.height = 30;
            gravitySlider.style.marginLeft = 0; gravitySlider.style.marginRight = 0;
            gravitySlider.tooltip = "Gravity multiplier, from zero to five times the normal model";
            gravitySlider.SetValueWithoutNotify((float)session.GravityMultiplier);
            gravitySlider.RegisterValueChangedCallback(evt => {
                session.SetGravityMultiplier(Math.Round(evt.newValue, 2));
                gravityValue.text = session.GravityMultiplier.ToString("F2") + "×";
            });
            menuScroll.Add(gravitySlider);
            var ends = Row(); ends.style.justifyContent = Justify.SpaceBetween;
            ends.Add(Label("0×", 9, Muted)); ends.Add(Label("5×", 9, Muted));
            ends.style.marginBottom = 13; menuScroll.Add(ends);
            infiniteFuel = Switch("Infinite propellant", "infinite-propellant-toggle", session.SetInfiniteFuel);
            menuScroll.Add(infiniteFuel);
            crashDestruction = Switch("Crash destruction", "crash-destruction-toggle", session.SetCrashDestruction);
            menuScroll.Add(crashDestruction);
            var limitation = Note("Crash destruction controls ground impact only. Individual-part and thermal failures are not modeled.");
            limitation.style.marginTop = 5; limitation.style.marginBottom = 16;
            menuScroll.Add(limitation);
            AddAction("Restore defaults", "reset-cheats", session.ResetCheats);
            AddAction("Back", "cheats-back", session.Back);
        }

        private void SyncValues(MissionSession session)
        {
            if (resumeButton != null) resumeButton.SetEnabled(session.HasSession);
            if (physicsVisible != null) physicsVisible.SetValueWithoutNotify(session.PhysicsVisible);
            if (vSync != null) vSync.SetValueWithoutNotify(session.VSync);
            if (infiniteFuel != null) infiniteFuel.SetValueWithoutNotify(session.InfiniteFuel);
            if (crashDestruction != null) crashDestruction.SetValueWithoutNotify(session.CrashDestructionEnabled);
            if (gravitySlider != null) gravitySlider.SetValueWithoutNotify((float)session.GravityMultiplier);
            if (gravityValue != null) gravityValue.text = session.GravityMultiplier.ToString("F2") + "×";
            for (int i = 0; i < capButtons.Count; i++) Select(capButtons[i], session.FrameRateLimit == Caps[i]);
            if (settingsNote != null) settingsNote.text = session.VSync
                ? "VSync follows display refresh. The selected frame cap is retained for when VSync is off."
                : "A fixed frame cap turns VSync off. Unlimited removes the application frame cap.";
            if (cheatState != null) cheatState.text = session.ModifiedFlight
                ? "MODIFIED FLIGHT · NOT VERIFIED\nRestoring defaults keeps this flag. Start a new mission to clear it."
                : "Changing these options marks the flight as modified and disables normal orbit verification.";
            var overrides = new List<string>();
            if (session.GravityMultiplier != 1) overrides.Add("g " + session.GravityMultiplier.ToString("F2") + "×");
            if (session.InfiniteFuel) overrides.Add("∞ PROPELLANT");
            if (!session.CrashDestructionEnabled) overrides.Add("IMPACT OFF");
            string modified = "MODIFIED · NOT VERIFIED" + (overrides.Count > 0 ? "  /  " + string.Join(" · ", overrides) : "");
            modifiedText.text = modified;
            modifiedText.style.display = session.ModifiedFlight ? DisplayStyle.Flex : DisplayStyle.None;
            impactText.style.display = session.FlightDestroyed ? DisplayStyle.Flex : DisplayStyle.None;
            impactRestart.style.display = session.FlightDestroyed ? DisplayStyle.Flex : DisplayStyle.None;
            modifiedBadge.tooltip = (session.FlightDestroyed ? "Vehicle destroyed by ground impact.\n" : "") +
                "Gravity multiplier: " + session.GravityMultiplier.ToString("F2") + "×\n" +
                "Infinite propellant: " + (session.InfiniteFuel ? "enabled" : "disabled") + "\n" +
                "Crash destruction: " + (session.CrashDestructionEnabled ? "enabled" : "disabled") +
                (session.ModifiedFlight ? "\nThis modified flight cannot receive normal orbit verification." : "");
            Border(modifiedBadge, session.FlightDestroyed ? new Color(.58f, .23f, .18f) : new Color(.49f, .34f, .15f));
            physicsState.text = (session.FlightDestroyed ? "IMPACT · VEHICLE DESTROYED" : "") +
                (session.FlightDestroyed && session.ModifiedFlight ? "\n" : "") + (session.ModifiedFlight ? modified : "");
            physicsState.style.color = session.FlightDestroyed ? new Color(.99f, .43f, .37f) : Amber;
            physicsState.style.display = session.ModifiedFlight || session.FlightDestroyed ? DisplayStyle.Flex : DisplayStyle.None;
            physicsText.text = session.PhysicsText ?? "— Launch to inspect forces";
            Resize();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) { Resize(); }
        private void BuildNerdPanel()
        {
            nerdPanel = new VisualElement { name = "nerd-stats-panel" };
            nerdPanel.style.position = Position.Absolute;
            nerdPanel.style.left = 12; nerdPanel.style.top = 120;
            nerdPanel.style.maxWidth = Length.Percent(90);
            nerdPanel.style.width = 270; nerdPanel.style.maxHeight = Length.Percent(42);
            nerdPanel.style.paddingLeft = 7; nerdPanel.style.paddingRight = 7;
            nerdPanel.style.paddingTop = 5; nerdPanel.style.paddingBottom = 5;
            nerdPanel.style.backgroundColor = Surface;
            Border(nerdPanel, Edge); Radius(nerdPanel, 5);
            nerdHeader = Row(); nerdHeader.name = "nerd-stats-drag-handle"; nerdHeader.Add(Label("STATS FOR NERDS  ·  DRAG", 9, Cyan, true));
            nerdHeader.RegisterCallback<PointerDownEvent>(BeginNerdDrag);
            nerdHeader.RegisterCallback<PointerMoveEvent>(MoveNerdDrag);
            nerdHeader.RegisterCallback<PointerUpEvent>(EndNerdDrag);
            nerdHeader.RegisterCallback<PointerCaptureOutEvent>(_ => draggingNerdPanel = false);
            nerdPanel.Add(nerdHeader);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = Length.Percent(100);
            scroll.style.flexShrink = 1;
            nerdText = Label("", 10, Text);
            nerdText.name = "nerd-stats-values";
            nerdText.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(nerdText); nerdPanel.Add(scroll); overlayRoot.Add(nerdPanel);
        }

        private void BeginNerdDrag(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            draggingNerdPanel = true;
            nerdPointerStart = evt.position;
            nerdPanelStart = new Vector2(nerdPanel.resolvedStyle.left, nerdPanel.resolvedStyle.top);
            nerdHeader.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }
        private void MoveNerdDrag(PointerMoveEvent evt)
        {
            if (!draggingNerdPanel || !nerdHeader.HasPointerCapture(evt.pointerId)) return;
            Vector2 delta = (Vector2)evt.position - nerdPointerStart;
            ClampNerdPanel(nerdPanelStart + delta);
            evt.StopPropagation();
        }
        private void ClampNerdPanel(Vector2 position)
        {
            if (nerdPanel == null || root == null || nerdPanel.resolvedStyle.display == DisplayStyle.None) return;
            float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
            float panelWidth = nerdPanel.resolvedStyle.width, panelHeight = nerdPanel.resolvedStyle.height;
            // Hidden and not-yet-laid-out elements report zero or nonfinite extents.
            // Keep the last position until the visible panel has usable geometry.
            if (!PositiveExtent(width) || !PositiveExtent(height) || !PositiveExtent(panelWidth) || !PositiveExtent(panelHeight) ||
                float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y)) return;
            nerdPanel.style.left = Mathf.Clamp(position.x, 0, Mathf.Max(0, width - panelWidth - 1));
            nerdPanel.style.top = Mathf.Clamp(position.y, 0, Mathf.Max(0, height - panelHeight - 1));
        }
        private static bool PositiveExtent(float value) => value > 0 && !float.IsInfinity(value);
        private void EndNerdDrag(PointerUpEvent evt)
        {
            draggingNerdPanel = false;
            if (nerdHeader.HasPointerCapture(evt.pointerId)) nerdHeader.ReleasePointer(evt.pointerId);
        }

        private void RefreshNerdStats()
        {
            if (!nerdVisible || Time.unscaledTime < nextNerdUpdate) return;
            nextNerdUpdate = Time.unscaledTime + .5f;
            var session = MissionSession.Instance;
            var driver = FindFirstObjectByType<RocketFlightDriver>();
            var simulation = driver != null ? driver.Simulation : null;
            string state = session == null ? "No session" : session.Page.ToString();
            if (simulation != null) state += " / stage " + (simulation.State.StageIndex + 1) + (simulation.IsDestroyed ? " / destroyed" : "");
            if (state != lastNerdState)
            {
                lastNerdState = state;
                nerdEvents.Enqueue(Time.realtimeSinceStartup.ToString("F1") + "s: " + state);
                while (nerdEvents.Count > 5) nerdEvents.Dequeue();
            }
            var k = Keyboard.current;
            string input = k == null || MissionSession.BlocksInput ? "none" :
                (k.wKey.isPressed ? "W " : "") + (k.sKey.isPressed ? "S " : "") +
                (k.aKey.isPressed ? "A " : "") + (k.dKey.isPressed ? "D " : "") +
                (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed ? "Shift " : "") +
                (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed ? "Ctrl " : "") +
                (k.spaceKey.isPressed ? "Space " : "");
            if (string.IsNullOrWhiteSpace(input)) input = "none";
            string flight = simulation == null ? "Flight: unavailable" :
                string.Format("Pitch {0:F1}°  Heading {1:F1}°\nThrottle {2:F0}%  Stage {3}", simulation.ActualPitchDegrees, simulation.ActualHeadingDegrees, driver.Throttle * 100, simulation.State.StageIndex + 1);
            float ms = Time.unscaledDeltaTime * 1000f;
            nerdText.text = string.Format("Sampled frame {0:F1} ms  ({1:F0} FPS)\nCPU hardware: {2}\nGPU hardware: {3}\nUnity allocated: {4} MB\n{5}\nKeys held: {6}\n\nRecent state changes:\n{7}",
                ms, ms > 0 ? 1000f / ms : 0, SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024), flight, input, string.Join("\n", nerdEvents));
        }
        private void Resize()
        {
            if (root == null) return;
            float height = root.resolvedStyle.height;
            float width = root.resolvedStyle.width;
            if (height <= 0 || float.IsNaN(height)) return;
            bool start = renderedPage == MissionMenuPage.Start;
            bool startAtLeft = start && width >= 900;
            if (modal != null && card != null)
            {
                modal.style.alignItems = startAtLeft ? Align.FlexStart : Align.Center;
                modal.style.backgroundColor = start ? new Color(.012f, .025f, .045f, .25f) : Backdrop;
                card.style.marginLeft = startAtLeft ? Mathf.Clamp(width * .055f, 24, 88) : 0;
                card.style.width = startAtLeft ? 370 : 430;
                card.style.backgroundColor = start ? new Color(.025f, .044f, .06f, .48f) : Surface;
                Border(card, start ? Color.clear : Edge);
            }
            if (menuScroll != null) menuScroll.style.maxHeight = Mathf.Max(150, height - 220);
            if (nerdPanel != null) ClampNerdPanel(new Vector2(nerdPanel.resolvedStyle.left, nerdPanel.resolvedStyle.top));
            if (physicsScroll != null) physicsScroll.style.maxHeight = Mathf.Clamp(height - 280, 120, 330);
        }

        private void OnDestroy()
        {
            if (root != null) root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (gameplay != null && root != null)
            {
                while (gameplay.childCount > 0) root.Add(gameplay[0]);
                gameplay.RemoveFromHierarchy();
            }
            overlayRoot?.RemoveFromHierarchy();
        }

        private Button AddAction(string text, string name, Action action, bool primary = false)
        {
            var button = Button(text, name, action, primary);
            button.style.marginBottom = 7;
            menuScroll.Add(button); return button;
        }
        private static Toggle Switch(string title, string name, Action<bool> change)
        {
            var toggle = new Toggle(title) { name = name };
            toggle.style.height = 38; toggle.style.marginTop = 4; toggle.style.marginBottom = 4;
            toggle.style.marginLeft = 0; toggle.style.marginRight = 0;
            toggle.style.color = Text; toggle.style.fontSize = 13;
            toggle.RegisterValueChangedCallback(evt => change(evt.newValue));
            var label = toggle.Q<Label>();
            if (label != null) { label.style.minWidth = 0; label.style.flexGrow = 1; }
            return toggle;
        }
        private static Button IconButton(string text, string name, Action click)
        {
            var button = Button(text, name, click);
            button.style.width = 23; button.style.height = 23;
            button.style.paddingLeft = 0; button.style.paddingRight = 0;
            button.style.marginLeft = 4; button.style.fontSize = 14;
            return button;
        }
        private static Button Button(string text, string name, Action click, bool primary = false)
        {
            var button = new Button(click) { name = name, text = text };
            button.style.height = 40; button.style.flexShrink = 0;
            button.style.marginLeft = 0; button.style.marginRight = 0;
            button.style.marginTop = 0; button.style.marginBottom = 0;
            button.style.paddingLeft = 14; button.style.paddingRight = 14;
            button.style.paddingTop = 0; button.style.paddingBottom = 0;
            button.style.fontSize = 13; button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.backgroundColor = primary ? Cyan : Raised;
            button.style.color = primary ? new Color(.025f, .08f, .10f) : Text;
            Border(button, primary ? Cyan : Edge); Radius(button, 4);
            return button;
        }
        private static void Select(Button button, bool selected)
        {
            button.style.color = selected ? Cyan : Text;
            button.style.backgroundColor = selected ? new Color(.075f, .17f, .20f) : Raised;
            Border(button, selected ? new Color(.24f, .51f, .55f) : Edge);
        }
        private static Label Note(string text)
        {
            var label = Label(text, 11, Muted);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
        private static Label Label(string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size; label.style.color = color;
            label.style.marginLeft = 0; label.style.marginRight = 0;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            return label;
        }
        private static VisualElement Row()
        {
            var element = new VisualElement(); element.style.flexDirection = FlexDirection.Row; return element;
        }
        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0; element.style.right = 0; element.style.top = 0; element.style.bottom = 0;
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
    }
}
