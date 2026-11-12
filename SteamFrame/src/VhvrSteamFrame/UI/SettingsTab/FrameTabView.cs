using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valve.Newtonsoft.Json.Linq;
using L = VhvrSteamFrame.FrameGuideLayout;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Contents of the Steam Frame tab, in three pages picked with buttons along the top:
    ///  - Settings (VHVR rows, saved by VHVR's OK button), grouped by who each setting is for:
    ///    left column ALL HEADSETS (punch, crouch, prompts), right column STEAM FRAME (binding tweaks, detection),
    ///    and below that EXTRA (server passwords, map sharing);
    ///  - Button guide: both Steam Frame controllers drawn with a line from each button to a box saying what it does (read
    ///    live from the binding file). Clicking a box picks a new action for that button; Reset to default clears them all;
    ///  - Height: a picture of VHVR's recenter pose, seated/standing settings, Set standing / Set seated buttons, last measurement.
    /// The page buttons read "I Settings", "II Button guide", "III Height".
    /// Every section header carries a coloured badge with its scope; a legend under the status line explains them.
    /// Positions follow VHVR's layout: rows 30 px apart, a row is a ~200 px label left of a ~150 px control at its x.
    /// </summary>
    internal class FrameTabView : MonoBehaviour
    {
        private const float RowStep = 30f;
        private const float LeftX = -250f;
        private const float RightX = 250f;
        private const float HeightX = 170f;
        // Top to bottom: page buttons, status line, scope legend, then the page itself (first row at TopY). VHVR's own
        // tab buttons cover the panel above about y = 255 (its rows run from 250 down to -270), so everything starts
        // below that; at 270 the page buttons were hidden behind the tab row.
        private const float PageButtonY = 232f;
        private const float StatusY = 203f;
        private const float LegendY = 182f;
        private const float TopY = 155f;
        // The panel's inside edges, measured from headset screenshots (2026-11-10): about x = ±450 and y = -252 at the
        // bottom. Everything stays inside these, with a small margin.
        private const float EdgeX = 440f;
        private const float BottomY = -240f;
        private const float GuideHeight = TopY + 10f - BottomY;
        private const float GuideColumnWidth = 280f;
        private const float GuideFontSize = 12f;

        // Page buttons: a Roman numeral and what the page is for.
        private static readonly string[] PageTitles = { "I  Settings", "II  Button guide", "III  Height" };
        private static int currentPage;
        private static Texture2D poseTexture, controllersTexture;

        private MethodInfo createElement;
        private GameObject labelPrefab;
        private TMP_Text status, punchNote, serverNote, heightNote, setResult;
        private Toggle grip, trigger, thumb, thumbSensing;
        private Slider standingSlider, seatedSlider;
        private GameObject buttonPrefab;
        private static bool loggedButtonTexts;
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<TMP_Text> pageButtonTexts = new List<TMP_Text>();
        private float nextRefresh;

        // Button guide state: the box whose picker is open (null = the picture shows), why the last pick was refused,
        // whether Reset was clicked once, and a one-off note for the status line under the right column.
        private Transform guidePage;
        private string pickingId, pickerMessage, guideMessage;
        private bool resetArmed;
        private TMP_Text guideNote;

        internal void Build(MethodInfo createElement, GameObject labelPrefab, GameObject buttonPrefab)
        {
            this.createElement = createElement;
            this.labelPrefab = labelPrefab;
            this.buttonPrefab = buttonPrefab;

            status = Label(transform, new Vector2(0f, StatusY), new Vector2(900f, 24f), 14f, TextAlignmentOptions.Center);
            var legend = Label(transform, new Vector2(0f, LegendY), new Vector2(940f, 18f), 12f, TextAlignmentOptions.Center);
            legend.text = $"{Badge(ModuleScope.AllHeadsets)} = any VR controller (tested on Steam Frame)   |   " +
                          $"{Badge(ModuleScope.SteamFrame)} = Frame only   |   {Badge(ModuleScope.Extra)} = not VR";

            for (int i = 0; i < PageTitles.Length; i++) pages.Add(NewPage(PageTitles[i]));
            BuildSettings(pages[0].transform);
            BuildGuide(pages[1].transform);
            BuildHeight(pages[2].transform);

            if (buttonPrefab != null)
            {
                for (int i = 0; i < PageTitles.Length; i++) PageButton(buttonPrefab, i);
            }
            else
            {
                currentPage = 0;
                Plugin.Log.LogWarning("FrameTab: no button prefab, showing the Settings page only.");
            }
            ShowPage(currentPage);

            foreach (var t in new[] { grip, trigger, thumb, thumbSensing })
                if (t != null) t.onValueChanged.AddListener(_ => Refresh());
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        /// <summary>The controller whose buttons the guide shows: the one in use, else the forced (Steam Frame) profile.</summary>
        private static ControllerProfile GuideProfile => ControllerDetector.ActiveProfile ?? ControllerDetector.ForcedProfile;

        // ---- Pages ----

        private void BuildSettings(Transform page)
        {
            // Left column: works with any VR controller.
            float y = TopY;
            Header(page, "<b>Punch</b>  <size=80%>what a bare hand needs held</size>", ModuleScope.AllHeadsets, LeftX, ref y);
            grip = Row(page, "Require grip", PunchHooks.RequireGrip, LeftX, ref y);
            trigger = Row(page, "Require trigger", PunchHooks.RequireTrigger, LeftX, ref y);
            thumb = Row(page, "Require thumb on stick", PunchHooks.RequireThumbOnStick, LeftX, ref y);
            punchNote = Label(page, new Vector2(LeftX - 30f, y - 2f), new Vector2(420f, 36f), 13f, TextAlignmentOptions.Center);
            y -= 40f;

            Header(page, "<b>Crouch</b>", ModuleScope.AllHeadsets, LeftX, ref y);
            Row(page, "Ignore crouch in menus and map", CrouchHooks.BlockInMenus, LeftX, ref y);
            Row(page, "Double-click to crouch", CrouchHooks.DoubleClick, LeftX, ref y);

            Header(page, "<b>Prompts</b>", ModuleScope.AllHeadsets, LeftX, ref y);
            Row(page, "Button labels", PromptsHooks.ButtonLabels, LeftX, ref y);
            Row(page, "Map hints show VR buttons", PromptsHooks.MapHints, LeftX, ref y);

            // Right column: Steam Frame only...
            y = TopY;
            Header(page, "<b>Frame bindings</b>  <size=80%>restart to apply</size>", ModuleScope.SteamFrame, RightX, ref y);
            Row(page, "Apply binding tweaks", BindingsHooks.Manage, RightX, ref y);
            thumbSensing = Row(page, "Thumb touch sensing", BindingsHooks.ThumbTouchSensing, RightX, ref y);
            Row(page, "Left stick click: sprint", BindingsHooks.SprintToggleOnLeftStickClick, RightX, ref y);
            Row(page, "Right stick click: crouch", BindingsHooks.CrouchOnRightStickClick, RightX, ref y);
            Row(page, "No stick flick sprint/crouch", BindingsHooks.NoStickFlickRunCrouch, RightX, ref y);
            Row(page, "Steam Frame detection", Plugin.FrameDetection, RightX, ref y);

            // ...and below it, the extras that have nothing to do with VR.
            y -= 10f;
            Header(page, "<b>Servers and map</b>", ModuleScope.Extra, RightX, ref y);
            Row(page, "Remember server passwords", ServerPasswordsHooks.Remember, RightX, ref y);
            Row(page, "Forget saved passwords", ServerPasswordsHooks.ForgetAll, RightX, ref y);
            Row(page, "Share map position on join", MapSharingHooks.SharePositionOnJoin, RightX, ref y);
            serverNote = Label(page, new Vector2(RightX - 30f, y + 4f), new Vector2(420f, 22f), 12f, TextAlignmentOptions.Center);
        }

        private void BuildGuide(Transform page)
        {
            guidePage = page;
            guideNote = null;
            var profile = GuideProfile;
            if (profile != SteamFrameProfile.Instance)
            {
                BuildGuideList(page, profile);
                return;
            }

            JObject current, defaults;
            try
            {
                current = BindingsHooks.ReadCurrent(profile);
                defaults = BindingsHooks.DefaultDocument();
            }
            catch (Exception e)
            {
                var error = Label(page, new Vector2(0f, 0f), new Vector2(600f, 40f), 14f, TextAlignmentOptions.Center);
                error.text = $"Couldn't read the {profile.DisplayName} binding file.";
                Plugin.Log.LogWarning($"FrameTab: button guide unavailable: {e.Message}");
                return;
            }

            for (int i = 0; i < FrameButtons.Left.Count; i++) ButtonBox(page, FrameButtons.Left[i], -1f, i, current, defaults);
            for (int i = 0; i < FrameButtons.Right.Count; i++) ButtonBox(page, FrameButtons.Right[i], 1f, i, current, defaults);

            if (pickingId != null && buttonPrefab != null) BuildPicker(page, current);
            else BuildPicture(page, current);

            if (buttonPrefab != null)
            {
                var reset = Button(page, buttonPrefab, "Button Reset to default", new Vector2(-L.ColumnCentreX, L.ResetY),
                    new Vector2(L.BoxWidth, L.ResetHeight), 14f, OnResetClicked);
                reset.text = resetArmed ? "<color=#ffb347><b>Click again to reset</b></color>" : "Reset to default";
            }
            guideNote = Label(page, new Vector2(L.ColumnCentreX, L.ResetY), new Vector2(L.BoxWidth, L.NoteHeight), L.NoteSize, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// One button's box: its name, then what it does now. Boxes of buttons that can be changed are buttons that open
        /// the picker; the trigger, grip and stick movement only show what they do. A changed button's name is orange.
        /// </summary>
        private void ButtonBox(Transform page, FrameButton b, float side, int index, JObject current, JObject defaults)
        {
            var pos = new Vector2(side * L.ColumnCentreX, L.BoxY(index));
            var size = new Vector2(L.BoxWidth, L.BoxHeight);
            string action = FrameButtons.Describe(current, b);
            bool changed = action != FrameButtons.Describe(defaults, b);
            bool picking = b.Id == pickingId;
            string name = b.Remappable ? b.Label : b.Label + " (fixed)";
            string nameColour = picking ? "#ffcc66" : changed ? "#ffb347" : "#b4b4b4";
            string text = $"<size={L.BoxNameSize}><color={nameColour}>{name}{(changed ? "  - changed" : "")}</color></size>\n" +
                          (picking ? $"<color=#ffcc66><b>{action}</b></color>" : action);

            if (b.Remappable && buttonPrefab != null)
            {
                string id = b.Id;
                Button(page, buttonPrefab, "Box " + id, pos, size, L.BoxActionSize, () => StartPick(id)).text = text;
            }
            else
            {
                Label(page, pos, size, L.BoxActionSize, TextAlignmentOptions.Center).text = $"<color=#a8a8a8>{text}</color>";
            }
        }

        /// <summary>Both controllers with their leader lines, and the two-button combinations between their handles.</summary>
        private void BuildPicture(Transform page, JObject current)
        {
            var texture = EmbeddedTexture("VhvrSteamFrame.frame_controllers.png", ref controllersTexture);
            if (texture != null)
            {
                var picture = new GameObject("FrameControllers", typeof(RectTransform));
                picture.layer = gameObject.layer;
                var rect = (RectTransform)picture.transform;
                rect.SetParent(page, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(L.PictureWidth, L.PictureHeight);
                rect.anchoredPosition = new Vector2(0f, L.PictureCentreY);
                var image = picture.AddComponent<RawImage>();
                image.raycastTarget = false;
                image.texture = texture;
            }

            var lines = FrameButtons.ChordLines(current);
            var notes = Label(page, new Vector2(-L.NotesWidth / 2f, L.NotesTopY), new Vector2(L.NotesWidth, L.NotesTopY - L.BottomY),
                L.NotesSize, TextAlignmentOptions.Top, topLeftPivot: true);
            notes.text = "<color=#b4b4b4>" + string.Join("\n", lines.Select(l =>
            {
                int colon = l.IndexOf(": ", StringComparison.Ordinal);
                return colon < 0 ? l : "<b>" + l.Substring(0, colon) + "</b>" + l.Substring(colon);
            })) + "</color>";
        }

        /// <summary>The actions the chosen button can have, 3 columns, the current one highlighted, then Cancel.</summary>
        private void BuildPicker(Transform page, JObject current)
        {
            var b = FrameButtons.ById(pickingId);
            if (b == null) { pickingId = null; BuildPicture(page, current); return; }

            var title = Label(page, new Vector2(0f, L.PickerTitleY), new Vector2(L.PictureWidth - 20f, 28f), 16f, TextAlignmentOptions.Center);
            title.text = $"<b>{(b.Hand == "left" ? "Left" : "Right")} {b.Label}</b>: pick what it does";

            var now = FrameButtons.ActionsOn(current, b);
            for (int i = 0; i < FrameButtons.Choices.Count; i++)
            {
                var choice = FrameButtons.Choices[i];
                bool selected = choice.Action == FrameButtons.Nothing ? now.Count == 0 : now.Contains(choice.Action);
                var label = Button(page, buttonPrefab, "Choice " + choice.Action, new Vector2(L.PickerX(i), L.PickerY(i)),
                    new Vector2(L.PickerButtonWidth, L.PickerButtonHeight), 13f, () => Pick(choice.Action));
                label.text = selected ? $"<color=#ffcc66><b>{choice.Label}</b></color>" : choice.Label;
            }

            Button(page, buttonPrefab, "Button Cancel", new Vector2(0f, L.CancelY), new Vector2(L.PickerButtonWidth, L.PickerButtonHeight), 14f,
                () => { pickingId = null; pickerMessage = null; RebuildGuide(); }).text = "Cancel";

            var message = Label(page, new Vector2(0f, L.PickerMessageY), new Vector2(L.PickerMessageWidth, L.PickerMessageHeight), 12f, TextAlignmentOptions.Center);
            message.text = pickerMessage == null
                ? "<color=#b4b4b4>Takes effect after restarting Valheim.</color>"
                : $"<color=#ffb347>{pickerMessage}</color>";
        }

        private void StartPick(string id)
        {
            pickingId = pickingId == id ? null : id; // clicking the open box again closes the picker
            pickerMessage = null;
            resetArmed = false;
            guideMessage = null;
            RebuildGuide();
        }

        private void Pick(string action)
        {
            string refusal = BindingsHooks.SetRemap(pickingId, action);
            if (refusal != null)
            {
                pickerMessage = refusal;
            }
            else
            {
                pickingId = null;
                pickerMessage = null;
            }
            RebuildGuide();
        }

        /// <summary>Reset needs two clicks, so a stray laser click can't undo every change.</summary>
        private void OnResetClicked()
        {
            pickingId = null;
            pickerMessage = null;
            if (BindingsHooks.RemapCount == 0)
            {
                resetArmed = false;
                guideMessage = "Nothing to reset: every button does what it does by default.";
            }
            else if (!resetArmed)
            {
                resetArmed = true;
                guideMessage = null;
            }
            else
            {
                BindingsHooks.ResetRemaps();
                resetArmed = false;
                guideMessage = "Buttons reset to default.";
            }
            RebuildGuide();
        }

        private void RebuildGuide()
        {
            if (guidePage == null) return;
            for (int i = guidePage.childCount - 1; i >= 0; i--)
            {
                var child = guidePage.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            BuildGuide(guidePage);
            Refresh();
        }

        /// <summary>A controller with no picture yet: what each button does, as two lists.</summary>
        private void BuildGuideList(Transform page, ControllerProfile profile)
        {
            var left = Label(page, new Vector2(-EdgeX, TopY + 10f), new Vector2(GuideColumnWidth, GuideHeight), GuideFontSize, TextAlignmentOptions.TopLeft, topLeftPivot: true);
            var right = Label(page, new Vector2(-EdgeX + GuideColumnWidth + 10f, TopY + 10f), new Vector2(GuideColumnWidth, GuideHeight), GuideFontSize, TextAlignmentOptions.TopLeft, topLeftPivot: true);
            string title = $"<size=15><b>{profile.DisplayName} buttons</b></size>\n";
            try
            {
                var rows = BindingTable.Build(BindingsHooks.ReadCurrent(profile));
                left.text = title + BindingTable.Format(rows, "Left");
                right.text = "<size=15> </size>\n" + BindingTable.Format(rows, "Right");
            }
            catch (Exception e)
            {
                left.text = title + $"Couldn't read the {profile.DisplayName} binding file.";
                Plugin.Log.LogWarning($"FrameTab: button guide unavailable: {e.Message}");
            }
        }

        private void BuildHeight(Transform page)
        {
            var picture = new GameObject("RecenterPose", typeof(RectTransform));
            picture.layer = gameObject.layer;
            var rect = (RectTransform)picture.transform;
            rect.SetParent(page, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            // Left half (-EdgeX to about -30): the recenter picture, and how recentering and the heights work under it.
            const float leftCentre = -235f;
            rect.sizeDelta = new Vector2(352f, 198f);
            rect.anchoredPosition = new Vector2(leftCentre, TopY - 90f);
            var image = picture.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = EmbeddedTexture("VhvrSteamFrame.recenter_pose.png", ref poseTexture);
            if (image.texture == null) picture.SetActive(false);

            var howTo = Label(page, new Vector2(leftCentre, -140f), new Vector2(400f, 200f), 12f, TextAlignmentOptions.Top);
            howTo.text =
                "<b>Recenter pose</b>: hold both controllers about 10 cm in front of your eyes, palms facing you, like " +
                "binoculars. Don't press trigger or grip; hold still for 3 seconds. Holding the Steam button recenters too " +
                "(when the option on the right is on).\n\n" +
                "<b>Seated / standing</b>: set both heights once with the buttons on the right. After that you stay at " +
                "your standing height in game whether you sit or stand: it switches about a second after you sit down " +
                "or stand up, and each recenter checks again. Recentering never changes the heights you set.";

            // Right half (about -70 to EdgeX): the settings, the two Set buttons and what's set now.
            float y = TopY;
            Header(page, "<b>Seated / standing</b>", ModuleScope.AllHeadsets, HeightX, ref y);
            Row(page, "Posture", PostureHooks.Mode, HeightX, ref y);
            standingSlider = RowObject(page, "Standing eye height (m)", PostureHooks.StandingEyeHeight, HeightX, ref y)?.GetComponentInChildren<Slider>(true);
            seatedSlider = RowObject(page, "Seated eye height (m)", PostureHooks.SeatedEyeHeight, HeightX, ref y)?.GetComponentInChildren<Slider>(true);
            Row(page, "Switch when you sit or stand", PostureHooks.FollowHead, HeightX, ref y);
            Row(page, "Steam button recenter re-measures", PostureHooks.RecalibrateOnSteamVRRecenter, HeightX, ref y);

            const float buttonsY = -30f, rightCentre = 150f;
            ActionButton(page, "Set standing height", new Vector2(rightCentre - 110f, buttonsY), () => SetHeight(Posture.Standing));
            ActionButton(page, "Set seated height", new Vector2(rightCentre + 110f, buttonsY), () => SetHeight(Posture.Seated));
            var setHow = Label(page, new Vector2(rightCentre, buttonsY - 30f), new Vector2(440f, 22f), 12f, TextAlignmentOptions.Center);
            setHow.text = "Stand up straight (or sit as you play), look ahead, then press. Saved straight away.";
            setResult = Label(page, new Vector2(rightCentre, buttonsY - 54f), new Vector2(440f, 22f), 12f, TextAlignmentOptions.Center);
            heightNote = Label(page, new Vector2(rightCentre, buttonsY - 96f), new Vector2(440f, 52f), 12f, TextAlignmentOptions.Center);

            var wrist = Label(page, new Vector2(rightCentre, BottomY + 35f), new Vector2(440f, 50f), 12f, TextAlignmentOptions.Center);
            wrist.text = "<b>Wrist minimap angle</b>: VRHUD tab > RightWrist > Set (in game). " +
                         "Hold the left trigger and move or tilt the panel, then press Jump to save.";
        }

        private void SetHeight(Posture which)
        {
            string message = PostureHooks.SetHeightFromHeadset(which, out float saved);
            // VHVR's OK button writes every slider back to the settings, so both sliders must show the saved heights
            // (setting one can move the other too, when SteamVR's floor moved since it was set).
            if (!float.IsNaN(saved))
            {
                if (standingSlider != null) standingSlider.value = PostureHooks.StandingEyeHeight.Value;
                if (seatedSlider != null) seatedSlider.value = PostureHooks.SeatedEyeHeight.Value;
            }
            if (setResult != null)
                setResult.text = float.IsNaN(saved) ? $"<color=#ffb347>{message}</color>" : $"<color=#9be29b>{message}</color>";
            Refresh();
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Count) index = 0;
            currentPage = index;
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == index);
            for (int i = 0; i < pageButtonTexts.Count; i++)
                if (pageButtonTexts[i] != null)
                    pageButtonTexts[i].text = i == index ? $"<color=#ffcc66><b>{PageTitles[i]}</b></color>" : PageTitles[i];
        }

        // ---- Live text ----

        private void Refresh()
        {
            if (status != null)
            {
                string detection;
                switch (ControllerDetector.State)
                {
                    case ControllerDetector.Result.Recognized: detection = $"<color=#9be29b>{ControllerDetector.DetectedProfile?.DisplayName} controllers detected</color>"; break;
                    case ControllerDetector.Result.Unrecognized: detection = $"<color=#ffb347>Steam Frame not detected</color> ({ControllerDetector.LeftType ?? "?"} / {ControllerDetector.RightType ?? "?"})"; break;
                    default: detection = "Looking for controllers..."; break;
                }
                status.text = detection + (BindingsHooks.RestartNeeded ? "   <color=#ffb347>Frame bindings changed: restart Valheim</color>" : "");
            }

            if (punchNote != null)
            {
                var r = new PunchRequirements
                {
                    Grip = grip != null ? grip.isOn : PunchHooks.Requirements.Grip,
                    Trigger = trigger != null ? trigger.isOn : PunchHooks.Requirements.Trigger,
                    ThumbOnStick = thumb != null ? thumb.isOn : PunchHooks.Requirements.ThumbOnStick,
                };
                string warning = PunchPolicy.Warning(r);
                bool sensing = thumbSensing != null ? thumbSensing.isOn : BindingsHooks.ThumbTouchSensingEnabled;
                if (r.ThumbOnStick && !sensing)
                    warning = (warning == null ? "" : warning + " ") + "Thumb sensing is off, so 'thumb on stick' is ignored.";
                punchNote.text = warning == null
                    ? $"<color=#9be29b>Punch needs {PunchHooks.Describe(r)}.</color>"
                    : $"<color=#ffb347>{warning}</color>";
            }

            if (serverNote != null)
            {
                int saved = ServerPasswordsHooks.SavedCount;
                serverNote.text = saved == 0 ? "No saved passwords." : saved == 1 ? "1 server password saved." : $"{saved} server passwords saved.";
            }

            if (guideNote != null)
            {
                int changed = BindingsHooks.RemapCount;
                string note;
                if (BindingsHooks.Manage != null && !BindingsHooks.Manage.Value)
                    note = "<color=#ffb347>Binding tweaks are off (page I), so changed buttons aren't used.</color>";
                else if (guideMessage != null)
                    note = guideMessage;
                else if (BindingsHooks.RestartNeeded)
                    note = "<color=#ffb347>Restart Valheim to use the changes.</color>";
                else if (changed > 0)
                    note = changed == 1 ? "1 button changed. Click a box to change it." : $"{changed} buttons changed. Click a box to change it.";
                else
                    note = "Click a box to change that button.";
                guideNote.text = note;
            }

            if (heightNote != null)
            {
                // Today's heights: the saved ones, moved with SteamVR's floor if it moved since they were set.
                float standing = PostureHooks.EffectiveStanding, seated = PostureHooks.EffectiveSeated;
                string set = $"Standing {PostureHooks.Metres(standing)}, seated {PostureHooks.Metres(seated)}.";
                string state = PostureCalibrator.GapWarning(standing, seated) is string gap
                    ? $"<color=#ffb347>{gap}</color>"
                    : !PostureHooks.Ready
                        ? "<color=#ffb347>Set both heights to play seated.</color>"
                        : float.IsNaN(PostureHooks.LastMeasured)
                            ? "No recenter yet this session."
                            : $"Now: <b>{PostureHooks.CurrentPosture.ToString().ToLowerInvariant()}</b>.";
                heightNote.text = set + "\n" + state;
            }
        }

        // ---- Building blocks ----

        private GameObject NewPage(string name)
        {
            var go = new GameObject("Page " + name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }

        private void PageButton(GameObject buttonPrefab, int index)
        {
            var text = Button(transform, buttonPrefab, "PageButton " + PageTitles[index],
                new Vector2((index - (PageTitles.Length - 1) / 2f) * 215f, PageButtonY), new Vector2(200f, 30f), 15f, () => ShowPage(index));
            pageButtonTexts.Add(text);
        }

        private void ActionButton(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            if (buttonPrefab == null) return;
            var text = Button(parent, buttonPrefab, "Button " + title, pos, new Vector2(200f, 32f), 14f, onClick);
            if (text != null) text.text = title;
        }

        /// <summary>
        /// A copy of VHVR's "Set" button with our own label. The copy's own text didn't show (blank page buttons in the
        /// 0.3.2 headset test), so its texts are switched off and a label like the rest of the tab's goes on top.
        /// </summary>
        private TMP_Text Button(Transform parent, GameObject prefab, string name, Vector2 pos, Vector2 size, float fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = Instantiate(prefab, parent);
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var own = go.GetComponentsInChildren<TMP_Text>(true);
            if (!loggedButtonTexts)
            {
                loggedButtonTexts = true;
                Plugin.Log.LogInfo($"FrameTab: button prefab texts: {string.Join(", ", Array.ConvertAll(own, t => $"{t.gameObject.name} \"{t.text}\" ({(t.gameObject.activeSelf ? "on" : "off")})"))}.");
            }
            foreach (var t in own) t.enabled = false;

            var label = Label(go.transform, Vector2.zero, size, fontSize, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            button.enabled = true;
            button.interactable = true;
            return label;
        }

        private Toggle Row(Transform parent, string label, ConfigEntryBase entry, float x, ref float y)
        {
            return RowObject(parent, label, entry, x, ref y)?.GetComponent<Toggle>();
        }

        /// <summary>One VHVR settings row (toggle, slider or chooser); returns the row's object, or null.</summary>
        private GameObject RowObject(Transform parent, string label, ConfigEntryBase entry, float x, ref float y)
        {
            if (entry == null) return null;
            int before = parent.childCount;
            bool created = (bool)createElement.Invoke(null, new object[]
            {
                new KeyValuePair<string, ConfigEntryBase>(label, entry), parent, new Vector2(x, y), "Steam Frame",
            });
            if (!created) return null;
            y -= RowStep;
            return parent.childCount > before ? parent.GetChild(parent.childCount - 1).gameObject : null;
        }

        private void Header(Transform parent, string text, ModuleScope scope, float columnX, ref float y)
        {
            var label = Label(parent, new Vector2(columnX - 30f, y), new Vector2(420f, 28f), 17f, TextAlignmentOptions.Center);
            label.text = text + "  " + Badge(scope);
            y -= RowStep;
        }

        /// <summary>A small coloured tag saying who a section is for. Never below 12 pt: it has to be readable in the headset.</summary>
        private static string Badge(ModuleScope scope)
        {
            switch (scope)
            {
                case ModuleScope.SteamFrame: return "<size=12><color=#ffb347><b>STEAM FRAME</b></color></size>";
                case ModuleScope.Extra: return "<size=12><color=#bbbbbb><b>EXTRA</b></color></size>";
                default: return "<size=12><color=#8fc7ff><b>ALL HEADSETS</b></color></size>";
            }
        }

        private TMP_Text Label(Transform parent, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions alignment, bool topLeftPivot = false)
        {
            var text = Instantiate(labelPrefab, parent).GetComponent<TMP_Text>();
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = topLeftPivot ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.richText = true;
            text.text = "";
            return text;
        }

        /// <summary>A PNG built into the plugin, loaded once; null (and a log line) if it can't be read.</summary>
        private static Texture2D EmbeddedTexture(string resource, ref Texture2D cache)
        {
            if (cache != null) return cache;
            try
            {
                using (var stream = typeof(FrameTabView).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new FileNotFoundException("embedded " + resource + " missing");
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "VhvrSteamFrame " + resource };
                    if (!ImageConversion.LoadImage(tex, bytes)) throw new InvalidDataException("not a PNG");
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    cache = tex;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"FrameTab: picture {resource} unavailable: {e.Message}");
            }
            return cache;
        }
    }
}
