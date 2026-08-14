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
| Tris | ~3.300 |
| Konzept | `Docs/AI-Tycoon Konzept.md` §2.3 |

Wandobjekt. **Ursprung:** Rückseite = Wandkontaktebene, horizontal zentriert, Unterkante bei Z 0.
Platzieren heißt also: an die Wandfläche setzen, Montagehöhe über die Y-Position bestimmen.

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

Die Blender-Materialnamen sind **exakt** die Unity-Materialnamen. 8 der 9 exportierten Materialien
existieren dadurch bereits im Projekt (aus dem nappin-Pack) und binden sich beim Import von selbst —
vorausgesetzt, der Model-Importer steht auf `materialSearch = Everywhere`. Das erzwingt
`Assets/_AITycoon/Art/Editor/FuseBoxModelPostprocessor.cs` bei jedem Re-Import automatisch;
Unitys Default `RecursiveUp` würde die Materialien in `Assets/ThirdParty/` nie finden.

Die **Segmente** exportieren bewusst mit *einem* gemeinsamen dunklen Material
(`(Mat)FuseSegment_Off`), nicht mit ihren Bandfarben: ein unbespieltes Prefab soll nicht wie
Volllast aussehen, und die Farbe setzt zur Laufzeit ohnehin `LoadPillar`. Die Bandfarben
zeigt in Blender `F.preview_load(0.58)`.

### Bewegliche Teile

| Teil | Ursprung | Achse (Blender) | Achse (Unity) | Ruhe | Ausschlag |
|---|---|---|---|---|---|
| `Breaker_Lever` | Drehpunkt der Schaltertafel | Y | lokal Z | 0° (senkrecht = EIN) | 135° (geflogen) |
| `Box_Door` | linke Scharnierkante | Z | lokal Y | 0° (zu) | ca. −75° (offen) |

Der Hebelausschlag ist mit `lever_sweep_bounds()` so gewählt, dass die Hebelspitze über den
gesamten Schwenk in der Schaltzone bleibt und nie mit Tür oder Glas kollidiert — er schwenkt
flach vor der Front, nicht in die Tiefe.

**Segmentzahl ändern:** `CONFIG["seg_count"]` setzen, dann `build_segments()` und
`assign_materials()` erneut ausführen. Die Farbbänder rechnen sich aus den Schwellen
0,60 / 0,85 automatisch neu — dieselbe Formel, die auch die HUD-Leiste benutzt.
Danach in Unity `AI Tycoon → Setup → Sicherungskasten einrichten` erneut aufrufen;
das Editor-Tool liest die Segmentzahl aus der Hierarchie und codiert sie nicht hart.
