using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace AITycoon.Quests.Editor
{
    public static class QuestPanelBuilder
    {
        [MenuItem("AI Tycoon/Build Quest Panel")]
        public static void BuildQuestPanel()
        {
            // Find HUD_Canvas
            var hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas == null)
            {
                Debug.LogError("HUD_Canvas not found in scene!");
                return;
            }

            // Find Quest button
            var questButton = GameObject.Find("Quest");
            if (questButton == null)
            {
                Debug.LogError("Quest GameObject not found in scene!");
                return;
            }

            // Add Button component to Quest if not already there
            if (questButton.GetComponent<Button>() == null)
            {
                var btn = questButton.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                btn.colors = colors;
                Undo.RegisterCreatedObjectUndo(questButton, "Add Button to Quest");
            }

            // --- Build Overlay ---
            var overlay = new GameObject("QuestOverlay");
            overlay.transform.SetParent(hudCanvas.transform, false);
            Undo.RegisterCreatedObjectUndo(overlay, "Create QuestOverlay");

            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.5f);
            overlayImage.raycastTarget = true;

            // --- Build Window ---
            var window = new GameObject("QuestWindow");
            window.transform.SetParent(overlay.transform, false);

            var windowRect = window.AddComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(500, 600);
            windowRect.anchoredPosition = Vector2.zero;

            var windowImage = window.AddComponent<Image>();
            windowImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            // --- Title ---
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(window.transform, false);

            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0, 60);
            titleRect.anchoredPosition = Vector2.zero;

            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "Aufträge";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // --- Close Button ---
            var closeGO = new GameObject("CloseButton");
            closeGO.transform.SetParent(window.transform, false);

            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(40, 40);
            closeRect.anchoredPosition = new Vector2(-10, -10);

            var closeImage = closeGO.AddComponent<Image>();
            closeImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            closeGO.AddComponent<Button>();

            var closeTextGO = new GameObject("Text");
            closeTextGO.transform.SetParent(closeGO.transform, false);
            var closeTextRect = closeTextGO.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;
            var closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeText.text = "X";
            closeText.fontSize = 20;
            closeText.fontStyle = FontStyles.Bold;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;

            // --- Quest List (5 placeholders) ---
            var listGO = new GameObject("QuestList");
            listGO.transform.SetParent(window.transform, false);

            var listRect = listGO.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(20, 20);
            listRect.offsetMax = new Vector2(-20, -70);

            var vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            string[] placeholders = {
                "Quest 1: Platzhalter",
                "Quest 2: Platzhalter",
                "Quest 3: Platzhalter",
                "Quest 4: Platzhalter",
                "Quest 5: Platzhalter"
            };

            foreach (var label in placeholders)
            {
                var item = new GameObject("QuestItem");
                item.transform.SetParent(listGO.transform, false);

                var itemImage = item.AddComponent<Image>();
                itemImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

                var itemLayout = item.AddComponent<LayoutElement>();
                itemLayout.preferredHeight = 70;

                var itemTextGO = new GameObject("Label");
                itemTextGO.transform.SetParent(item.transform, false);
                var itemTextRect = itemTextGO.AddComponent<RectTransform>();
                itemTextRect.anchorMin = Vector2.zero;
                itemTextRect.anchorMax = Vector2.one;
                itemTextRect.offsetMin = new Vector2(15, 0);
                itemTextRect.offsetMax = new Vector2(-15, 0);
                var itemText = itemTextGO.AddComponent<TextMeshProUGUI>();
                itemText.text = label;
                itemText.fontSize = 18;
                itemText.alignment = TextAlignmentOptions.MidlineLeft;
                itemText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            }

            // --- Wire up QuestPanelController ---
            var controller = hudCanvas.GetComponent<QuestPanelController>()
                ?? hudCanvas.AddComponent<QuestPanelController>();

            var controllerSO = new SerializedObject(controller);
            controllerSO.FindProperty("questPanel").objectReferenceValue = overlay;
            controllerSO.ApplyModifiedProperties();

            // Wire Quest button -> OpenPanel
            var questBtn = questButton.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                questBtn.onClick,
                controller.TogglePanel
            );

            // Wire Close button -> ClosePanel
            var closeBtn = closeGO.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                closeBtn.onClick,
                controller.ClosePanel
            );

            // Panel starts hidden
            overlay.SetActive(false);

            EditorUtility.SetDirty(hudCanvas);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );

            Debug.Log("Quest Panel built successfully! Save the scene with Ctrl+S.");
            Selection.activeGameObject = overlay;
        }
    }
}
