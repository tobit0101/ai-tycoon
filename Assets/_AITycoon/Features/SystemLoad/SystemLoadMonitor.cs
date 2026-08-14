using System;
using UnityEngine;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Fragt die System-Speicherlast in einem festen Intervall ab und stellt sie
    /// dem UI (und später der Spiellogik, z.B. der "Sicherung") zur Verfügung.
    ///
    /// Provider-Wahl pro Plattform:
    ///  - macOS:   Unified Memory (Mach-API) — auf Apple Silicon ist RAM = GPU-Speicher
    ///  - Windows: VRAM-Auslastung aller aktiven GPUs (PDH-Counter),
    ///             Fallback auf RAM wenn Counter fehlen oder 0 liefern
    /// </summary>
    public class SystemLoadMonitor : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Abfrage-Intervall in Sekunden.")]
        [SerializeField] private float pollInterval = 1.0f;

        public SystemLoadSample Current { get; private set; }
        public bool HasData { get; private set; }

        /// <summary>Wird nach jeder erfolgreichen Abfrage ausgelöst.</summary>
        public event Action<SystemLoadSample> SampleUpdated;

        private ISystemLoadProvider provider;

        // Die echte Plattform-Quelle wird getrennt gehalten, damit OverrideProvider() sie
        // zurueckgeben kann, ohne sie neu aufbauen zu muessen.
        private ISystemLoadProvider platformProvider;

        // Provider sind nicht serialisierbar. Nach einem Domain Reload — jede Skript-Aenderung
        // waehrend der Play Mode laeuft — sind beide Referenzen weg, waehrend Awake() NICHT
        // erneut laeuft. Dieses Flag trennt "noch nie aufgebaut" von "bewusst abgeschaltet,
        // weil die Quelle ausgefallen ist": nur der erste Fall darf nachgezogen werden.
        private bool platformProviderResolved;

        private float timer;

        /// <summary>
        /// Laeuft die Anzeige gerade auf einer eingesetzten Ersatzquelle statt auf der echten
        /// Messung? Gedacht fuer Werkzeuge, die das sichtbar machen wollen.
        /// </summary>
        public bool IsSimulated => provider != null && provider != platformProvider;

        private void Awake()
        {
            provider = ResolvePlatformProvider();
            if (provider == null)
            {
                Debug.LogWarning("[SystemLoadMonitor] Keine Speicherlast-Quelle auf dieser Plattform verfügbar.");
            }
        }

        private ISystemLoadProvider ResolvePlatformProvider()
        {
            if (platformProviderResolved)
                return platformProvider;

            platformProviderResolved = true;
            platformProvider = CreateProvider();
            return platformProvider;
        }

        /// <summary>
        /// Setzt eine Ersatzquelle ein — gedacht fuer Test- und Simulationswerkzeuge, siehe
        /// <see cref="SimulatedLoadProvider"/>. <c>null</c> stellt die Plattform-Messung wieder her.
        ///
        /// Der Plattform-Provider wird dabei bewusst NICHT verworfen: zwischen Simulation und
        /// echter Messung soll sich beliebig oft umschalten lassen. Die uebergebene Ersatzquelle
        /// gehoert dem Aufrufer und wird hier nie verworfen.
        /// </summary>
        public void OverrideProvider(ISystemLoadProvider replacement)
        {
            provider = replacement ?? ResolvePlatformProvider();

            // Sofort ein Sample nachschieben. Ohne das zeigen HUD und Lastsaeule bis zum naechsten
            // Poll-Intervall (1 s) noch den alten Wert, und das Umschalten wirkt wie ein Aussetzer.
            ForceSample();
        }

        /// <summary>
        /// Erzwingt eine sofortige Abfrage, statt auf das naechste Poll-Intervall zu warten.
        /// Gedacht fuer Werkzeuge, die den Wert der Quelle von aussen veraendern: ohne das
        /// laege die Aufloesung eines Wertverlaufs bei <see cref="pollInterval"/> Sekunden.
        /// </summary>
        public void ForceSample()
        {
            timer = 0f;
            Poll();
        }

        private void Start()
        {
            Poll(); // Erste Messung sofort, nicht erst nach dem ersten Intervall.
        }

        private void Update()
        {
            // Nach einem Domain Reload ist die Referenz weg, ohne dass Awake() erneut lief.
            // Ohne dieses Nachziehen bliebe die Anzeige nach jeder Skript-Aenderung im
            // laufenden Play Mode dauerhaft auf dem letzten Wert stehen.
            if (provider == null && !platformProviderResolved)
                provider = ResolvePlatformProvider();

            if (provider == null)
                return;

            timer += Time.unscaledDeltaTime;
            if (timer < pollInterval)
                return;

            timer = 0f;
            Poll();
        }

        private void Poll()
        {
            if (provider == null)
                return;

            try
            {
                if (provider.TryGetSample(out SystemLoadSample sample))
                {
                    Current = sample;
                    HasData = true;
                    SampleUpdated?.Invoke(sample);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SystemLoadMonitor] Abfrage fehlgeschlagen, Monitor wird deaktiviert: {e.Message}");
                provider.Dispose();

                // Wenn die ausgefallene Quelle die Plattform-Messung war, muss auch deren
                // Referenz fallen — sonst gaebe OverrideProvider(null) spaeter ein bereits
                // verworfenes Objekt zurueck.
                if (provider == platformProvider)
                    platformProvider = null;

                provider = null;
            }
        }

        private static ISystemLoadProvider CreateProvider()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            var mac = new MacUnifiedMemoryProvider();
            if (mac.TryGetSample(out _))
                return mac;
            mac.Dispose();
            return null;
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            // Bevorzugt: VRAM über die GPU-Performance-Counter.
            var gpu = new WindowsGpuMemoryProvider();
            if (gpu.TryGetSample(out SystemLoadSample probe) && probe.UsedBytes > 0)
                return gpu;
            gpu.Dispose();

            // Fallback: RAM (z.B. iGPU ohne dediziertes VRAM oder fehlende Counter).
            var ram = new WindowsRamProvider();
            if (ram.TryGetSample(out _))
                return ram;
            ram.Dispose();
            return null;
#else
            return null;
#endif
        }

        private void OnDestroy()
        {
            // Nur die selbst erzeugte Plattform-Quelle verwerfen. Eine per OverrideProvider()
            // eingesetzte Ersatzquelle gehoert dem Aufrufer.
            platformProvider?.Dispose();
            platformProvider = null;
            provider = null;
        }
    }
}
