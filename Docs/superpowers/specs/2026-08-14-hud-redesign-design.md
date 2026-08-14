# HUD-Überarbeitung — Design

Datum: 2026-08-14
Projekt: AI Tycoon (Unity)

## Ziel

Das bestehende HUD soll moderner, hochwertiger und deutlich besser lesbar wirken —
**ohne** vorhandene Funktionalität, Scripts, Referenzen oder Interaktionen zu entfernen
und **ohne** Gameplay-Logik zu ändern. Positionen und Gruppierung der Elemente bleiben
wie sie sind; verbessert werden Kontrast, Panels, visuelle Hierarchie und Abstände.

Priorität: **Lesbarkeit > klare Hierarchie > Kontrast > modernes Design > dekorative Effekte.**

## Ausgangslage (analysiert)

Canvas `[HUD_Canvas]` (ScreenSpaceOverlay, CanvasScaler ScaleWithScreenSize, Referenz
1920×1080, MatchWidthOrHeight = 0). Kinder:

| Element | Rolle | Ecke | anchoredPos | Größe | Sprite |
|---|---|---|---|---|---|
| `SystemLoadBar` | Status (Stromlast) | oben rechts | (-100, -40) | 340×56 | Panel (Image, schwarz 55%) |
| `Energy` (Zap) | Status (gehört zu Stromlast) | oben rechts | (0, -22) | 100×100 | Zap |
| `Agents` (Citizen) | Button → Menü | oben links | (50, -150) | 100×100 | Citizen |
| `Quests` (Clipboard) | Button → Menü | oben links | (50, -25) | 95×95 | Tile_Energy_Clipboard_big_icon |
| `Settings` | Button → Menü | unten links | (50, 35) | 100×100 | Settings |
| `InfoLabel` | Kind von SystemLoadBar (Status-Text, TMP) | — | — | — | — |

`SystemLoadBar` trägt `SystemLoadBarUI` mit serialisierten Referenzen: `monitor`,
`segmentContainer` (→ `Segments`, HorizontalLayoutGroup), `statusLabel`, `tooltipPanel`,
`tooltipLabel`. `SystemLoadBarUI.Awake()` baut die 16 Segmente zur Laufzeit dynamisch.

### Identifizierte Probleme

1. **Überlappung:** `Energy` (100×100 in der rechten oberen Ecke) liegt über der `SystemLoadBar`;
   beide gehören logisch zusammen, wirken aber nicht als Einheit.
2. **Kein Kontrast:** Die Menü-Buttons (Agents/Quests/Settings) sind blanke Icons ohne
   Panel-Hintergrund — vor hellen Spielbereichen schlecht lesbar.
3. **Inkonsistente Größen:** 100/100/95/100 px — fast, aber nicht ganz einheitlich.
4. **Schwacher Panel-Look:** SystemLoadBar nutzt ein hartes schwarzes Image (55%), nicht den
   vorhandenen Frosted-Glass-Look.
5. **Text ohne Schatten/Outline:** kann vor hellen Hintergründen untergehen.
6. **Keine klare Hierarchie / uneinheitliche Ränder.**

## Vorhandene, wiederverwendbare Assets

- `Assets/_AITycoon/UI/Materials/UIFrostedGlass.mat` (Kawase-Blur-Shader, Tint dunkelblau
  `#1A2035` @ ~72%) — für dunkle, transparente Panels.
- Icons: `Zap.png`, `Citizen.png`, `Tile_Energy_Clipboard_big_icon.png`, `Settings.png`.
- Editor-Tool-Muster: `SystemLoadBarCreator` (`Assets/_AITycoon/Features/SystemLoad/Editor/`).

## Zielbild

- **Oben rechts — Status-Cluster "Stromlast":** `Energy`/Zap-Icon + Titel + Segmentleiste +
  Status-Text als **eine** zusammenhängende Frosted-Glass-Panel-Einheit. Keine Überlappung mehr.
- **Links — Menü-Dock:** `Agents` + `Quests` in einem gemeinsamen dunklen Panel gruppiert,
  einheitliche Button-Größe. `Settings` unten links (eigenes kleines Panel).
- **Akzent Bernstein `#FFB74D`:** nur sparsam — kritischer Last-Zustand (Status-Text/hohe
  Segmente) und aktiver/Hover-Button-Rahmen. **Keine** harte Prozentzahl auf der Leiste
  (Konzept "Raum zeigt Klischee, Datenblatt zeigt Wahrheit" — echte Zahlen bleiben im
  Hover-Datenblatt-Tooltip).

## Farb- & Typo-System (`HudTheme`)

Zentrale statische Klasse als Single Source of Truth (Farben später leicht anpassbar):

- Panel-Hintergrund: **primär** das vorhandene `UIFrostedGlass`-Material (Tint `#1A2035` @ ~72%,
  Ist-Wert des Materials). Fallback ohne Blur: solides `#1A2035` @ ~0.66 Alpha.
