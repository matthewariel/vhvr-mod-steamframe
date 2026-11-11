using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Contents of the Steam Frame tab, in three pages picked with buttons along the top:
    ///  - Settings (VHVR rows, saved by VHVR's OK button), grouped by who each setting is for:
    ///    left column ALL HEADSETS (punch, crouch, prompts), right column STEAM FRAME (binding tweaks, detection),
    ///    and below that EXTRA (server passwords, map sharing);
    ///  - Button guide: what each button of the active controller does, read from its binding file, plus map and chest controls;
    ///  - Recenter &amp; height: a picture of VHVR's recenter pose, seated/standing settings, last measurement.
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
        private const float GuideHeight = 450f; // TopY + 10 down to -285, the bottom of VHVR's last row

        private static readonly string[] PageTitles = { "Settings", "Button guide", "Recenter & height" };
        private static int currentPage;
        private static Texture2D poseTexture;

        private MethodInfo createElement;
        private GameObject labelPrefab;
        private TMP_Text status, punchNote, serverNote, heightNote;
        private Toggle grip, trigger, thumb, thumbSensing;
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<TMP_Text> pageButtonTexts = new List<TMP_Text>();
        private float nextRefresh;

        internal void Build(MethodInfo createElement, GameObject labelPrefab, GameObject buttonPrefab)
        {
            this.createElement = createElement;
            this.labelPrefab = labelPrefab;

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
            var left = Label(page, new Vector2(-460f, TopY + 10f), new Vector2(290f, GuideHeight), 13f, TextAlignmentOptions.TopLeft, topLeftPivot: true);
            var right = Label(page, new Vector2(-160f, TopY + 10f), new Vector2(290f, GuideHeight), 13f, TextAlignmentOptions.TopLeft, topLeftPivot: true);
            var extra = Label(page, new Vector2(140f, TopY + 10f), new Vector2(320f, GuideHeight), 13f, TextAlignmentOptions.TopLeft, topLeftPivot: true);
            var profile = GuideProfile;
            // The buttons come from one controller's binding file, so this part is specific to that controller.
            string title = $"<size=15><b>{profile.DisplayName} buttons</b></size>  {Badge(ModuleScope.SteamFrame)}\n";
            try
            {
                var rows = BindingTable.Build(BindingsHooks.ReadCurrent(profile));
                left.text = title + BindingTable.Format(rows, "Left");
                right.text = "<size=15> </size>\n" + BindingTable.Format(rows, "Right");
            }
            catch (Exception e)
            {
                left.text = title + $"Couldn't read the {profile.DisplayName} binding file.";
                right.text = "";
                Plugin.Log.LogWarning($"FrameTab: button guide unavailable: {e.Message}");
            }
            // How the map and chests work is the same on every headset; only the button names come from the profile.
            extra.text =
                $"<size=15><b>Map and chests</b></size>  {Badge(ModuleScope.AllHeadsets)}\n" +
                "<b>Big map</b>  <size=85%>(laser on the map)</size>\n" +
                "Zoom: left D-pad up / down\n" +
                "Move: hold trigger and drag\n" +
                "Ping: right stick click\n" +
                "Add pin: grip + trigger\n" +
                "Remove pin: bumper\n" +
                "Cross out pin: trigger on it\n\n" +
                "<b>Chests</b>\n" +
                "Move an item: grip + trigger on it\n" +
                "Place stacks: right stick click\n" +
                "Take all: left stick click\n\n" +
                "<b>Recenter</b>: hands to eyes for 3 s\n" +
                "<size=85%>(see Recenter & height)</size>\n\n" +
                "<b>Wrist map angle</b>: VRHUD tab >\n" +
                "RightWrist > Set (in game)";
        }

        private void BuildHeight(Transform page)
        {
            var picture = new GameObject("RecenterPose", typeof(RectTransform));
            picture.layer = gameObject.layer;
            var rect = (RectTransform)picture.transform;
            rect.SetParent(page, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(352f, 198f);
            rect.anchoredPosition = new Vector2(-300f, TopY - 90f);
            var image = picture.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = PoseTexture();
            if (image.texture == null) picture.SetActive(false);

            var howTo = Label(page, new Vector2(-300f, TopY - 297f), new Vector2(360f, 210f), 13f, TextAlignmentOptions.Top);
            howTo.text =
                "<b>Recenter pose</b>\n" +
                "Hold both controllers about 10 cm in front of your eyes, one in front of each eye, " +
                "palms facing you, like holding binoculars. Don't press trigger or grip. Hold still for 3 seconds.\n\n" +
                "Standing: VHVR measures your height and it's remembered as your standing height.\n" +
                "Seated: you keep your standing height in game.\n" +
                "Holding the Steam button to recenter does the same when the option on the right is on.";

            float y = TopY;
            Header(page, "<b>Seated / standing</b>", ModuleScope.AllHeadsets, HeightX, ref y);
            Row(page, "Posture", PostureHooks.Mode, HeightX, ref y);
            Row(page, "Standing eye height (m)", PostureHooks.StandingEyeHeight, HeightX, ref y);
            Row(page, "Seated eye height (m)", PostureHooks.SeatedEyeHeight, HeightX, ref y);
            Row(page, "Steam button recenter re-measures", PostureHooks.RecalibrateOnSteamVRRecenter, HeightX, ref y);
            heightNote = Label(page, new Vector2(HeightX - 30f, y - 14f), new Vector2(420f, 64f), 13f, TextAlignmentOptions.Center);

            var wrist = Label(page, new Vector2(HeightX - 30f, TopY - 340f), new Vector2(420f, 70f), 13f, TextAlignmentOptions.Center);
            wrist.text = "<b>Wrist minimap angle</b>: VRHUD tab > RightWrist > Set (in game). " +
                         "Hold the left trigger and move or tilt the panel, then press Jump to save.";
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

            if (heightNote != null)
            {
                string learned = $"Split at {PostureHooks.Threshold:F2} m: below counts as seated.";
                heightNote.text = float.IsNaN(PostureHooks.LastMeasured)
                    ? "No recenter yet this session.\n" + learned
                    : $"Last recenter measured {PostureHooks.LastMeasured:F2} m: <b>{PostureHooks.LastPosture.ToString().ToLowerInvariant()}</b>.\n" + learned;
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
            var go = Instantiate(buttonPrefab, transform);
            go.name = "PageButton " + PageTitles[index];
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(190f, 30f);
            rect.anchoredPosition = new Vector2((index - (PageTitles.Length - 1) / 2f) * 205f, PageButtonY);
            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.richText = true;
                text.enableAutoSizing = false;
                text.fontSize = 15f;
            }
            pageButtonTexts.Add(text);
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowPage(index));
            button.enabled = true;
            button.interactable = true;
        }

        private Toggle Row(Transform parent, string label, ConfigEntryBase entry, float x, ref float y)
        {
            if (entry == null) return null;
            int before = parent.childCount;
            bool created = (bool)createElement.Invoke(null, new object[]
            {
                new KeyValuePair<string, ConfigEntryBase>(label, entry), parent, new Vector2(x, y), "Steam Frame",
            });
            if (!created) return null;
            y -= RowStep;
            return parent.childCount > before ? parent.GetChild(parent.childCount - 1).GetComponent<Toggle>() : null;
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

        private static Texture2D PoseTexture()
        {
            if (poseTexture != null) return poseTexture;
            try
            {
                using (var stream = typeof(FrameTabView).Assembly.GetManifestResourceStream("VhvrSteamFrame.recenter_pose.png"))
                {
                    if (stream == null) throw new FileNotFoundException("embedded recenter_pose.png missing");
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "VhvrSteamFrame recenter pose" };
                    if (!ImageConversion.LoadImage(tex, bytes)) throw new InvalidDataException("not a PNG");
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    poseTexture = tex;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"FrameTab: recenter picture unavailable: {e.Message}");
            }
            return poseTexture;
        }
    }
}
