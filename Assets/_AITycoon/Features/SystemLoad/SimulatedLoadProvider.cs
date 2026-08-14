using UnityEngine;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Eine Speicherlast-Quelle, deren Wert gesetzt statt gemessen wird.
    ///
    /// Zweck: den gesamten Wertebereich pruefbar machen. Die echte Messung liefert auf einem
    /// Arbeitsrechner ueber Stunden vielleicht 40-60 % — die gelbe und die rote Zone von HUD und
    /// Lastsaeule bekaeme man sonst nie zu sehen, ohne den Rechner kuenstlich vollzuladen.
    ///
    /// Bewusst ein <see cref="ISystemLoadProvider"/> und kein Sonderweg im Monitor: die Abstraktion
    /// existiert bereits, und so durchlaeuft der simulierte Wert exakt dieselbe Kette wie ein
    /// echter Messwert (Poll -> SampleUpdated -> SystemLoadBarUI und LoadPillar). Was hier
    /// funktioniert, funktioniert damit auch im Echtbetrieb — ein Test, der an der Praesentation
    /// vorbeigeht, wuerde genau das nicht belegen.
    ///
    /// Liegt bewusst im Runtime-Ordner und nicht unter Editor/: die Klasse ist auch die Grundlage
    /// fuer ein spaeteres In-Game-Debug-Menue. Sie wird von sich aus nie aktiv — nur wer
    /// <see cref="SystemLoadMonitor.OverrideProvider"/> aufruft, bekommt simulierte Werte.
    /// </summary>
    public sealed class SimulatedLoadProvider : ISystemLoadProvider
    {
        /// <summary>Referenzgroesse, wenn keine echte Gesamtgroesse bekannt ist: 64 GB.</summary>
        public const ulong DefaultTotalBytes = 64UL * 1024UL * 1024UL * 1024UL;

        private float fraction01;

        /// <summary>
        /// Kennzeichnet die Quelle im Datenblatt-Tooltip. Absichtlich als "Simulation" sichtbar:
        /// eine simulierte Zahl darf nie wie eine gemessene aussehen.
        /// </summary>
        public string SourceLabel { get; set; } = "Simulation";

        /// <summary>Gesamtgroesse, gegen die gerechnet wird. Nur fuer die GB-Anzeige relevant.</summary>
        public ulong TotalBytes { get; set; } = DefaultTotalBytes;

        /// <summary>Die simulierte Auslastung. Wird beim Setzen auf 0..1 geklemmt.</summary>
        public float Fraction01
        {
            get => fraction01;
            set => fraction01 = Mathf.Clamp01(value);
        }

        public bool TryGetSample(out SystemLoadSample sample)
        {
            sample = new SystemLoadSample
            {
                UsedBytes = (ulong)(fraction01 * TotalBytes),
                TotalBytes = TotalBytes,
                SourceLabel = SourceLabel
            };
            return true;
        }

        public void Dispose()
        {
            // Keine Ressourcen zu halten.
        }
    }
}
