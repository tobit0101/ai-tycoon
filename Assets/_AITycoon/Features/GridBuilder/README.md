# Features/GridBuilder

Das Bau-/Placement-System (Raster, Moebel platzieren, Kollisionscheck).

## Richtig
- Grid-Datenstruktur, Placement-Validierung, Snapping-Logik
- Bau-Vorschau (Ghost-Objekte), Rotations-Handling

## Falsch
- Agenten-Verhalten -> Features/AI_Agents/
- Reine Deko-Assets ohne Bau-Logik -> gehoeren eher in Art/ oder ThirdParty/, hier nur wenn sie
  Teil der Bau-Mechanik sind (z.B. ein Prefab mit Grid-Snapping-Komponente)
