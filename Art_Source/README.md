# Art_Source

Blender-Quelldateien für eigene 3D-Assets.

**Bewusst außerhalb von `Assets/`**: Unity kann `.blend` zwar direkt importieren, braucht dafür
aber auf jeder Maschine ein installiertes Blender. Wir exportieren stattdessen FBX nach
`Assets/_AITycoon/Art/Models/`. Beide Formate gehen laut `.gitattributes` automatisch über Git LFS.

## FuseBox — Hauptsicherungskasten mit Lastsäule

| | |
|---|---|
| Quelle | `FuseBox.blend` (Blender 5.2 LTS) |
| Generator | `fusebox_build.py` |
| Export | `Assets/_AITycoon/Art/Models/FuseBox.fbx` |
| Maße | 1,16 m × 0,425 m × 1,45 m (B × T × H) |
| Tris | ~3.470 (inkl. Sicherungsbank und Decal-Quads) |
| Konzept | `Docs/AI-Tycoon Konzept.md` §2.3 |

Wandobjekt. **Ursprung:** Rückseite = Wandkontaktebene, horizontal zentriert, Unterkante bei Z 0.
Platzieren heißt also: an die Wandfläche setzen, Montagehöhe über die Y-Position bestimmen.

### Vertrag

Der Unity-Builder bindet per `transform.Find(...)` an Objektnamen — diese Namen sind
faktisch eine **API**. Umbenennen ist ausdrücklich eine **Breaking Change** und braucht
einen abgestimmten Doppel-Commit (FBX + Unity-Code).

**Stabile Objektnamen:**

| Name | Wird gebunden von / Zweck |
|---|---|
| `FuseBox_Root` | Wurzel der Baugruppe, Ursprung = Wandebene/X-Mitte/Unterkante |
| `Segments` + `Segment_00`…`Segment_11` | `transform.Find("Segments")`; Index 00 = **unten**; LoadPillar färbt zur Laufzeit |
| `Breaker_Lever` | `transform.Find("Breaker_Lever")`; Kipp-Animation "Sicherung fliegt" |
| `Breaker_Bank` (mit Kindern `Breaker_Tog_00`…`_05`) | Kipphebel-Bank in der Nische; kippt beim Blowout synchron zum Hebel (lokal X) |
| `Breaker_Mod_00`…`_05` + `Breaker_Tog_00`…`_05` | DIN-Module; paarweise ein-/ausblenden = sichtbare Denklast-Kapazität (Elektriker-Event) |
| `Box_Door` (mit Kindern `Box_DoorGlass`, `Box_Handle`) | Tür-Animation; Glas und Griff drehen mit |
| `FX_Breaker`, `FX_ColumnTop`, `FX_LeverTip` | Ansatzpunkte für spätere Partikeleffekte (Funken / Rauch–Statuslicht / Hebelspitze in Ruhestellung) |

**Bewusste Drehpunkte** (Ursprünge sind Absicht, nicht Zufall):

- `Breaker_Lever`: Ursprung am Drehlager der Schaltertafel, Rotation um Blender-Y.
- `Box_Door`: Ursprung an der linken Scharnierkante, Rotation um Blender-Z.
- `Breaker_Bank`: Empty auf der Kippachse der DIN-Schiene, Rotation um Blender-X
  (`CONFIG["bank_blowout_deg"]` = geflogen, Vorschau: `F.set_bank(-55)`).

**Animations-Konvention:** Bewegungen sind zustandsgetrieben und werden zur Laufzeit
per Transform-Rotation animiert (Coroutine/Tween mit AnimationCurve) — bewusst keine
FBX-Clips und kein Animator. Die Kipphebel der Bank sind neutral dunkel gebacken;
Rot zeigt Unity im Fehlerfall per MaterialPropertyBlock-Tint.

`CONFIG["ASSET_VERSION"]` zählt bei jeder inhaltlichen Änderung des Exports hoch
(1 = Modellbau, 2 = Textur-Stufe, 3 = Sicherungsbank in der Nische statt rotem Block).

### Achsen

In Blender zeigt die Front nach **−Y**, oben ist **+Z**. Der FBX-Export mit `-Z Forward / Y Up`
ergibt in Unity korrekt +Z forward / +Y up. Eine Rotation um Blenders Y-Achse (der Sicherungshebel)
entspricht in Unity einer Rotation um die **lokale Z-Achse**.

