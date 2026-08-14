using System.Collections;
using System.Collections.Generic;
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

        [Tooltip("Kipp-Transform der Hauptsicherung. Optional.")]
        [SerializeField] private Transform breakerLever;

        [Tooltip("Tuer-Transform (Box_Door). Optional — der Blowout laeuft sonst ohne Tuer.")]
        [SerializeField] private Transform boxDoor;

        [Tooltip("Kipphebel-Bank in der Nische (Breaker_Bank, seit ASSET_VERSION 3). Optional.")]
        [SerializeField] private Transform breakerBank;

        [Tooltip("Renderer der Bank-Kipphebel (Breaker_Tog_XX) — im Fehlerfall rot getintet.")]
        [SerializeField] private Renderer[] bankToggles;

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

        [Header("Lever")]
        [Tooltip("Kippwinkel um die lokale X-Achse: NEGATIV kippt den Hebel wie einen " +
                 "Messerschalter nach vorne-unten aus der Wand (visuell verifiziert; +135 " +
                 "kippte ihn in den Kasten). X-Rotationen behalten das Blender-Vorzeichen, " +
                 "weil der FBX-Import die X-Achse spiegelt.")]
        [SerializeField] private float leverBlowoutAngle = -135f;

        [Header("Blowout-Choreografie")]
        [Tooltip("Kippwinkel der Bank um lokal X: POSITIV = nach vorne-unten (visuell " +
                 "verifiziert). Bewusst entgegengesetztes Vorzeichen zum Haupthebel (-135): " +
                 "die Bank ist ein Empty, und Unitys FBX-Import gibt Null-Knoten eine " +
                 "kompensierende Rotation statt gespiegelter Geometrie — ihre lokale X-Achse " +
                 "zeigt dadurch andersherum als die der Mesh-Kinder.")]
        [SerializeField] private float bankBlowoutAngle = 55f;
        [Tooltip("Sekunden, die die Bank dem Haupthebel hinterherschnappt — liest sich als " +
                 "Kraftuebertragung ueber die Schubstange.")]
        [SerializeField] private float bankFollowDelay = 0.07f;
        [Tooltip("Oeffnungswinkel der Tuer um lokal Z beim Blowout (Blender-Scharnier: -75 = offen).")]
        [SerializeField] private float doorOpenAngle = 75f;
        [Tooltip("Dauer des Hebel-/Bank-Schlags. Kurz — das ist ein Ereignis, keine Handlung.")]
        [SerializeField] private float leverSlamDuration = 0.12f;
        [SerializeField] private float doorOpenDuration = 0.35f;
        [SerializeField] private float doorCloseDuration = 0.6f;
        [Tooltip("Dauer des Zurueckdrueckens von Bank und Hebel — bewusst langsamer als der Schlag.")]
        [SerializeField] private float leverResetDuration = 0.8f;
        [Tooltip("Easing fuers Rausfliegen. Werte ueber 1 ergeben den Ueberschwinger. " +
                 "Peak bewusst klein halten: bei 135 Grad Grundwinkel bedeutet 1.04 schon " +
                 "+5 Grad ueber den in Blender geprueften Schwenkbereich hinaus.")]
        [SerializeField] private AnimationCurve slamCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(0.75f, 1.04f),
            new Keyframe(1f, 1f));
        [Tooltip("Easing fuers Zuruecksetzen — ruhig, ohne Ueberschwinger.")]
        [SerializeField] private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Tint der Bank-Kipphebel im Fehlerfall. HDR-Faktor > 1, weil die Hebel dunkel " +
                 "gebacken sind — _BaseColor multipliziert die Textur, ohne Faktor bliebe der " +
                 "Tint nahezu unsichtbar.")]
        [SerializeField] private Color toggleTripColor = new Color(2.5f, 0.35f, 0.3f);

        // Wiederverwendeter Property-Block statt pro Segment eine Material-Instanz zu erzeugen
        // (segments[i].material wuerde bei jedem Aufruf eine dauerhafte Kopie anlegen - Leak).
        private MaterialPropertyBlock _mpb;

        // Letzter bekannter Lastwert, damit TriggerBlowout() nach dem Reset dieselbe
        // Apply()-Logik erneut nutzen kann, statt die Faerbung zu duplizieren.
        private float _lastFraction01;

        private Coroutine _blowoutRoutine;

        // Waehrend die Sicherung geflogen ist, darf der Monitor die Saeule nicht wieder
        // einfaerben. Ohne dieses Flag wuerde das naechste Sample (Poll-Intervall 1 s) die
        // Segmente wieder aufleuchten lassen, waehrend der Hebel unten haengt — Hebel und
        // Saeule wuerden Widerspruechliches erzaehlen.
        private bool _blownOut;

        /// <summary>
        /// Steht die Sicherung auf "geflogen"? Sie bleibt in diesem Zustand, bis jemand
        /// <see cref="ResetBreaker"/> ruft — sie macht sich NICHT von allein wieder rein.
        /// </summary>
        public bool IsBlownOut => _blownOut;

        /// <summary>
        /// Wird vom Editor-Builder direkt nach dem Erzeugen der Segmente aufgerufen.
        /// Bewusst ohne sofortiges Einfaerben: im Edit-Modus wuerde das Material-Instanzen
        /// erzeugen, die in der Szene haengen bleiben.
        /// </summary>
        public void AssignSegments(Renderer[] bottomToTop)
        {
            segments = bottomToTop;
        }

        /// <summary>
        /// Verbindet diesen Presenter mit dem Hebel-Transform der Hauptsicherung.
        /// Wird vom OfficeLayoutBuilder nach dem Erzeugen/Verknuepfen des Hebels aufgerufen.
        /// Darf null sein - TriggerBlowout() ueberspringt dann einfach die Hebel-Animation.
        /// </summary>
        public void AssignLever(Transform lever)
        {
            breakerLever = lever;
        }

        /// <summary>
        /// Verbindet diesen Presenter mit der Tuer (Box_Door). Darf null sein —
        /// die Blowout-Choreografie ueberspringt dann den Tuer-Teil.
        /// </summary>
        public void AssignDoor(Transform door)
        {
            boxDoor = door;
        }

        /// <summary>
        /// Verbindet diesen Presenter mit der Kipphebel-Bank in der Nische (Breaker_Bank)
        /// samt der Kipphebel-Renderer fuer den Fehlerfall-Tint. Beides darf null sein.
        /// </summary>
        public void AssignBank(Transform bank, Renderer[] toggles)
        {
            breakerBank = bank;
            bankToggles = toggles;
        }

        private void Awake()
        {
            if (monitor == null)
                monitor = FindAnyObjectByType<SystemLoadMonitor>(FindObjectsInactive.Exclude);

            ResolveContractBindings();
        }

        /// <summary>
        /// Selbstheilung ueber die Vertragsnamen der FBX-Hierarchie (siehe Art_Source/README.md):
        /// alles, was der OfficeLayoutBuilder nicht zugewiesen hat — etwa weil die Szenen-Instanz
        /// aelter ist als das jeweilige Feature — wird hier nachgebunden. Ohne das bliebe eine
        /// Teilanimation einfach stumm: die Tuer "geht nicht auf", ohne dass irgendwo ein
        /// Fehler stuende. Explizite Builder-Zuweisungen haben weiter Vorrang (nur null-Felder
        /// werden befuellt).
        /// </summary>
        private void ResolveContractBindings()
        {
            // Auch die Segmente selbst einsammeln: ein "Reset" der Komponente im Inspector
            // leert das Array — ohne diese Heilung bliebe die Saeule danach dauerhaft dunkel.
            if (segments == null || segments.Length == 0)
            {
                Transform segmentsRoot = transform.Find("Segments");
                if (segmentsRoot != null)
                {
                    var found = new List<Renderer>();
                    foreach (Transform seg in segmentsRoot)
                    {
                        if (seg.TryGetComponent(out Renderer segmentRenderer))
                            found.Add(segmentRenderer);
                    }
                    // Zero-Padding macht alphabetisch = numerisch (Segment_00 unten).
                    found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    segments = found.ToArray();
                }
            }

            if (breakerLever == null)
                breakerLever = transform.Find("Breaker_Lever");
            if (boxDoor == null)
                boxDoor = transform.Find("Box_Door");
            if (breakerBank == null)
                breakerBank = transform.Find("Breaker_Bank");

            if (breakerBank != null && (bankToggles == null || bankToggles.Length == 0))
            {
                var toggles = new List<Renderer>();
                foreach (Transform child in breakerBank)
                {
                    if (child.name.StartsWith("Breaker_Tog_", System.StringComparison.Ordinal)
                        && child.TryGetComponent(out Renderer toggleRenderer))
                    {
                        toggles.Add(toggleRenderer);
                    }
                }
                bankToggles = toggles.ToArray();
            }
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

            // Deaktivieren stoppt laufende Coroutinen — ohne dieses Zuruecksetzen bliebe
            // _blownOut haengen und Apply() wuerde die Saeule nach dem Reaktivieren
            // dauerhaft nicht mehr einfaerben.
            _blowoutRoutine = null;
            _blownOut = false;
            if (breakerLever != null)
                breakerLever.localRotation = Quaternion.identity;
            if (breakerBank != null)
                breakerBank.localRotation = Quaternion.identity;
            if (boxDoor != null)
                boxDoor.localRotation = Quaternion.identity;
            TintBankToggles(false);
        }

        private void OnSample(SystemLoadSample sample) => Apply(sample.Fraction01);

        private void Apply(float fraction01)
        {
            // Zuerst merken, dann erst pruefen: der Wert wird auch gebraucht, wenn gerade nicht
            // gefaerbt werden darf — nach dem Blowout wird genau darauf zurueckgestellt.
            _lastFraction01 = fraction01;

            if (_blownOut)
                return;

            if (segments == null || segments.Length == 0)
                return;

            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction01 * segments.Length), 0, segments.Length);

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null)
                    continue;

                Color color = emptyColor;
                bool isFilled = i < filled;
                if (isFilled)
                {
                    // VU-Meter: die Farbe haengt an der Hoehe des Segments, nicht am Gesamtwert.
                    float t = (i + 0.5f) / segments.Length;
                    color = t < warnThreshold ? lowColor
                          : t < criticalThreshold ? midColor
                          : highColor;
                }

                SetSegmentColor(segments[i], color, isFilled);
            }
        }

        /// <summary>
        /// Faerbt ein einzelnes Segment ueber einen wiederverwendeten MaterialPropertyBlock ein -
        /// statt ueber renderer.material (Singular-Property), das pro Aufruf eine dauerhafte
        /// Material-Instanz-Kopie erzeugen wuerde (Leak). renderer.sharedMaterial dient hier nur
        /// zum Pruefen, welche Shader-Properties existieren, und wird selbst nie veraendert.
        /// </summary>
        private void SetSegmentColor(Renderer renderer, Color color, bool isFilled)
        {
            Material sharedMat = renderer.sharedMaterial;
            if (sharedMat == null)
                return;

            _mpb ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_mpb);

            // URP-Lit reagiert auf _BaseColor; Emission macht die Segmente aus der
            // Iso-Perspektive auch im Schatten lesbar.
            if (sharedMat.HasProperty("_BaseColor"))
                _mpb.SetColor("_BaseColor", color);

            if (sharedMat.HasProperty("_EmissionColor"))
            {
                // WICHTIG: Das Shader-KEYWORD "_EMISSION" laesst sich NICHT per
                // MaterialPropertyBlock setzen - nur Property-WERTE wie _EmissionColor selbst.
                // Das Keyword muss am GETEILTEN Material aktiv sein, sonst bleibt Emission aus,
                // egal welche Farbe hier gesetzt wird. Das uebernimmt der parallele Builder in
                // OfficeLayoutBuilder.GetOrCreateSegmentMaterial() per mat.EnableKeyword("_EMISSION")
                // und mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive.
                _mpb.SetColor("_EmissionColor", isFilled ? color * 1.6f : Color.black);
            }

            renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// Faerbt alle Segmente auf die "leer"-Farbe, unabhaengig vom aktuellen Lastwert -
        /// genutzt waehrend die Sicherung fliegt (<see cref="TriggerBlowout"/>).
        /// </summary>
        private void SetAllSegmentsEmpty()
        {
            if (segments == null)
                return;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                    SetSegmentColor(segments[i], emptyColor, false);
            }
        }

        /// <summary>
        /// Oeffentlicher Hook fuer Story-/Comedy-Events: die Sicherung "fliegt" als komplette
        /// Choreografie — Hebel knallt heraus, die Bank-Kipphebel in der Nische schnappen einen
        /// Wimpernschlag spaeter nach (Kraftuebertragung ueber die Schubstange) und werden rot
        /// getintet, die Tuer springt auf, die Saeule wird schlagartig leer.
        /// Danach BLEIBT die Sicherung geflogen: das Wieder-Reinmachen ist eine bewusste
        /// (Spieler-)Handlung und laeuft ueber <see cref="ResetBreaker"/> — nichts stellt sich
        /// von allein zurueck.
        /// </summary>
        public void TriggerBlowout()
        {
            if (_blowoutRoutine != null)
                StopCoroutine(_blowoutRoutine);

            _blowoutRoutine = StartCoroutine(BlowoutRoutine());
        }

        /// <summary>
        /// Die Umkehrung von <see cref="TriggerBlowout"/>, mit bewusst anderer Dramaturgie:
        /// Tuer zu (ruhig), Bank hoch, Hebel wird langsam "reingedrueckt", dann faerbt sich die
        /// Saeule auf den zuletzt bekannten Lastwert zurueck. Rausfliegen ist ein Ereignis,
        /// Reinmachen eine Handlung.
        /// </summary>
        public void ResetBreaker()
        {
            if (!_blownOut)
                return;

            if (_blowoutRoutine != null)
                StopCoroutine(_blowoutRoutine);

            _blowoutRoutine = StartCoroutine(ResetRoutine());
        }

        private IEnumerator BlowoutRoutine()
        {
            _blownOut = true;

            // Der Strom ist SOFORT weg — die Saeule erlischt mit dem Schlag, nicht danach.
            SetAllSegmentsEmpty();

            yield return SlamPhase();
            TintBankToggles(true);

            // Tuer springt auf — federnd (slamCurve), sie wird vom Ereignis aufgestossen.
            if (boxDoor != null)
                yield return RotateLocal(boxDoor, Quaternion.Euler(0f, 0f, doorOpenAngle),
                                         doorOpenDuration, slamCurve);

            // Ende: die Sicherung bleibt geflogen, bis ResetBreaker() gerufen wird.
            _blowoutRoutine = null;
        }

        private IEnumerator ResetRoutine()
        {
            // Reset-Dramaturgie: erst die Tuer (ruhig), dann die Bank, zuletzt der Haupthebel.
            if (boxDoor != null)
                yield return RotateLocal(boxDoor, Quaternion.identity, doorCloseDuration, settleCurve);

            if (breakerBank != null)
                yield return RotateLocal(breakerBank, Quaternion.identity,
                                         leverResetDuration * 0.5f, settleCurve);
            TintBankToggles(false);

            if (breakerLever != null)
                yield return RotateLocal(breakerLever, Quaternion.identity,
                                         leverResetDuration, settleCurve);

            // Keine duplizierte Faerbungs-Logik - einfach die bestehende Apply()-Logik mit dem
            // zuletzt bekannten Sample-Wert erneut anwenden. Das Flag muss vorher fallen,
            // sonst blockt Apply() sich selbst.
            _blownOut = false;
            Apply(_lastFraction01);

            _blowoutRoutine = null;
        }

        /// <summary>
        /// Hebel und Bank schlagen fast gleichzeitig heraus — die Bank um
        /// <see cref="bankFollowDelay"/> versetzt. Beide laufen in EINER Coroutine, damit
        /// TriggerBlowout() bei einem Re-Trigger keine verwaisten Teil-Animationen hinterlaesst
        /// (StopCoroutine stoppt nur die Haupt-Routine, keine verschachtelten Starts).
        /// </summary>
        private IEnumerator SlamPhase()
        {
            // Achsenlage — alle drei Vorzeichen sind im Play Mode VERIFIZIERT, nicht hergeleitet:
            //   Hebel (Mesh)  -135 um lokal X = nach vorne-unten (+135 kippte in den Kasten),
            //   Bank  (Empty)  +55 um lokal X = nach vorne-unten,
            //   Tuer  (Mesh)   +75 um lokal Z = auf.
            // Dass Hebel und Bank um dieselbe Achse kippen und trotzdem entgegengesetzte
            // Vorzeichen brauchen, kommt vom FBX-Import: Meshes werden gespiegelt (X-Achse),
            // Null-Knoten wie Breaker_Bank bekommen stattdessen eine kompensierende Rotation —
            // ihre lokale X-Achse zeigt dadurch andersherum. Nicht "vereinheitlichen".
            Quaternion leverFrom = breakerLever != null ? breakerLever.localRotation : Quaternion.identity;
            Quaternion bankFrom = breakerBank != null ? breakerBank.localRotation : Quaternion.identity;
            Quaternion leverTo = Quaternion.Euler(leverBlowoutAngle, 0f, 0f);
            Quaternion bankTo = Quaternion.Euler(bankBlowoutAngle, 0f, 0f);

            // Untergrenze gegen Division durch 0, falls die Dauer im Inspector auf 0 steht.
            float slam = Mathf.Max(leverSlamDuration, 0.0001f);
            float total = slam + bankFollowDelay;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (breakerLever != null)
                {
                    float k = Mathf.Clamp01(t / slam);
                    breakerLever.localRotation =
                        Quaternion.SlerpUnclamped(leverFrom, leverTo, slamCurve.Evaluate(k));
                }
                if (breakerBank != null)
                {
                    float k = Mathf.Clamp01((t - bankFollowDelay) / slam);
                    breakerBank.localRotation =
                        Quaternion.SlerpUnclamped(bankFrom, bankTo, slamCurve.Evaluate(k));
                }
                yield return null;
            }

            if (breakerLever != null)
                breakerLever.localRotation = leverTo;
            if (breakerBank != null)
                breakerBank.localRotation = bankTo;
        }

        /// <summary>
        /// Dreht ein Transform ueber eine Kurve auf eine lokale Ziel-Rotation.
        /// SlerpUnclamped, damit Kurvenwerte ueber 1 (Ueberschwinger) wirken koennen.
        /// </summary>
        private static IEnumerator RotateLocal(Transform target, Quaternion to,
                                               float duration, AnimationCurve curve)
        {
            Quaternion from = target.localRotation;
            if (duration <= 0f)
            {
                target.localRotation = to;
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                target.localRotation =
                    Quaternion.SlerpUnclamped(from, to, curve.Evaluate(t / duration));
                yield return null;
            }
            target.localRotation = to;
        }

        /// <summary>
        /// Tintet die Bank-Kipphebel rot (Fehlerfall) bzw. zurueck auf neutral — per
        /// MaterialPropertyBlock, aus demselben Grund wie bei den Segmenten (kein Material-Leak,
        /// Batch bleibt erhalten).
        /// </summary>
        private void TintBankToggles(bool tripped)
        {
            if (bankToggles == null)
                return;

            _mpb ??= new MaterialPropertyBlock();
            foreach (Renderer toggle in bankToggles)
            {
                if (toggle == null)
                    continue;

                toggle.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", tripped ? toggleTripColor : Color.white);
                toggle.SetPropertyBlock(_mpb);
            }
        }

        [ContextMenu("Sicherung testweise ausloesen (Play Mode)")]
        private void EditorTestBlowout()
        {
            if (Application.isPlaying)
            {
                TriggerBlowout();
            }
            else
            {
                Debug.LogWarning("[SystemLoad] Bitte starte das Spiel, um die Sicherung testweise auszuloesen.");
            }
        }

        [ContextMenu("Sicherung testweise reinmachen (Play Mode)")]
        private void EditorTestReset()
        {
            if (Application.isPlaying)
            {
                ResetBreaker();
            }
            else
            {
                Debug.LogWarning("[SystemLoad] Bitte starte das Spiel, um die Sicherung testweise reinzumachen.");
            }
        }
    }
}
