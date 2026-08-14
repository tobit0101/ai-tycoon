using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using AITycoon.Quests;

namespace AITycoon.Settings.Editor
{
    public static class SettingsPanelBuilder
    {
        [MenuItem("AI Tycoon/Rebuild Settings Panel")]
        public static void BuildSettingsPanel()
        {
            var hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas == null) { Debug.LogError("HUD_Canvas not found!"); return; }

            var settingsButton = GameObject.Find("Settings");
            if (settingsButton == null) { Debug.LogError("Settings GameObject not found!"); return; }

            // Remove old overlay if exists
            var oldOverlay = hudCanvas.transform.Find("SettingsOverlay");
            if (oldOverlay != null)
            {
                Undo.DestroyObjectImmediate(oldOverlay.gameObject);
                Debug.Log("Removed old SettingsOverlay.");
            }

            // Remove old controller
            var oldController = hudCanvas.GetComponent<SettingsPanelController>();
            if (oldController != null)
                Undo.DestroyObjectImmediate(oldController);

            // Add Button to Settings if missing
            if (settingsButton.GetComponent<Button>() == null)
                Undo.AddComponent<Button>(settingsButton);

            // --- Overlay ---
            var overlay = new GameObject("SettingsOverlay");
            Undo.RegisterCreatedObjectUndo(overlay, "Create SettingsOverlay");
            overlay.transform.SetParent(hudCanvas.transform, false);
            overlay.transform.SetAsLastSibling();
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.55f);

            // --- Window ---
            var window = CreateRect("SettingsWindow", overlay.transform, new Vector2(560, 620));
            var windowImg = window.AddComponent<Image>();
            windowImg.color = new Color(0.13f, 0.13f, 0.16f, 0.98f);

