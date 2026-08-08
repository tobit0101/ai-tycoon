using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AITycoon.Features.SystemLoad.EditorTools
{
    /// <summary>
    /// Erstellt die System-Lastleiste (Monitor + segmentierte HUD-Leiste) in der
    /// aktuellen Szene: Menü "AI Tycoon → UI → System-Lastleiste erstellen".
    ///
    /// Bildsprache: Die Leiste zeigt nur Karikatur (Titel "STROMLAST" + Stimmungs-Text).
    /// Die echten Zahlen liegen im Datenblatt-Tooltip, das beim Hover erscheint.
    /// </summary>
    public static class SystemLoadBarCreator
    {
        [MenuItem("AI Tycoon/UI/System-Lastleiste erstellen")]
        public static void CreateLoadBar()
        {
            // --- Canvas suchen oder anlegen ---
            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Exclude);
            if (canvas == null)
            {
                var canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD Canvas");
            }

            // Ohne EventSystem funktioniert das Datenblatt-Tooltip (Hover) nicht.
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Exclude) == null)
            {
                Debug.LogWarning("[SystemLoad] Kein EventSystem in der Szene — das Datenblatt-Tooltip braucht eines (GameObject → UI → Event System).");
            }

            // --- Monitor suchen oder anlegen ---
            SystemLoadMonitor monitor = Object.FindAnyObjectByType<SystemLoadMonitor>(FindObjectsInactive.Exclude);
            if (monitor == null)
            {
                var monitorGo = new GameObject("[System_Load_Monitor]");
                monitor = monitorGo.AddComponent<SystemLoadMonitor>();
                Undo.RegisterCreatedObjectUndo(monitorGo, "Create System Load Monitor");
            }

            // --- Panel (oben rechts) ---
            var panel = new GameObject("SystemLoadBar", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(panel, "Create System Load Bar");
            panel.transform.SetParent(canvas.transform, false);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-16f, -16f);
            panelRect.sizeDelta = new Vector2(340f, 56f);

            var background = panel.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = true; // nötig für das Hover-Tooltip

            // --- Segment-Container (obere Hälfte) ---
            var segmentsGo = new GameObject("Segments", typeof(RectTransform));
            segmentsGo.transform.SetParent(panel.transform, false);

            var segmentsRect = segmentsGo.GetComponent<RectTransform>();
            segmentsRect.anchorMin = new Vector2(0f, 0.5f);
            segmentsRect.anchorMax = new Vector2(1f, 1f);
            segmentsRect.offsetMin = new Vector2(8f, 2f);
            segmentsRect.offsetMax = new Vector2(-8f, -8f);

            // --- Untere Zeile: Titel links, Karikatur-Status rechts ---
            var titleLabel = CreateLabel(panel.transform, "TitleLabel",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.4f, 0.5f),
                offsetMin: new Vector2(8f, 4f), offsetMax: new Vector2(0f, -2f));
            titleLabel.text = "STROMLAST";
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var statusLabel = CreateLabel(panel.transform, "StatusLabel",
                anchorMin: new Vector2(0.4f, 0f), anchorMax: new Vector2(1f, 0.5f),
                offsetMin: new Vector2(0f, 4f), offsetMax: new Vector2(-8f, -2f));
            statusLabel.text = "…";
            statusLabel.alignment = TextAlignmentOptions.MidlineRight;

            // --- Datenblatt-Tooltip (unter der Leiste, initial versteckt) ---
            var tooltipGo = new GameObject("Datenblatt", typeof(RectTransform), typeof(Image));
            tooltipGo.transform.SetParent(panel.transform, false);

            var tooltipRect = tooltipGo.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(1f, 0f);
            tooltipRect.anchorMax = new Vector2(1f, 0f);
            tooltipRect.pivot = new Vector2(1f, 1f);
            tooltipRect.anchoredPosition = new Vector2(0f, -6f);
            tooltipRect.sizeDelta = new Vector2(340f, 44f);

            var tooltipBg = tooltipGo.GetComponent<Image>();
            tooltipBg.color = new Color(0f, 0f, 0f, 0.85f);
            tooltipBg.raycastTarget = false;

            var tooltipLabel = CreateLabel(tooltipGo.transform, "DatenblattLabel",
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                offsetMin: new Vector2(8f, 4f), offsetMax: new Vector2(-8f, -4f));
            tooltipLabel.text = "Datenblatt\n—";
            tooltipLabel.fontSize = 12f;
            tooltipLabel.alignment = TextAlignmentOptions.TopLeft;

            tooltipGo.SetActive(false);

            // --- Bar-Komponente verdrahten ---
            var bar = panel.AddComponent<SystemLoadBarUI>();
            var so = new SerializedObject(bar);
            so.FindProperty("monitor").objectReferenceValue = monitor;
            so.FindProperty("segmentContainer").objectReferenceValue = segmentsRect;
            so.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            so.FindProperty("tooltipPanel").objectReferenceValue = tooltipGo;
            so.FindProperty("tooltipLabel").objectReferenceValue = tooltipLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = panel;
            EditorSceneManager.MarkSceneDirty(panel.scene);

            Debug.Log("[SystemLoad] System-Lastleiste erstellt. Play drücken — Karikatur auf der Leiste, echte Zahlen im Hover-Datenblatt.");
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = 14f;
            label.raycastTarget = false;
            return label;
        }
    }
}
