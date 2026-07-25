# Data

ScriptableObject-basierte Datencontainer: Konfigurationswerte, die Designer im Inspector aendern koennen.

## Richtig
- Agenten-Rollen-Definitionen, Item-/Moebel-Definitionen, Prompt-Templates als SO
- Balancing-Werte (Kosten, Ressourcenverbrauch etc.)

## Falsch
- Code mit Spiel-Logik (SOs sollten moeglichst nur Daten halten, keine komplexe Logik)
- Runtime-Zustand (z.B. aktueller Spielstand) -> das gehoert in Save/Load-Systeme, nicht in die
  gleichen SO-Assets, die auch als Vorlage/Config dienen
