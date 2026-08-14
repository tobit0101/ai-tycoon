using UnityEngine;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Die Weltobjekt-Haelfte der Denklast: eine Lastsaeule am Sicherungskasten (Konzept §2.3).
    ///
    /// Arbeitsteilung Welt/HUD: <see cref="SystemLoadBarUI"/> liefert die Praezision im HUD,
    /// dieses Objekt gibt der Ressource ein Zuhause im Raum. Beides haengt an derselben Quelle
    /// (<see cref="SystemLoadMonitor"/>) — keine Parallelimplementierung.
    ///
    /// Konzept-Regel: Belegung, kein Tank. Segmente leuchten auf und erloeschen wieder,
    /// nichts laeuft ueber Zeit leer.
    /// </summary>
    public class LoadPillar : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Quelle der Messwerte. Wenn leer, wird in der Szene gesucht.")]
        [SerializeField] private SystemLoadMonitor monitor;

        [Tooltip("Die Segmente von unten nach oben. Wird vom OfficeLayoutBuilder gesetzt.")]
        [SerializeField] private Renderer[] segments;

        [Header("Colors")]
        [SerializeField] private Color emptyColor = new Color(0.16f, 0.17f, 0.19f);
        [SerializeField] private Color lowColor = new Color(0.30f, 0.85f, 0.35f);
        [SerializeField] private Color midColor = new Color(0.95f, 0.80f, 0.25f);
        [SerializeField] private Color highColor = new Color(0.95f, 0.30f, 0.25f);

        [Header("Thresholds")]
        [Range(0f, 1f)]
        [SerializeField] private float warnThreshold = 0.60f;
        [Range(0f, 1f)]
        [SerializeField] private float criticalThreshold = 0.85f;

        /// <summary>
        /// Wird vom Editor-Builder direkt nach dem Erzeugen der Segmente aufgerufen.
        /// Bewusst ohne sofortiges Einfaerben: im Edit-Modus wuerde das Material-Instanzen
        /// erzeugen, die in der Szene haengen bleiben.
        /// </summary>
        public void AssignSegments(Renderer[] bottomToTop)
        {
            segments = bottomToTop;
        }

        private void Awake()
        {
            if (monitor == null)
                monitor = FindAnyObjectByType<SystemLoadMonitor>(FindObjectsInactive.Exclude);
        }

        private void OnEnable()
        {
            if (monitor == null)
                return;

            monitor.SampleUpdated += OnSample;
            if (monitor.HasData)
                OnSample(monitor.Current);
        }

        private void OnDisable()
        {
            if (monitor != null)
                monitor.SampleUpdated -= OnSample;
        }

        private void OnSample(SystemLoadSample sample) => Apply(sample.Fraction01);

        private void Apply(float fraction01)
        {
            if (segments == null || segments.Length == 0)
                return;

            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction01 * segments.Length), 0, segments.Length);

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null)
                    continue;

                Color color = emptyColor;
                if (i < filled)
                {
                    // VU-Meter: die Farbe haengt an der Hoehe des Segments, nicht am Gesamtwert.
                    float t = (i + 0.5f) / segments.Length;
                    color = t < warnThreshold ? lowColor
                          : t < criticalThreshold ? midColor
                          : highColor;
                }

                Material mat = segments[i].material;
                mat.color = color;

                // URP-Lit reagiert auf _BaseColor; Emission macht die Segmente aus der
                // Iso-Perspektive auch im Schatten lesbar.
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", i < filled ? color * 1.6f : Color.black);
                }
            }
        }
    }
}