- Panel-Rand: `rgba(255,255,255,0.10)`, oben heller Inset `rgba(255,255,255,0.08)`
- Text primär: `#F4F6FB` (fast weiß), Bold, TMP-Outline/Shadow dezent
- Text sekundär: `#9AA4B8` (gedämpftes Grau)
- Akzent: `#FFB74D` (Bernstein/Gold), sparsam
- Segmente: leer `rgba(255,255,255,0.09)`; belegt grün `#5BD074` → gelb `#F2C94C` → rot `#EB5757`
  (VU-Meter, Farbe nach Segmentposition — bestehende Logik in `SystemLoadBarUI` bleibt)
- Ränder/Abstände: einheitlicher Bildschirmrand 16px, Panel-Innenabstand 12–14px, Button 52px

## Architektur der Umsetzung

**Ansatz: versionierte Editor-Skripte** (Menü "AI Tycoon → UI → …"), ausgeführt im
**Edit-Modus** (nicht Play). Kein Live-RunCommand für persistente Änderungen (im Play-Modus
sind Szenen-Änderungen flüchtig).

Neue Dateien unter `Assets/_AITycoon/UI/Editor/`:

- **`HudTheme.cs`** (Runtime, unter `Assets/_AITycoon/UI/`): statische Farb-/Maß-Konstanten.
  Wird sowohl vom Editor-Skript als auch ggf. zur Laufzeit (SystemLoadBarUI-Farben) genutzt.
  → Liegt außerhalb `Editor/`, damit Runtime-Code darauf zugreifen kann.
- **`HudStyleApplier.cs`** (Editor): Menü **"AI Tycoon → UI → HUD-Style anwenden"**.
  - Findet Objekte im `[HUD_Canvas]` per Name.
  - Wendet Panel-Hintergründe, Farben, Abstände, TMP-Outline/Shadow an.
  - Legt Gruppen-Panels an (Status-Cluster oben rechts, Menü-Dock links) und parentet
    die bestehenden Elemente hinein — **ohne** Komponenten/Referenzen zu zerstören.
  - **Idempotent:** erkennt bereits erzeugte Style-Objekte (per Name/Tag/Marker-Component)
    und aktualisiert statt zu duplizieren.
  - Nutzt `Undo`-Registrierung und `EditorSceneManager.MarkSceneDirty`.

### Umgang mit Referenzen (Nicht-Zerstörungs-Garantie)

- `SystemLoadBarUI`, `Image`, Klick-Handler, `segmentContainer`, `statusLabel`,
  `tooltipPanel`, `tooltipLabel` bleiben erhalten. Beim Umparenten via
  `Transform.SetParent(newParent, worldPositionStays: false)` bleiben Component-Referenzen
  gültig (nur der Transform-Elternteil ändert sich).
- Farb-/Layout-Felder von `SystemLoadBarUI` (emptyColor/lowColor/…) werden — falls angepasst —
  über `SerializedObject` gesetzt, nicht durch Neuerstellen der Komponente.
- Die dynamische Segment-Erzeugung in `Awake()` bleibt unverändert; das Editor-Skript ändert
  nur Container-Layout/Farbwerte, keine Logik.

## Skalierung / Auflösung

- CanvasScaler bleibt ScaleWithScreenSize @ 1920×1080. `MatchWidthOrHeight` wird auf **0.5**
  gesetzt (statt 0), damit HUD bei unterschiedlichen Aspect Ratios ausgewogener skaliert.
- Alle Panels über Ecken-Anchors verankert (oben-rechts, oben-links, unten-links), damit sie
  bei anderen Auflösungen an ihrer Ecke bleiben.

## Verifikation (nach Umsetzung)

1. **Console** auf neue Fehler prüfen (`Unity.GetConsoleLogs`) — nur bekannte AI-Assistant-
   Netzwerkfehler dürfen erscheinen.
2. **Referenz-Check:** Editor-Skript loggt, ob alle `SystemLoadBarUI`-Referenzen nach dem
   Styling noch gesetzt sind (nicht null).
3. **Visuell:** Screenshot/Play im Edit→Play prüfen — Elemente an richtiger Ecke, lesbar über
   heller UND dunkler Zone, keine Überlappung, Hover-Datenblatt funktioniert weiter.
4. **Idempotenz:** Menü zweimal ausführen → keine Duplikate.

## Explizit außerhalb des Scopes (YAGNI)

- Keine neuen HUD-Funktionen, keine neuen Menüs/Panels-Inhalte, keine Icons neu erstellen.
- Keine Gameplay-/Ressourcen-Logik.
- Kein Umgruppieren der Elemente an andere Bildschirmseiten (User: "so wie es gerade ist").
- Keine sichtbare Prozentzahl auf der Leiste.
