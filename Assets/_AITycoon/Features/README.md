# Features

Jeder Unterordner hier ist ein "Vertical Slice": ein abgeschlossenes Spiel-System mit allem was dazugehoert
(Skripte, Prefabs, ggf. eigene Materialien/Daten).

## Richtig
- Neuen Ordner pro Feature anlegen (z.B. Features/Inventory/)
- Alles, was NUR dieses eine Feature betrifft, bleibt im jeweiligen Unterordner

## Falsch
- Direkt Dateien in Features/ ablegen (immer erst Unterordner pro Feature erstellen)
- Zwei Features hart aneinander koppeln (Feature A ruft Klassen aus Feature B direkt auf).
  Kommunikation lieber ueber Core/ Events oder Interfaces.
