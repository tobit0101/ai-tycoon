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
        private float timer;

        private void Awake()
        {
            provider = CreateProvider();
            if (provider == null)
            {
                Debug.LogWarning("[SystemLoadMonitor] Keine Speicherlast-Quelle auf dieser Plattform verfügbar.");
            }
        }

        private void Start()
        {
            Poll(); // Erste Messung sofort, nicht erst nach dem ersten Intervall.
        }

        private void Update()
        {
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
            provider?.Dispose();
            provider = null;
        }
    }
}