### Stil-Regeln (nicht verhandelbar)

Das Modell folgt dem Look von `Assets/ThirdParty/nappin/OfficeEssentialsPack/`:

- **Ein Material pro Farbe**, Basis ist eine vertikale Gradient-Rampe auf `_BaseMap`.
- Die Rampen sind oberhalb V ≈ 0,30 flach und dunkeln nur nach unten ab.
- Deshalb: **UVs mappen die Objekthöhe auf V** — und zwar über die *gesamte Baugruppe*,
  nicht pro Einzelteil, damit der Verlauf durchläuft. Siehe `apply_uvs()`.
- Blender-Materialnamen sind **exakt** die Unity-Materialnamen. Dadurch bindet Unitys
  FBX-Importer die vorhandenen nappin-Materialien beim Import von selbst.
- Flat Shading, leichte Bevels (Modifier bleiben live, werden erst beim FBX-Export gebacken).

### Textur-Stufe (ASSET_VERSION 2)

Der Kasten nutzt seit der Textur-Stufe **eigene gebackene Maps** statt der geteilten
nappin-Gradient-Materialien (bewusster Trade-off: echtes AO + Grundmuster + regelbarer
Look, dafür verlässt der Kasten das geteilte Materialsystem):

- `Assets/_AITycoon/Art/Textures/T_FuseBox_BaseColor.png` — sRGB. Enthält den alten
  Look (nappin-Rampe über die Welthöhe gesampelt) × Orangenhaut-Grundmuster
  (Box-Projektion, ~1 Kachel/Modell) × Kantenaufhellung (Pointiness) × Staub (nur auf
  Flächen mit Normale nach oben).
- `Assets/_AITycoon/Art/Textures/T_FuseBox_AO.png` — **Non-Color**. Reines AO der
  Gesamtbaugruppe. Bewusst NICHT in die BaseColor eingebrannt, damit die Stärke in
  Unity gegen die vorhandene SSAO geregelt werden kann.
- `Assets/_AITycoon/Art/Textures/T_FuseBox_Labels.png` — Sticker-Sheet für die
  `Decal_*`-Quads (Alpha-Clip, triviale UV-Ausschnitte, unabhängig vom Atlas).
- `Assets/_AITycoon/Art/Textures/T_FuseBox_Circuit.png` — opake Schaltnetz-Grafik
  für `Decal_Niche` (Nischenrückwand hinter dem Sichtfenster, Material
  `M_FuseBox_Circuit`).
- `Art_Source/T_FuseBox_Pattern.png` — Zutat für den Bake, **kein** Spiel-Asset
  (Graustufen, Mittelwert 0,5, σ 0,05; Einblendung normiert sich über den Mittelwert).

Atlas: Cube Projection pro Objekt → `average_islands_scale` → `pack_islands`
(16 px Margin bei 2048). Gemessene Texeldichte: **~449 px/m bei 2048** (Band 256–512).
**Ausgenommen bleiben:** `Segment_*` (Laufzeit-Einfärbung per MaterialPropertyBlock),
`Box_DoorGlass` (Transparenz passt nicht in ein opakes Atlas-Material, behält
`(Mat)Glass`), `Decal_*` (eigene UVs).

Nach Geometrie-Änderungen neu texturieren:

```python
F.unwrap_for_bake(atlas_px=2048, margin_px=16)
print(F.texel_density_report())
F.build_bake_materials()      # braucht Art_Source/T_FuseBox_Pattern.png
F.bake_maps(resolution=2048)  # Cycles; der langsame Schritt
F.build_atlas_material()      # M_FuseBox (BaseColor x AO nur als Blender-Vorschau)
F.build_labels()              # Decal-Quads + M_FuseBox_Labels
F.assign_materials()
```

Dosierung in `fusebox_build.py`: `PATTERN_STRENGTH`, `EDGE_STRENGTH`, `DUST_STRENGTH`,
`PATTERN_SCALE` — Zielbild ist "dezent angebraucht", nicht verranzt.

### Regenerieren / Ändern

`fusebox_build.py` ist ein Blender-Modul, kein Standalone-Skript. Alle Maße, Farben und die
**Segmentzahl** stehen im `CONFIG`-Dict am Dateianfang. Jede Bau-Funktion ist idempotent
(löscht gleichnamige Objekte zuerst), Änderungen sind also beliebig oft wiederholbar.

