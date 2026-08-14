using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AITycoon.Features.SystemLoad.EditorTools
{
    /// <summary>
    /// Bedienfeld, um die Denklast von Hand zu setzen statt zu messen:
    /// Menue "AI Tycoon → Denklast simulieren".
    ///
    /// Warum es das braucht: die echte Messung liegt auf einem Arbeitsrechner dauerhaft im
    /// mittleren Bereich. Die gelbe und die rote Zone von HUD und Lastsaeule — also genau die
    /// Zustaende, in denen das Spiel etwas erzaehlen soll — bekaeme man ohne dieses Fenster
    /// nie zu sehen.
    ///
    /// Das Fenster faelscht bewusst NICHT die Anzeige, sondern tauscht nur die Quelle
    /// (<see cref="SimulatedLoadProvider"/> ueber <see cref="SystemLoadMonitor.OverrideProvider"/>).
    /// Der simulierte Wert durchlaeuft damit exakt dieselbe Kette wie ein Messwert. Ein Test, der
    /// die Presenter umgeht, wuerde ueber deren Korrektheit nichts aussagen.
    ///
    /// Alle Zahlen unten (Segmentanzahl, Schwellen) werden zur Laufzeit aus
    /// <see cref="LoadPillar"/> und <see cref="SystemLoadBarUI"/> gelesen und nicht hier
    /// dupliziert — sonst wuerde das Werkzeug irgendwann etwas anderes behaupten als das Spiel.
    /// </summary>
    public class SystemLoadSimulatorWindow : EditorWindow
    {
        private SimulatedLoadProvider simulation;
        private bool simulationActive;

        private float targetFraction = 0.5f;

        private bool sweeping;
        private float sweepSpeed = 0.2f;
        private int sweepDirection = 1;

        private bool keepRunningUnfocused = true;
        private bool previousRunInBackground;

        private double lastUpdateTime;

        /// <summary>Alles, was das Fenster ueber die Lastsaeule wissen muss — frisch gelesen.</summary>
        private struct PillarInfo
        {
            public LoadPillar Pillar;
            public int SegmentCount;
            public float WarnThreshold;
            public float CriticalThreshold;
        }

        [MenuItem("AI Tycoon/Denklast simulieren")]
        public static void Open()
        {
            GetWindow<SystemLoadSimulatorWindow>("Denklast").minSize = new Vector2(360f, 430f);
        }

        private void OnEnable()
        {
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StopSimulation();
        }

        // ------------------------------------------------------------------ Takt

        private void OnEditorUpdate()
        {
            // Editor-Zeit statt Time.deltaTime: die Game-Loop steht, sobald der Editor den Fokus
            // verliert und runInBackground aus ist. Der Sweep soll davon unabhaengig laufen.
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - lastUpdateTime);
            lastUpdateTime = now;

            if (!simulationActive)
                return;

            SystemLoadMonitor monitor = FindMonitor();
            if (monitor == null)
            {
                StopSimulation();
                Repaint();
                return;
            }

            if (sweeping)
                AdvanceSweep(deltaTime);

            PushValue(monitor);
            Repaint();
        }

        /// <summary>Faehrt den Wert zwischen 0 und 1 hin und her (Ping-Pong).</summary>
        private void AdvanceSweep(float deltaTime)
        {
            targetFraction += sweepDirection * sweepSpeed * deltaTime;

            if (targetFraction >= 1f)
            {
                targetFraction = 1f;
                sweepDirection = -1;
                return;
            }

            if (targetFraction <= 0f)
            {
                targetFraction = 0f;
                sweepDirection = 1;
            }
        }

        private void PushValue(SystemLoadMonitor monitor)
        {
            simulation.Fraction01 = targetFraction;
            monitor.ForceSample();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Beim Verlassen des Play Mode ist der Monitor weg — der Zustand hier muss mit.
            if (change == PlayModeStateChange.ExitingPlayMode)
                StopSimulation();
        }

        // ------------------------------------------------------------------ Start / Stopp

        private void StartSimulation(SystemLoadMonitor monitor)
        {
            simulation ??= new SimulatedLoadProvider();

            // Die echte Gesamtgroesse uebernehmen, damit die GB-Zahlen im Datenblatt plausibel
            // bleiben. Nur der Anteil wird simuliert, nicht die Groesse der Maschine.
            if (monitor.HasData && monitor.Current.TotalBytes > 0)
                simulation.TotalBytes = monitor.Current.TotalBytes;

            if (keepRunningUnfocused)
                EnableRunInBackground();

            simulationActive = true;
            monitor.OverrideProvider(simulation);
            PushValue(monitor);
        }

        private void StopSimulation()
        {
            if (!simulationActive)
                return;

            simulationActive = false;
            sweeping = false;

            SystemLoadMonitor monitor = FindMonitor();
            if (monitor != null)
                monitor.OverrideProvider(null);

            RestoreRunInBackground();
        }

        /// <summary>
        /// Ohne das steht die Game-Loop, sobald der Editor den Fokus verliert. Sichtbar wird das
        /// vor allem beim Blowout: dessen WaitForSeconds laeuft dann nie ab und der Hebel bleibt
        /// unten haengen — was wie ein Fehler im Spiel aussieht, aber keiner ist.
        /// Nur zur Laufzeit gesetzt, die PlayerSettings bleiben unberuehrt.
        /// </summary>
        private void EnableRunInBackground()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        private void RestoreRunInBackground()
        {
            if (EditorApplication.isPlaying)
                Application.runInBackground = previousRunInBackground;
        }

        // ------------------------------------------------------------------ Oberflaeche

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Denklast simulieren", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Nur im Play Mode. Die Presenter haengen am SystemLoadMonitor, und der "
                    + "misst erst, wenn das Spiel laeuft.", MessageType.Info);
                simulationActive = false;
                return;
            }

            SystemLoadMonitor monitor = FindMonitor();
            if (monitor == null)
            {
                EditorGUILayout.HelpBox(
                    "Kein SystemLoadMonitor in der Szene. Menue \"AI Tycoon → UI → "
                    + "System-Lastleiste erstellen\" legt einen an.", MessageType.Warning);
                return;
            }

            DrawSourceSection(monitor);
            EditorGUILayout.Space(6f);
            DrawValueSection(monitor);
            EditorGUILayout.Space(6f);
            DrawSweepSection();
            EditorGUILayout.Space(6f);
            DrawBlowoutSection();
            EditorGUILayout.Space(6f);
            DrawCapacitySection();
            EditorGUILayout.Space(6f);
            DrawReadout(monitor);
        }

        private void DrawSourceSection(SystemLoadMonitor monitor)
        {
            EditorGUILayout.LabelField("Quelle", EditorStyles.boldLabel);

            bool wanted = EditorGUILayout.ToggleLeft(
                simulationActive ? "Simulation aktiv (echte Messung ausgesetzt)" : "Simulation einschalten",
                simulationActive);

            if (wanted != simulationActive)
            {
                if (wanted)
                    StartSimulation(monitor);
                else
                    StopSimulation();
            }

            using (new EditorGUI.DisabledScope(simulationActive))
            {
                keepRunningUnfocused = EditorGUILayout.ToggleLeft(
                    "Weiterlaufen, wenn der Editor den Fokus verliert", keepRunningUnfocused);
            }

            EditorGUILayout.LabelField(
                "gemeldet als", monitor.HasData ? monitor.Current.SourceLabel : "—");
        }

        private void DrawValueSection(SystemLoadMonitor monitor)
        {
            using (new EditorGUI.DisabledScope(!simulationActive))
            {
                EditorGUILayout.LabelField("Wert", EditorStyles.boldLabel);

                float percent = EditorGUILayout.Slider(
                    "Denklast %", targetFraction * 100f, 0f, 100f);

                if (!Mathf.Approximately(percent * 0.01f, targetFraction))
                {
                    targetFraction = percent * 0.01f;
                    sweeping = false;
                    if (simulationActive)
                        PushValue(monitor);
                }

                DrawJumpMarks(monitor);
            }
        }

        /// <summary>
        /// Sprungmarken auf die Werte, ab denen das erste gelbe bzw. erste rote Segment leuchtet.
        /// Beide werden aus der tatsaechlichen Segmentanzahl und den tatsaechlichen Schwellen der
        /// Lastsaeule gerechnet — die Segmentanzahl ist ein Parameter des Blender-Generators und
        /// darf hier nicht als Konstante liegen.
        /// </summary>
        private void DrawJumpMarks(SystemLoadMonitor monitor)
        {
            PillarInfo info = ReadPillar();
            if (info.Pillar == null || info.SegmentCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Keine Lastsaeule mit Segmenten gefunden — Sprungmarken nicht berechenbar.",
                    MessageType.None);
                return;
            }

            int firstWarn = FirstIndexInZone(info.SegmentCount, info.WarnThreshold);
            int firstCritical = FirstIndexInZone(info.SegmentCount, info.CriticalThreshold);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawJumpButton(monitor, "Leer", 0f);
                DrawJumpButton(monitor, "Gruen", LightUpFraction(0, info.SegmentCount));

                if (firstWarn >= 0)
                    DrawJumpButton(monitor, "1. Gelb", LightUpFraction(firstWarn, info.SegmentCount));

                if (firstCritical >= 0)
                    DrawJumpButton(monitor, "1. Rot", LightUpFraction(firstCritical, info.SegmentCount));

                DrawJumpButton(monitor, "Voll", 1f);
            }
        }

        private void DrawJumpButton(SystemLoadMonitor monitor, string label, float fraction)
        {
            if (!GUILayout.Button(label))
                return;

            targetFraction = Mathf.Clamp01(fraction);
            sweeping = false;
            if (simulationActive)
                PushValue(monitor);
        }

        private void DrawSweepSection()
        {
            using (new EditorGUI.DisabledScope(!simulationActive))
            {
                EditorGUILayout.LabelField("Automatik", EditorStyles.boldLabel);
                sweeping = EditorGUILayout.ToggleLeft(
                    "Wert durchfahren (auf und ab)", sweeping);
                sweepSpeed = EditorGUILayout.Slider("Tempo (Anteil/s)", sweepSpeed, 0.02f, 1f);
            }
        }

        private void DrawBlowoutSection()
        {
            EditorGUILayout.LabelField("Reaktion", EditorStyles.boldLabel);

            LoadPillar pillar = ReadPillar().Pillar;
            using (new EditorGUI.DisabledScope(pillar == null))
            {
                if (GUILayout.Button("Sicherung ausloesen"))
                    pillar.TriggerBlowout();
            }

            if (pillar == null)
                EditorGUILayout.HelpBox("Keine LoadPillar in der Szene.", MessageType.None);
        }

        /// <summary>
        /// Blendet Modul/Kipphebel-Paare der Sicherungsbank ein und aus — die Vorschau auf das
        /// Elektriker-Ausbau-Event (Konzept §2.3): belegte Felder = gebaute Denklast-Kapazitaet.
        /// Reine Testansicht im Play Mode; die echte Ausbau-Logik lebt spaeter im Gameplay-Code
        /// und dieses Fenster liest die Paare nur ueber die Vertragsnamen aus der FBX-Hierarchie.
        /// </summary>
        private void DrawCapacitySection()
        {
            EditorGUILayout.LabelField("Ausbau (Sicherungsbank)", EditorStyles.boldLabel);

            LoadPillar pillar = ReadPillar().Pillar;
            if (pillar == null)
            {
                EditorGUILayout.HelpBox("Keine LoadPillar in der Szene.", MessageType.None);
                return;
            }

            List<(GameObject module, GameObject toggle)> pairs = FindModulePairs(pillar.transform);
            if (pairs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Keine Breaker_Mod_XX am Sicherungskasten gefunden — FBX-Stand vor "
                    + "ASSET_VERSION 3?", MessageType.None);
                return;
            }

            int active = 0;
            foreach ((GameObject module, GameObject _) in pairs)
            {
                if (module.activeSelf)
                    active++;
            }

            // Mindestens 1: ein Sicherungskasten ohne einzige Sicherung erzaehlt nichts.
            int wanted = EditorGUILayout.IntSlider("Belegte Felder", active, 1, pairs.Count);
            if (wanted == active)
                return;

            for (int i = 0; i < pairs.Count; i++)
            {
                bool on = i < wanted;
                pairs[i].module.SetActive(on);
                if (pairs[i].toggle != null)
                    pairs[i].toggle.SetActive(on);
            }
        }

        /// <summary>
        /// Modul/Kipphebel-Paare der Sicherungsbank, ueber die Vertragsnamen gesammelt
        /// (Breaker_Mod_XX direkt unter der Instanz, Breaker_Tog_XX unter Breaker_Bank).
        /// Transform.Find findet auch deaktivierte Kinder — bereits ausgeblendete Module
        /// verschwinden also nicht aus der Zaehlung.
        /// </summary>
        private static List<(GameObject module, GameObject toggle)> FindModulePairs(Transform root)
        {
            var pairs = new List<(GameObject, GameObject)>();
            for (int i = 0; ; i++)
            {
                Transform module = root.Find($"Breaker_Mod_{i:D2}");
                if (module == null)
                    break;

                Transform toggle = root.Find($"Breaker_Bank/Breaker_Tog_{i:D2}");
                pairs.Add((module.gameObject, toggle != null ? toggle.gameObject : null));
            }
            return pairs;
        }

        private void DrawReadout(SystemLoadMonitor monitor)
        {
            EditorGUILayout.LabelField("Ablesung", EditorStyles.boldLabel);

            if (!monitor.HasData)
            {
                EditorGUILayout.LabelField("noch kein Messwert");
                return;
            }

            SystemLoadSample sample = monitor.Current;
            EditorGUILayout.LabelField(
                "Anteil", $"{sample.Fraction01:P1}   ({sample.UsedGb:0.0} / {sample.TotalGb:0.0} GB)");

            PillarInfo info = ReadPillar();
            if (info.Pillar != null && info.SegmentCount > 0)
            {
                EditorGUILayout.LabelField(
                    "Lastsaeule", DescribeSegments(sample.Fraction01, info.SegmentCount,
                                                   info.WarnThreshold, info.CriticalThreshold));
            }

            int hudSegments = ReadHudSegmentCount();
            if (hudSegments > 0)
            {
                EditorGUILayout.LabelField(
                    "HUD-Leiste", DescribeSegments(sample.Fraction01, hudSegments,
                                                   info.WarnThreshold, info.CriticalThreshold));
            }
        }

        // ------------------------------------------------------------------ Rechnen und Lesen

        /// <summary>
        /// Ein Anteil, bei dem Segment <paramref name="index"/> zuverlaessig leuchtet.
        ///
        /// Die Presenter runden mit <c>RoundToInt(fraction * count)</c>, die Kippkante fuer
        /// Segment i liegt also bei <c>fraction * count == i + 0.5</c>. Genau diesen Wert zu
        /// nehmen waere falsch: <see cref="Mathf.RoundToInt"/> rundet nach round-half-to-even,
        /// und die Kante faellt dadurch je nach Index mal nach oben und mal nach unten
        /// (7.5 -> 8, aber 10.5 -> 10). Deshalb die Mitte zwischen zwei Kanten, also i + 1.
        /// </summary>
        private static float LightUpFraction(int index, int count) => (index + 1f) / count;

        /// <summary>Index des ersten Segments, dessen Position in oder ueber der Zone liegt.</summary>
        private static int FirstIndexInZone(int count, float threshold)
        {
            for (int i = 0; i < count; i++)
            {
                if ((i + 0.5f) / count >= threshold)
                    return i;
            }
            return -1;
        }

        private static string DescribeSegments(float fraction, int count, float warn, float critical)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * count), 0, count);

            int green = 0, yellow = 0, red = 0;
            for (int i = 0; i < filled; i++)
            {
                float position = (i + 0.5f) / count;
                if (position < warn)
                    green++;
                else if (position < critical)
                    yellow++;
                else
                    red++;
            }

            return $"{filled} / {count}   ({green} gruen, {yellow} gelb, {red} rot)";
        }

        private static SystemLoadMonitor FindMonitor() =>
            Object.FindAnyObjectByType<SystemLoadMonitor>(FindObjectsInactive.Exclude);

        /// <summary>
        /// Liest Segmentanzahl und Schwellen aus der Lastsaeule. Bewusst ueber SerializedObject:
        /// die Felder sind privat und sollen es bleiben — ein Werkzeug ist kein Grund, die
        /// Kapselung einer Spielklasse aufzubrechen.
        /// </summary>
        private static PillarInfo ReadPillar()
        {
            var pillar = Object.FindAnyObjectByType<LoadPillar>(FindObjectsInactive.Exclude);
            if (pillar == null)
                return default;

            var serialized = new SerializedObject(pillar);
            return new PillarInfo
            {
                Pillar = pillar,
                SegmentCount = serialized.FindProperty("segments").arraySize,
                WarnThreshold = serialized.FindProperty("warnThreshold").floatValue,
                CriticalThreshold = serialized.FindProperty("criticalThreshold").floatValue
            };
        }

        private static int ReadHudSegmentCount()
        {
            var bar = Object.FindAnyObjectByType<SystemLoadBarUI>(FindObjectsInactive.Exclude);
            if (bar == null)
                return 0;

            return new SerializedObject(bar).FindProperty("segmentCount").intValue;
        }
    }
}