            // --- Title bar ---
            var titleBar = CreateRect("TitleBar", window.transform);
            var titleBarRect = titleBar.GetComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0, 1);
            titleBarRect.anchorMax = new Vector2(1, 1);
            titleBarRect.pivot = new Vector2(0.5f, 1);
            titleBarRect.sizeDelta = new Vector2(0, 64);
            titleBarRect.anchoredPosition = Vector2.zero;
            var titleBarImg = titleBar.AddComponent<Image>();
            titleBarImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);

            AddText(titleBar.transform, "TitleText", "Einstellungen", 26, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Vector2(20, 0), new Vector2(-60, 0));

            // Close button
            var closeBtn = CreateRect("CloseButton", titleBar.transform);
            var closeBtnRect = closeBtn.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1, 0.5f);
            closeBtnRect.anchorMax = new Vector2(1, 0.5f);
            closeBtnRect.pivot = new Vector2(1, 0.5f);
            closeBtnRect.sizeDelta = new Vector2(44, 44);
            closeBtnRect.anchoredPosition = new Vector2(-10, 0);
            var closeBtnImg = closeBtn.AddComponent<Image>();
            closeBtnImg.color = new Color(0.75f, 0.15f, 0.15f, 1f);
            closeBtn.AddComponent<Button>();
            AddText(closeBtn.transform, "X", "X", 22, FontStyles.Bold, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.zero, fullStretch: true);

            // --- Scrollable content ---
            var scrollRect = CreateRect("ScrollArea", window.transform);
            var scrollRectComp = scrollRect.GetComponent<RectTransform>();
            scrollRectComp.anchorMin = new Vector2(0, 0);
            scrollRectComp.anchorMax = new Vector2(1, 1);
            scrollRectComp.offsetMin = new Vector2(0, 16);
            scrollRectComp.offsetMax = new Vector2(0, -64);

            var viewport = CreateRect("Viewport", scrollRect.transform);
            var vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            contentRect.anchoredPosition = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 12, 24);
            vlg.spacing = 12;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollRect.AddComponent<ScrollRect>();
            sr.content = contentRect;
            sr.viewport = vpRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 30;

            // --- Section helpers ---
            Color accent = new Color(1f, 0.72f, 0.18f, 1f);
            Color rowBg = new Color(0.2f, 0.2f, 0.23f, 1f);
            Color labelColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            Color trackColor = new Color(0.28f, 0.28f, 0.32f, 1f);
            Color fillColor = new Color(0.3f, 0.65f, 1f, 1f);

            void AddHeader(string text)
            {
                var h = new GameObject("Header_" + text);
                h.transform.SetParent(content.transform, false);
                var le = h.AddComponent<LayoutElement>();
                le.preferredHeight = 36;
                var t = h.AddComponent<TextMeshProUGUI>();
                t.text = text.ToUpper();
                t.fontSize = 13;
                t.fontStyle = FontStyles.Bold;
                t.color = accent;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.characterSpacing = 2;
            }

            void AddSliderRow(string label, float value = 0.75f)
            {
                var row = new GameObject("Row_" + label);
                row.transform.SetParent(content.transform, false);
                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = 52;
                var rowImg = row.AddComponent<Image>();
                rowImg.color = rowBg;

                // Label
                var lGO = new GameObject("Label");
                lGO.transform.SetParent(row.transform, false);
                var lRect = lGO.AddComponent<RectTransform>();
                lRect.anchorMin = new Vector2(0, 0.5f);
                lRect.anchorMax = new Vector2(0, 0.5f);
                lRect.pivot = new Vector2(0, 0.5f);
                lRect.sizeDelta = new Vector2(160, 52);
                lRect.anchoredPosition = new Vector2(16, 0);
                var lt = lGO.AddComponent<TextMeshProUGUI>();
                lt.text = label;
                lt.fontSize = 16;
                lt.color = labelColor;
                lt.alignment = TextAlignmentOptions.MidlineLeft;

                // Track
                var track = new GameObject("Track");
                track.transform.SetParent(row.transform, false);
                var tRect = track.AddComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0, 0.5f);
                tRect.anchorMax = new Vector2(1, 0.5f);
                tRect.pivot = new Vector2(0, 0.5f);
                tRect.sizeDelta = new Vector2(-196, 8);
                tRect.anchoredPosition = new Vector2(180, 0);
                var tImg = track.AddComponent<Image>();
                tImg.color = trackColor;

                // Fill
                var fill = new GameObject("Fill");
                fill.transform.SetParent(track.transform, false);
                var fRect = fill.AddComponent<RectTransform>();
                fRect.anchorMin = Vector2.zero;
                fRect.anchorMax = new Vector2(value, 1);
                fRect.offsetMin = Vector2.zero;
                fRect.offsetMax = Vector2.zero;
                var fImg = fill.AddComponent<Image>();
                fImg.color = fillColor;

                // Handle dot
                var handle = new GameObject("Handle");
                handle.transform.SetParent(track.transform, false);
                var hRect = handle.AddComponent<RectTransform>();
                hRect.anchorMin = new Vector2(value, 0.5f);
                hRect.anchorMax = new Vector2(value, 0.5f);
                hRect.pivot = new Vector2(0.5f, 0.5f);
                hRect.sizeDelta = new Vector2(18, 18);
                hRect.anchoredPosition = Vector2.zero;
                var hImg = handle.AddComponent<Image>();
                hImg.color = Color.white;
            }

            void AddToggleRow(string label, bool isOn = true)
            {
                var row = new GameObject("Row_" + label);
                row.transform.SetParent(content.transform, false);
                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = 52;
                var rowImg = row.AddComponent<Image>();
                rowImg.color = rowBg;

                var lGO = new GameObject("Label");
                lGO.transform.SetParent(row.transform, false);
                var lRect = lGO.AddComponent<RectTransform>();
                lRect.anchorMin = new Vector2(0, 0.5f);
                lRect.anchorMax = new Vector2(0, 0.5f);
                lRect.pivot = new Vector2(0, 0.5f);
                lRect.sizeDelta = new Vector2(300, 52);
                lRect.anchoredPosition = new Vector2(16, 0);
                var lt = lGO.AddComponent<TextMeshProUGUI>();
                lt.text = label;
                lt.fontSize = 16;
                lt.color = labelColor;
                lt.alignment = TextAlignmentOptions.MidlineLeft;

                // Pill background
                var pill = new GameObject("Pill");
                pill.transform.SetParent(row.transform, false);
                var pRect = pill.AddComponent<RectTransform>();
                pRect.anchorMin = new Vector2(1, 0.5f);
                pRect.anchorMax = new Vector2(1, 0.5f);
                pRect.pivot = new Vector2(1, 0.5f);
                pRect.sizeDelta = new Vector2(52, 28);
                pRect.anchoredPosition = new Vector2(-16, 0);
                var pImg = pill.AddComponent<Image>();
                pImg.color = isOn ? fillColor : trackColor;

                // Knob
                var knob = new GameObject("Knob");
                knob.transform.SetParent(pill.transform, false);
                var kRect = knob.AddComponent<RectTransform>();
                kRect.anchorMin = new Vector2(isOn ? 1f : 0f, 0.5f);
                kRect.anchorMax = new Vector2(isOn ? 1f : 0f, 0.5f);
                kRect.pivot = new Vector2(isOn ? 1f : 0f, 0.5f);
                kRect.sizeDelta = new Vector2(24, 24);
                kRect.anchoredPosition = new Vector2(isOn ? -2 : 2, 0);
                var kImg = knob.AddComponent<Image>();
                kImg.color = Color.white;
            }

            void AddSelectRow(string label, string value)
            {
                var row = new GameObject("Row_" + label);
                row.transform.SetParent(content.transform, false);
                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = 52;
                var rowImg = row.AddComponent<Image>();
                rowImg.color = rowBg;

                var lGO = new GameObject("Label");
                lGO.transform.SetParent(row.transform, false);
                var lRect = lGO.AddComponent<RectTransform>();
                lRect.anchorMin = new Vector2(0, 0.5f);
                lRect.anchorMax = new Vector2(0, 0.5f);
                lRect.pivot = new Vector2(0, 0.5f);
                lRect.sizeDelta = new Vector2(160, 52);
                lRect.anchoredPosition = new Vector2(16, 0);
                var lt = lGO.AddComponent<TextMeshProUGUI>();
                lt.text = label;
                lt.fontSize = 16;
                lt.color = labelColor;
                lt.alignment = TextAlignmentOptions.MidlineLeft;

                var pill = new GameObject("ValueBox");
                pill.transform.SetParent(row.transform, false);
                var pRect = pill.AddComponent<RectTransform>();
                pRect.anchorMin = new Vector2(1, 0.5f);
                pRect.anchorMax = new Vector2(1, 0.5f);
                pRect.pivot = new Vector2(1, 0.5f);
                pRect.sizeDelta = new Vector2(180, 36);
                pRect.anchoredPosition = new Vector2(-16, 0);
                var pImg = pill.AddComponent<Image>();
                pImg.color = trackColor;

                var vt = new GameObject("ValueText");
                vt.transform.SetParent(pill.transform, false);
                var vtRect = vt.AddComponent<RectTransform>();
                vtRect.anchorMin = Vector2.zero;
                vtRect.anchorMax = Vector2.one;
                vtRect.offsetMin = new Vector2(10, 0);
                vtRect.offsetMax = new Vector2(-10, 0);
                var vtText = vt.AddComponent<TextMeshProUGUI>();
                vtText.text = value;
                vtText.fontSize = 15;
                vtText.color = Color.white;
                vtText.alignment = TextAlignmentOptions.Center;
            }

            void AddSpacer()
            {
                var s = new GameObject("Spacer");
                s.transform.SetParent(content.transform, false);
                var le = s.AddComponent<LayoutElement>();
                le.preferredHeight = 8;
            }

            // Audio
            AddHeader("Audio");
            AddSliderRow("Master Volume", 0.8f);
            AddSliderRow("Musik", 0.6f);
            AddSliderRow("Soundeffekte", 0.75f);
            AddSpacer();

            // Grafik
            AddHeader("Grafik");
            AddSelectRow("Qualität", "Hoch");
            AddSelectRow("Auflösung", "1920 × 1080");
            AddToggleRow("Vollbild", true);
            AddToggleRow("VSync", true);
            AddSpacer();

            // Gameplay
            AddHeader("Gameplay");
            AddSelectRow("Sprache", "Deutsch");
            AddSelectRow("Schwierigkeit", "Normal");

            // --- Wire controller ---
            var controller = hudCanvas.AddComponent<SettingsPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("settingsPanel").objectReferenceValue = overlay;
            so.ApplyModifiedProperties();

            var settingsBtn = settingsButton.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBtn.onClick, controller.TogglePanel);
            var closeBtnComp = closeBtn.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtnComp.onClick, controller.ClosePanel);

            overlay.SetActive(false);

            EditorUtility.SetDirty(hudCanvas);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("Settings Panel rebuilt! Save with Ctrl+S.");
            Selection.activeGameObject = overlay;
        }

        static GameObject CreateRect(string name, Transform parent, Vector2? size = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            if (size.HasValue)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size.Value;
                rect.anchoredPosition = Vector2.zero;
            }
            return go;
        }

        static void AddText(Transform parent, string name, string text, float size,
            FontStyles style, TextAlignmentOptions align,
            Vector2 offsetMin, Vector2 offsetMax, bool fullStretch = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            if (fullStretch)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
        }
    }
}
