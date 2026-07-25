# UI

Alle Menues, HUD-Elemente und View-Skripte, die Daten aus dem Spiel visuell darstellen.

## Richtig
- Canvas-Prefabs, HUD-Controller, Menu-Skripte
- UI-Skripte, die auf Events aus Core/Features hoeren und Werte anzeigen (Presenter-Pattern)

## Falsch
- Geschaeftslogik direkt in UI-Skripten (z.B. Ressourcen-Berechnung im Button-OnClick).
  Die Logik gehoert ins jeweilige Feature, UI ruft nur auf / zeigt Ergebnis an.