In Blenders Python-Konsole:

```python
import sys, importlib
sys.path.append("/Users/tobias/develop/ai-tycoon/Art_Source")
import fusebox_build as F
importlib.reload(F)

F.build_plate(); F.build_box(); F.build_breaker()
F.build_column(); F.build_segments(); F.build_details()
F.build_materials(); F.assign_materials(); F.apply_uvs()
F.bake_rotations(); F.build_hierarchy()
F.export_fbx("/Users/tobias/develop/ai-tycoon/Assets/_AITycoon/Art/Models/FuseBox.fbx")
```

Nützliche Helfer:

| Aufruf | Zweck |
|---|---|
| `F.segment_slots()` | Z-Grenzen der Segmente |
| `F.seg_band(i)` | Farbband eines Segments — dieselbe Formel wie `SystemLoadBarUI` |
| `F.preview_load(0.58)` | Lastzustand in Blender simulieren (Bandfarbe vs. Off-Material) |
| `F.set_lever(grad)` | Hebelstellung: `0` = EIN, `CONFIG["lever_blowout_deg"]` = geflogen |
| `F.set_door(grad)` | Türstellung, `0` = zu. Glas und Griff drehen mit |
| `F.lever_sweep_bounds()` | Kollisionscheck über den gesamten Schwenkbereich |
| `F.qa()` | Abschlussprüfung (Material, UV, Scale, Rotation, Hierarchie) — leer = sauber |
| `F.purge_orphans()` | verwaiste Datenblöcke entfernen |
| `F.tri_count()` / `F.report()` | Polycount und Objektliste |

**Vor jedem Export:** `F.set_door(0)`, `F.set_lever(0)`, `F.assign_materials()`, `F.qa()` —
sonst wandert eine Vorschau-Stellung ins FBX.

### Materialien

Die Blender-Materialnamen sind **exakt** die Unity-Materialnamen. Seit der Textur-Stufe
exportiert der Kasten mit `M_FuseBox` (Atlas: BaseColor + AO), `M_FuseBox_Labels`
(Sticker-Sheet, Alpha-Clip), `M_FuseBox_Circuit` (Schaltnetz-Nische) — alle drei liegen als
Assets in `Assets/_AITycoon/Art/Materials/` — plus `(Mat)Glass` aus dem nappin-Pack.
Die Bindung übernimmt `Assets/_AITycoon/Art/Editor/FuseBoxModelPostprocessor.cs`
(`OnAssignMaterialModel`, Namens-Lookup über beide Materialbibliotheken) bei jedem Re-Import.

Die **Segmente** exportieren bewusst mit *einem* gemeinsamen dunklen Material
(`(Mat)FuseSegment_Off`), nicht mit ihren Bandfarben: ein unbespieltes Prefab soll nicht wie
Volllast aussehen, und die Farbe setzt zur Laufzeit ohnehin `LoadPillar`. Die Bandfarben
zeigt in Blender `F.preview_load(0.58)`.

### Bewegliche Teile

| Teil | Ursprung | Achse (Blender) | Achse (Unity) | Ruhe | Ausschlag |
|---|---|---|---|---|---|
| `Breaker_Lever` | Drehpunkt der Schaltertafel | Y | lokal Z | 0° (senkrecht = EIN) | 135° (geflogen) |
| `Breaker_Bank` | Kippachse der DIN-Schiene | X | lokal X | 0° (alle EIN) | −55° (alle geflogen) |
| `Box_Door` | linke Scharnierkante | Z | lokal Y | 0° (zu) | ca. −75° (offen) |

Der Hebelausschlag ist mit `lever_sweep_bounds()` so gewählt, dass die Hebelspitze über den
gesamten Schwenk in der Schaltzone bleibt und nie mit Tür oder Glas kollidiert — er schwenkt
flach vor der Front, nicht in die Tiefe.

**Segmentzahl ändern:** `CONFIG["seg_count"]` setzen, dann `build_segments()` und
`assign_materials()` erneut ausführen. Die Farbbänder rechnen sich aus den Schwellen
0,60 / 0,85 automatisch neu — dieselbe Formel, die auch die HUD-Leiste benutzt.
Danach in Unity `AI Tycoon → Setup → Sicherungskasten einrichten` erneut aufrufen;
das Editor-Tool liest die Segmentzahl aus der Hierarchie und codiert sie nicht hart.
