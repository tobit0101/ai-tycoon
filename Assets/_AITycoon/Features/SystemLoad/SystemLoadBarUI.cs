using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Segmentierte Belegungsleiste für die System-Speicherlast (VU-Meter-Optik).
    ///
    /// Bildsprache-Regel: "Raum zeigt Klischee, Datenblatt zeigt Wahrheit."
    /// Die UI selbst bleibt karikaturhaft (Segmente + Stimmungs-Text wie
    /// "Am Anschlag!"). Die harten technischen Werte (GB, Quelle) erscheinen
    /// nur als Datenblatt-Tooltip beim Hover — nie direkt auf der Leiste.
    ///
    /// Konzept-Regel: Belegung, kein Tank — Segmente leuchten auf und erlöschen,
    /// nichts "verbraucht" sich über Zeit.
    /// </summary>
    public class SystemLoadBarUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [Tooltip("Quelle der Messwerte. Wenn leer, wird in der Szene gesucht.")]
        [SerializeField] private SystemLoadMonitor monitor;

        [Tooltip("Container, in dem die Segmente erzeugt werden.")]
        [SerializeField] private RectTransform segmentContainer;

        [Tooltip("Karikatur-Statuszeile, z.B. \"Gut zu tun\".")]
        [SerializeField] private TMP_Text statusLabel;

        [Tooltip("Datenblatt-Panel mit den echten Zahlen. Nur bei Hover sichtbar.")]
        [SerializeField] private GameObject tooltipPanel;

        [Tooltip("Label im Datenblatt-Panel.")]
        [SerializeField] private TMP_Text tooltipLabel;

        [Header("Segments")]
        [SerializeField] private int segmentCount = 16;
        [SerializeField] private float segmentSpacing = 2f;

        [Header("Colors")]
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color lowColor = new Color(0.30f, 0.85f, 0.35f);
        [SerializeField] private Color midColor = new Color(0.95f, 0.80f, 0.25f);
        [SerializeField] private Color highColor = new Color(0.95f, 0.30f, 0.25f);

        [Header("Thresholds")]
        [Range(0f, 1f)]
        [SerializeField] private float warnThreshold = 0.60f;
        [Range(0f, 1f)]
        [SerializeField] private float criticalThreshold = 0.85f;

        [Header("Karikatur-Status")]
        [SerializeField] private string statusLow = "Läuft entspannt";
        [SerializeField] private string statusMid = "Gut zu tun";
        [SerializeField] private string statusHigh = "Am Anschlag!";

        private Image[] segments;

        /// <summary>Die echten Zahlen — für Datenblatt/Akte, nie für die Karikatur-UI.</summary>
        public string TechnicalInfo { get; private set; } = "—";

        private void Awake()
        {
            if (monitor == null)
                monitor = FindAnyObjectByType<SystemLoadMonitor>(FindObjectsInactive.Exclude);

            BuildSegments();

            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (monitor != null)
            {
                monitor.SampleUpdated += Refresh;
                if (monitor.HasData)
                    Refresh(monitor.Current);
                else if (statusLabel != null)
                    statusLabel.text = "…";
            }
            else if (statusLabel != null)
            {
                statusLabel.text = "—";
            }
        }

        private void OnDisable()
        {
            if (monitor != null)
                monitor.SampleUpdated -= Refresh;
        }

        private void BuildSegments()
        {
            if (segmentContainer == null)
            {
                Debug.LogWarning("[SystemLoadBarUI] Kein Segment-Container zugewiesen.");
                return;
            }

            // Layout sicherstellen: gleichmäßige horizontale Verteilung.
            var layout = segmentContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = segmentContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = segmentSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            segments = new Image[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                var go = new GameObject($"Segment_{i:00}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(segmentContainer, false);

                var image = go.GetComponent<Image>();
                image.color = emptyColor;
                image.raycastTarget = false;
                segments[i] = image;
            }
        }

        private void Refresh(SystemLoadSample sample)
        {
            if (segments != null)
            {
                int filled = Mathf.Clamp(Mathf.RoundToInt(sample.Fraction01 * segments.Length), 0, segments.Length);
                for (int i = 0; i < segments.Length; i++)
                {
                    if (i < filled)
                    {
                        // VU-Meter: Farbe hängt an der Position des Segments, nicht am Gesamtwert.
                        float t = (i + 0.5f) / segments.Length;
                        segments[i].color = t < warnThreshold ? lowColor
                                          : t < criticalThreshold ? midColor
                                          : highColor;
                    }
                    else
                    {
                        segments[i].color = emptyColor;
                    }
                }
            }

            // Karikatur-Status nach Gesamtauslastung.
            if (statusLabel != null)
            {
                statusLabel.text = sample.Fraction01 < warnThreshold ? statusLow
                                 : sample.Fraction01 < criticalThreshold ? statusMid
                                 : statusHigh;
            }

            // Die Wahrheit fürs Datenblatt.
            TechnicalInfo = $"{sample.UsedGb:0.0} / {sample.TotalGb:0.0} GB · {sample.SourceLabel} · {sample.Fraction01:P0}";
            if (tooltipLabel != null)
                tooltipLabel.text = $"Datenblatt\n{TechnicalInfo}";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }
    }
}
